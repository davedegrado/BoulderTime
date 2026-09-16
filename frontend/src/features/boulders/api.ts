import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { ApiError, defaultMessage } from "@/lib/apiError";
import type { PagedResult } from "@/lib/paging";
import type { GymRole } from "@/lib/format";
import type { GradeSystemType } from "@/features/grading/api";
import type { HoldColor } from "@/features/boulders/holdColors";
import { prepareBoulderPhoto } from "@/features/boulders/imageResize";

export type BoulderStatus = "ACTIVE" | "REMOVED";

export interface BoulderGrade { gradeSystemId: string; systemName: string; systemType: GradeSystemType; gradeValueId: string; label: string; rank: number; colorHex: string | null }

export interface BoulderSummary {
  id: string; gymId: string; sectorId: string; sectorName: string; photoUrl: string;
  holdColor: HoldColor; grades: BoulderGrade[]; status: BoulderStatus; createdAt: string; removedAt: string | null;
}

export interface BoulderDetail extends BoulderSummary {
  gymSlug: string; gymName: string; photoPath: string;
  setter: { userId: string; displayName: string; avatarUrl: string | null } | null;
  viewerRole: GymRole | null;
}

export interface BoulderFilters { status?: BoulderStatus; sectorId?: string; holdColor?: HoldColor; gradeValueId?: string }

export interface SaveBoulderInput {
  sectorId: string; photoPath: string; holdColor: HoldColor;
  grades: { gradeSystemId: string; gradeValueId: string }[];
  setterUserId: string | null;
}

export interface RemoveResult { removed: number; sectors: { sectorId: string; sectorName: string; removed: number }[] }

interface UploadTicket { bucket: string; path: string; uploadUrl: string; method: string; headers: Record<string, string>; maxBytes: number; expiresAt: string }

export const boulderKeys = {
  list: (gymId: string, f: BoulderFilters) => ["boulders", gymId, "list", f] as const,
  detail: (id: string) => ["boulders", "detail", id] as const,
};

const PAGE_SIZE = 24;

export function useBoulders(gymId: string | undefined, filters: BoulderFilters) {
  return useInfiniteQuery({
    queryKey: boulderKeys.list(gymId ?? "", filters),
    queryFn: ({ pageParam, signal }) => {
      const params = new URLSearchParams({ page: String(pageParam), pageSize: String(PAGE_SIZE) });
      for (const [k, v] of Object.entries(filters)) if (v) params.set(k, v);
      return api.get<PagedResult<BoulderSummary>>(`/api/gyms/${gymId}/boulders?${params}`, { signal });
    },
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
    placeholderData: keepPreviousData,
    enabled: !!gymId,
  });
}

export function useBoulder(id: string | undefined) {
  return useQuery({
    queryKey: boulderKeys.detail(id ?? ""),
    queryFn: ({ signal }) => api.get<BoulderDetail>(`/api/boulders/${id}`, { signal }),
    enabled: !!id,
  });
}

/** Shrinks the photo, asks the API for an upload ticket, and uploads straight to storage. Returns the object path. */
export async function uploadBoulderPhoto(gymId: string, file: File, onStage?: (stage: "processing" | "uploading") => void): Promise<string> {
  onStage?.("processing");
  const blob = await prepareBoulderPhoto(file);
  const contentType = blob.type || file.type;
  const ticket = await api.post<UploadTicket>(`/api/gyms/${gymId}/boulder-photos`, { contentType, sizeBytes: blob.size });

  onStage?.("uploading");
  let res: Response;
  try {
    res = await fetch(ticket.uploadUrl, { method: ticket.method, headers: ticket.headers, body: blob });
  } catch {
    throw new ApiError(0, null, defaultMessage(0));
  }
  if (!res.ok) throw new ApiError(res.status, null, "The photo upload failed. Try again.");
  return ticket.path;
}

function useInvalidateBoulders() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries({ queryKey: ["boulders"] });
}

export function useCreateBoulder(gymId: string) {
  const invalidate = useInvalidateBoulders();
  return useMutation({ mutationFn: (input: SaveBoulderInput) => api.post<BoulderDetail>(`/api/gyms/${gymId}/boulders`, input), onSuccess: invalidate });
}

export function useUpdateBoulder(boulderId: string) {
  const invalidate = useInvalidateBoulders();
  return useMutation({ mutationFn: (input: SaveBoulderInput) => api.put<BoulderDetail>(`/api/boulders/${boulderId}`, input), onSuccess: invalidate });
}

export function useRemoveBoulders(gymId: string) {
  const invalidate = useInvalidateBoulders();
  return useMutation({ mutationFn: (boulderIds: string[]) => api.post<RemoveResult>(`/api/gyms/${gymId}/boulders/remove`, { boulderIds }), onSuccess: invalidate });
}

export function useRestoreBoulder() {
  const invalidate = useInvalidateBoulders();
  return useMutation({ mutationFn: (boulderId: string) => api.post<BoulderDetail>(`/api/boulders/${boulderId}/restore`), onSuccess: invalidate });
}
