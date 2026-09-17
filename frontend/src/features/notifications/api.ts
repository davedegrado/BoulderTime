import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthProvider";
import type { PagedResult } from "@/lib/paging";
import type { Person } from "@/features/community/api";
import { prepareImage } from "@/features/boulders/imageResize";
import { putWithProgress } from "@/features/community/api";

export type NotificationType =
  | "GYM_ANNOUNCEMENT" | "SECTOR_RETRACED" | "BOULDER_UPDATED" | "OFFICIAL_BETA"
  | "BOULDER_COMMENTS" | "VIDEO_APPROVED" | "VIDEO_REJECTED" | "REPORT_REVIEWED"
  | "NEW_BOULDERS_IN_SECTOR" | "NEW_BOULDERS_AT_GYM";
export type NotificationCategory = "GYM_UPDATES" | "SECTOR_UPDATES" | "BOULDER_UPDATES" | "MY_CONTENT";

export interface AppNotification {
  id: string; type: NotificationType; category: NotificationCategory; title: string; body: string | null; link: string;
  relatedEntityType: string; relatedEntityId: string; count: number; isRead: boolean; createdAt: string;
}
export interface NotificationSettings { gymUpdates: boolean; sectorUpdates: boolean; boulderUpdates: boolean; myContent: boolean }

export type AnnouncementType = "ANNOUNCEMENT" | "EVENT" | "SCHEDULE_CHANGE" | "MAINTENANCE" | "COMPETITION" | "OTHER";
export const announcementTypeLabel: Record<AnnouncementType, string> = {
  ANNOUNCEMENT: "Announcement", EVENT: "Event", SCHEDULE_CHANGE: "Schedule change", MAINTENANCE: "Maintenance", COMPETITION: "Competition", OTHER: "Other",
};
export const needsEventDate = (t: AnnouncementType) => t === "EVENT" || t === "COMPETITION";

export interface Announcement {
  id: string; gymId: string; gymName: string; gymSlug: string; type: AnnouncementType; title: string; content: string;
  imageUrl: string | null; imagePath: string | null; eventDate: string | null; sectorId: string | null; sectorName: string | null;
  notifiedFollowers: boolean; author: Person; createdAt: string; updatedAt: string;
}
export interface SaveAnnouncementInput {
  type: AnnouncementType; title: string; content: string; sectorId: string | null; imagePath: string | null; eventDate: string | null; notifyFollowers?: boolean;
}

export const notificationKeys = {
  list: ["notifications", "list"] as const,
  unread: ["notifications", "unread"] as const,
  settings: ["notifications", "settings"] as const,
  follows: ["notifications", "follows"] as const,
  announcements: (gymId: string) => ["announcements", gymId] as const,
};

export function useNotifications() {
  return useInfiniteQuery({
    queryKey: notificationKeys.list,
    queryFn: ({ pageParam, signal }) => api.get<PagedResult<AppNotification>>(`/api/notifications?page=${pageParam}&pageSize=30`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
  });
}

/** Polled so the tab badge stays fresh while the app is open (push notifications come later). */
export function useUnreadCount() {
  const { session } = useAuth();
  return useQuery({
    queryKey: notificationKeys.unread,
    queryFn: ({ signal }) => api.get<{ unread: number }>("/api/notifications/unread-count", { signal }),
    enabled: !!session,
    refetchInterval: 60_000,
    refetchOnWindowFocus: true,
  });
}

export function useMarkRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string | "all") => (id === "all" ? api.post("/api/notifications/read-all") : api.post(`/api/notifications/${id}/read`)),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["notifications"] }),
  });
}

export function useNotificationSettings() {
  return useQuery({ queryKey: notificationKeys.settings, queryFn: ({ signal }) => api.get<NotificationSettings>("/api/users/me/notification-settings", { signal }) });
}

export function useUpdateNotificationSettings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (patch: Partial<NotificationSettings>) => api.put<NotificationSettings>("/api/users/me/notification-settings", patch),
    onMutate: (patch) => qc.setQueryData<NotificationSettings>(notificationKeys.settings, (s) => (s ? { ...s, ...patch } : s)),
    onSuccess: (s) => qc.setQueryData(notificationKeys.settings, s),
    onError: () => qc.invalidateQueries({ queryKey: notificationKeys.settings }),
  });
}

export interface MyFollows {
  gyms: { gym: { id: string; slug: string; name: string; city: string; logoUrl: string | null }; isFavorite: boolean; notificationsEnabled: boolean }[];
  sectors: { sectorId: string; sectorName: string; gymId: string; gymSlug: string; gymName: string; notificationsEnabled: boolean }[];
  boulders: { boulderId: string; gymId: string; gymName: string; sectorName: string; notificationsEnabled: boolean }[];
}

export function useMyFollows() {
  return useQuery({ queryKey: notificationKeys.follows, queryFn: ({ signal }) => api.get<MyFollows>("/api/users/me/follows", { signal }) });
}

/** Toggle notifications on a follow, or unfollow (enabled = null). */
export function useFollowSetting() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ kind, id, enabled }: { kind: "gyms" | "sectors" | "boulders"; id: string; enabled: boolean | null }) =>
      enabled === null ? api.delete(`/api/${kind}/${id}/follow`) : api.put(`/api/${kind}/${id}/follow`, { notificationsEnabled: enabled }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: notificationKeys.follows });
      qc.invalidateQueries({ queryKey: ["gyms"] });
      qc.invalidateQueries({ queryKey: ["climbing"] });
    },
  });
}

// ---- Announcements ----

export function useAnnouncements(gymId: string | undefined) {
  return useInfiniteQuery({
    queryKey: notificationKeys.announcements(gymId ?? ""),
    queryFn: ({ pageParam, signal }) => api.get<PagedResult<Announcement>>(`/api/gyms/${gymId}/announcements?page=${pageParam}&pageSize=20`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
    enabled: !!gymId,
  });
}

export function useAnnouncementMutations(gymId: string) {
  const qc = useQueryClient();
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: notificationKeys.announcements(gymId) });
    qc.invalidateQueries({ queryKey: ["climbing"] });
  };
  return {
    create: useMutation({ mutationFn: (input: SaveAnnouncementInput) => api.post<Announcement>(`/api/gyms/${gymId}/announcements`, input), onSuccess: invalidate }),
    update: useMutation({ mutationFn: ({ id, ...input }: SaveAnnouncementInput & { id: string }) => api.put<Announcement>(`/api/announcements/${id}`, input), onSuccess: invalidate }),
    remove: useMutation({ mutationFn: (id: string) => api.delete(`/api/announcements/${id}`), onSuccess: invalidate }),
  };
}

export async function uploadAnnouncementImage(gymId: string, file: File): Promise<string> {
  const blob = await prepareImage(file);
  const ticket = await api.post<{ path: string; uploadUrl: string; method: string; headers: Record<string, string>; maxBytes: number }>(
    `/api/gyms/${gymId}/announcement-images`, { contentType: blob.type || file.type, sizeBytes: blob.size });
  await putWithProgress(ticket, blob);
  return ticket.path;
}
