import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { captureVideoThumbnail } from "@/features/community/videoThumbnail";
import { t } from "@/i18n/i18n";
import { api } from "@/lib/api";
import { ApiError, defaultMessage } from "@/lib/apiError";
import type { PagedResult } from "@/lib/paging";
import type { BoulderSummary } from "@/features/boulders/api";
import type { GradeSystemType } from "@/features/grading/api";

export interface Person { userId: string; displayName: string; avatarUrl: string | null }

export interface Comment {
  id: string; boulderId: string; author: Person; content: string; status: "VISIBLE" | "HIDDEN";
  createdAt: string; editedAt: string | null; likes: number; likedByViewer: boolean; isMine: boolean; canModerate: boolean;
}

export interface ConsensusBucket { gradeValueId: string; label: string; rank: number; colorHex: string | null; votes: number }
export interface SystemConsensus {
  gradeSystemId: string; systemName: string; systemType: GradeSystemType; totalVotes: number;
  consensusValueId: string | null; officialValueId: string | null; viewerValueId: string | null;
  buckets: ConsensusBucket[]; scale: ConsensusBucket[];
}
export interface GradeConsensus { viewerCanSuggest: boolean; systems: SystemConsensus[] }

export type VideoStatus = "PENDING" | "APPROVED" | "REJECTED";
export interface Beta { id: string; boulderId: string; videoUrl: string; thumbnailUrl: string | null; caption: string | null; uploadedBy: Person; updatedAt: string }
export interface Video { id: string; boulderId: string; author: Person; videoUrl: string; thumbnailUrl: string | null; caption: string | null; status: VideoStatus; rejectionReason: string | null; createdAt: string; isMine: boolean }
export interface BoulderVideos { approved: PagedResult<Video>; mineInReview: Video[] }
export interface ModerationVideo { video: Video; boulder: BoulderSummary }

export type ReportEntityType = "COMMENT" | "VIDEO" | "BOULDER";
export type ReportReason = "INAPPROPRIATE" | "WRONG_BOULDER" | "SPAM" | "MISLEADING" | "OTHER";
export type ReportStatus = "PENDING" | "RESOLVED" | "DISMISSED";
export interface Report {
  id: string; entityType: ReportEntityType; entityId: string; gymId: string; gymName: string; boulderId: string;
  reason: ReportReason; description: string | null; status: ReportStatus; resolutionNote: string | null;
  reportedBy: Person; createdAt: string; reviewedAt: string | null; excerpt: string | null;
}
export interface ModerationSummary { pendingVideos: number; pendingReports: number }

const REPORT_REASONS: Record<ReportReason, string> = {
  INAPPROPRIATE: "Inappropriate content", WRONG_BOULDER: "Wrong boulder", SPAM: "Spam", MISLEADING: "Misleading", OTHER: "Other",
};

/** Read at render time, so the labels follow the language in use. */
export const reportReasonLabel: Record<ReportReason, string> = new Proxy({} as Record<ReportReason, string>, {
  get: (_, key: string) => t(REPORT_REASONS[key as ReportReason] ?? key),
  ownKeys: () => Object.keys(REPORT_REASONS),
  getOwnPropertyDescriptor: () => ({ enumerable: true, configurable: true }),
});

interface ResumableUpload { endpoint: string; headers: Record<string, string>; metadata: Record<string, string>; chunkSize: number }
interface UploadTicket { path: string; uploadUrl: string; method: string; headers: Record<string, string>; maxBytes: number; resumable?: ResumableUpload | null }

export const MAX_VIDEO_BYTES = 100 * 1024 * 1024;

export const communityKeys = {
  comments: (boulderId: string) => ["community", "comments", boulderId] as const,
  consensus: (boulderId: string) => ["community", "consensus", boulderId] as const,
  beta: (boulderId: string) => ["community", "beta", boulderId] as const,
  videos: (boulderId: string) => ["community", "videos", boulderId] as const,
  moderationVideos: (gymId: string) => ["moderation", gymId, "videos"] as const,
  moderationReports: (scope: string, status: ReportStatus) => ["moderation", scope, "reports", status] as const,
  summary: (gymId: string) => ["moderation", gymId, "summary"] as const,
};

/** Uploads a file to a storage ticket with progress (fetch can't report upload progress). */
export function putWithProgress(ticket: UploadTicket, body: Blob, onProgress?: (fraction: number) => void): Promise<void> {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open(ticket.method, ticket.uploadUrl);
    for (const [k, v] of Object.entries(ticket.headers)) xhr.setRequestHeader(k, v);
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress?.(e.loaded / e.total);
    xhr.onload = () => (xhr.status >= 200 && xhr.status < 300 ? resolve() : reject(new ApiError(xhr.status, null, t("The upload failed. Try again."))));
    xhr.onerror = () => reject(new ApiError(0, null, defaultMessage(0)));
    xhr.send(body);
  });
}

/** Delays between automatic retries of a failed chunk (e.g. the phone briefly losing signal). */
export const RESUMABLE_RETRY_DELAYS = [0, 1000, 3000, 5000, 10000, 20000];

/**
 * Uploads a file in chunks over the tus protocol. If the connection drops, each chunk is retried and the upload
 * continues from the last byte the server confirmed — it never restarts from zero.
 */
export async function uploadResumable(ticket: UploadTicket & { resumable: ResumableUpload }, file: Blob, onProgress?: (fraction: number) => void): Promise<void> {
  // Loaded on demand: only people who actually upload a video download the tus client.
  const { Upload: TusUpload } = await import("tus-js-client");
  return new Promise((resolve, reject) => {
    const r = ticket.resumable;
    const upload = new TusUpload(file, {
      endpoint: new URL(r.endpoint, window.location.origin).href,
      headers: r.headers,
      metadata: r.metadata,
      chunkSize: r.chunkSize,
      retryDelays: RESUMABLE_RETRY_DELAYS,
      // Every ticket targets a fresh object, so there's nothing to resume across sessions.
      storeFingerprintForResuming: false,
      onProgress: (sent, total) => onProgress?.(total ? sent / total : 0),
      onSuccess: () => resolve(),
      onError: (error) => {
        const status = (error as { originalResponse?: { getStatus(): number } }).originalResponse?.getStatus() ?? 0;
        reject(new ApiError(status, null, status === 0
          ? t("The upload was interrupted. Check your connection and try again.")
          : status === 413 ? "The video is too large." : "The upload failed. Try again."));
      },
    });
    upload.start();
  });
}

export interface UploadedVideo { path: string; thumbnailPath: string | null }

/**
 * Uploads a video (resumable) plus a poster frame captured on the device. A thumbnail that can't be produced or
 * uploaded never blocks the video itself.
 */
export async function uploadVideo(boulderId: string, file: File, kind: "COMMUNITY" | "BETA", onProgress?: (f: number) => void): Promise<UploadedVideo> {
  if (file.size > MAX_VIDEO_BYTES) throw new ApiError(400, null, t("Videos must be under 100 MB. Trim it and try again."));
  const contentType = file.type || "video/mp4";
  const ticket = await api.post<UploadTicket>(`/api/boulders/${boulderId}/video-uploads`, { kind, contentType, sizeBytes: file.size });
  if (ticket.resumable) await uploadResumable({ ...ticket, resumable: ticket.resumable }, file, onProgress);
  else await putWithProgress(ticket, file, onProgress);

  let thumbnailPath: string | null = null;
  try {
    const thumb = await captureVideoThumbnail(file);
    if (thumb) {
      const thumbTicket = await api.post<UploadTicket>(`/api/boulders/${boulderId}/video-uploads`,
        { kind: kind === "BETA" ? "BETA_THUMBNAIL" : "COMMUNITY_THUMBNAIL", contentType: "image/jpeg", sizeBytes: thumb.size });
      await putWithProgress(thumbTicket, thumb);
      thumbnailPath = thumbTicket.path;
    }
  } catch {
    thumbnailPath = null;
  }
  return { path: ticket.path, thumbnailPath };
}

// ---- Comments ----

export function useComments(boulderId: string) {
  return useInfiniteQuery({
    queryKey: communityKeys.comments(boulderId),
    queryFn: ({ pageParam, signal }) => api.get<PagedResult<Comment>>(`/api/boulders/${boulderId}/comments?page=${pageParam}&pageSize=30`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
  });
}

export function useCommentMutations(boulderId: string) {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: communityKeys.comments(boulderId) });
  return {
    create: useMutation({ mutationFn: (content: string) => api.post<Comment>(`/api/boulders/${boulderId}/comments`, { content }), onSuccess: invalidate }),
    edit: useMutation({ mutationFn: ({ id, content }: { id: string; content: string }) => api.patch<Comment>(`/api/comments/${id}`, { content }), onSuccess: invalidate }),
    remove: useMutation({ mutationFn: (id: string) => api.delete(`/api/comments/${id}`), onSuccess: invalidate }),
    like: useMutation({ mutationFn: ({ id, like }: { id: string; like: boolean }) => (like ? api.put<Comment>(`/api/comments/${id}/like`) : api.delete<Comment>(`/api/comments/${id}/like`)), onSuccess: invalidate }),
    hide: useMutation({ mutationFn: ({ id, hide }: { id: string; hide: boolean }) => api.post<Comment>(`/api/comments/${id}/${hide ? "hide" : "unhide"}`), onSuccess: invalidate }),
  };
}

// ---- Community grade ----

export function useConsensus(boulderId: string) {
  return useQuery({ queryKey: communityKeys.consensus(boulderId), queryFn: ({ signal }) => api.get<GradeConsensus>(`/api/boulders/${boulderId}/grade-consensus`, { signal }) });
}

export function useSuggestGrade(boulderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ gradeSystemId, gradeValueId }: { gradeSystemId: string; gradeValueId: string | null }) =>
      gradeValueId
        ? api.put<GradeConsensus>(`/api/boulders/${boulderId}/grade-suggestions`, { gradeSystemId, gradeValueId })
        : api.delete<GradeConsensus>(`/api/boulders/${boulderId}/grade-suggestions/${gradeSystemId}`),
    onSuccess: (data) => qc.setQueryData(communityKeys.consensus(boulderId), data),
  });
}

// ---- Videos ----

export function useBeta(boulderId: string) {
  return useQuery({ queryKey: communityKeys.beta(boulderId), queryFn: async ({ signal }) => (await api.get<Beta | undefined>(`/api/boulders/${boulderId}/beta`, { signal })) ?? null });
}

export function useSaveBeta(boulderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { storagePath: string; caption: string; thumbnailPath: string | null } | null) =>
      input ? api.put<Beta>(`/api/boulders/${boulderId}/beta`, input) : api.delete(`/api/boulders/${boulderId}/beta`),
    onSuccess: () => qc.invalidateQueries({ queryKey: communityKeys.beta(boulderId) }),
  });
}

export function useVideos(boulderId: string) {
  return useInfiniteQuery({
    queryKey: communityKeys.videos(boulderId),
    queryFn: ({ pageParam, signal }) => api.get<BoulderVideos>(`/api/boulders/${boulderId}/videos?page=${pageParam}&pageSize=12`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.approved.hasMore ? last.approved.page + 1 : undefined),
  });
}

export function useVideoMutations(boulderId: string) {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: communityKeys.videos(boulderId) });
  return {
    submit: useMutation({ mutationFn: (input: { storagePath: string; caption: string; thumbnailPath: string | null }) => api.post<Video>(`/api/boulders/${boulderId}/videos`, input), onSuccess: invalidate }),
    remove: useMutation({ mutationFn: (id: string) => api.delete(`/api/videos/${id}`), onSuccess: invalidate }),
  };
}

// ---- Reports & moderation ----

export function useCreateReport() {
  return useMutation({
    mutationFn: (input: { entityType: ReportEntityType; entityId: string; reason: ReportReason; description?: string }) => api.post<Report>("/api/reports", input),
  });
}

export function useModerationSummary(gymId: string) {
  return useQuery({ queryKey: communityKeys.summary(gymId), queryFn: ({ signal }) => api.get<ModerationSummary>(`/api/gyms/${gymId}/moderation/summary`, { signal }) });
}

export function usePendingVideos(gymId: string) {
  return useQuery({ queryKey: communityKeys.moderationVideos(gymId), queryFn: ({ signal }) => api.get<ModerationVideo[]>(`/api/gyms/${gymId}/moderation/videos`, { signal }) });
}

export function useReviewVideo(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, approve, reason }: { id: string; approve: boolean; reason?: string }) =>
      approve ? api.post<Video>(`/api/videos/${id}/approve`) : api.post<Video>(`/api/videos/${id}/reject`, { reason }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["moderation", gymId] }),
  });
}

/** `scope` is a gym id, or "all" for platform admins. */
export function useReports(scope: string, status: ReportStatus) {
  const url = scope === "all" ? `/api/admin/reports?status=${status}` : `/api/gyms/${scope}/moderation/reports?status=${status}`;
  return useQuery({ queryKey: communityKeys.moderationReports(scope, status), queryFn: ({ signal }) => api.get<PagedResult<Report>>(url, { signal }) });
}

export function useCloseReport() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, dismiss, removeContent, note }: { id: string; dismiss?: boolean; removeContent?: boolean; note?: string }) =>
      api.post<Report>(`/api/reports/${id}/${dismiss ? "dismiss" : "resolve"}`, { action: removeContent ? "REMOVE_CONTENT" : "NONE", note }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ["moderation"] });
      qc.invalidateQueries({ queryKey: ["community"] });
      qc.invalidateQueries({ queryKey: ["admin"] });
    },
  });
}
