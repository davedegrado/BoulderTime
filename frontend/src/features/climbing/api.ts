import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthProvider";
import type { PagedResult } from "@/lib/paging";
import type { BoulderDetail, BoulderSummary, ViewerProgress } from "@/features/boulders/api";
import { boulderKeys } from "@/features/boulders/api";
import type { GymDetail, GymSummary, Sector } from "@/features/gyms/api";
import { gymKeys } from "@/features/gyms/api";
import type { GradeSystemType } from "@/features/grading/api";
import type { Announcement } from "@/features/notifications/api";

export interface HistoryItem { boulder: BoulderSummary; attempts: number; completed: boolean; completedAt: string | null; updatedAt: string; rating: number | null }
export interface ClimbingStats { completed: number; projects: number; totalAttempts: number; completedThisMonth: number }
export interface HighestGrade { gradeSystemId: string; systemName: string; systemType: GradeSystemType; gymName: string; label: string; rank: number; colorHex: string | null }
export interface WeekActivity { weekStart: string; completed: number }
export interface Profile {
  id: string; displayName: string; avatarUrl: string | null; memberSince: string; isMe: boolean;
  stats: ClimbingStats; highestGrades: HighestGrade[]; weekly: WeekActivity[];
  followedGyms: GymSummary[]; recentCompletions: HistoryItem[];
}
export interface HomeGym { gym: GymSummary; isFavorite: boolean; activeBoulders: number; newThisWeek: number }
export interface Home { stats: ClimbingStats; gyms: HomeGym[]; projects: BoulderSummary[]; freshToTry: BoulderSummary[]; recentCompletions: HistoryItem[]; updates: Announcement[] }
export type HistoryFilter = "ALL" | "COMPLETED" | "PROJECTS";

export const climbingKeys = {
  home: ["climbing", "home"] as const,
  profile: (userId: string) => ["climbing", "profile", userId] as const,
  history: (userId: string, filter: HistoryFilter) => ["climbing", "history", userId, filter] as const,
};

export function useHome() {
  const { session } = useAuth();
  return useQuery({ queryKey: climbingKeys.home, queryFn: ({ signal }) => api.get<Home>("/api/users/me/home", { signal }), enabled: !!session });
}

export function useProfile(userId: string | undefined) {
  return useQuery({
    queryKey: climbingKeys.profile(userId ?? ""),
    queryFn: ({ signal }) => api.get<Profile>(`/api/users/${userId}/profile`, { signal }),
    enabled: !!userId,
  });
}

export function useHistory(userId: string | undefined, filter: HistoryFilter) {
  return useInfiniteQuery({
    queryKey: climbingKeys.history(userId ?? "", filter),
    queryFn: ({ pageParam, signal }) =>
      api.get<PagedResult<HistoryItem>>(`/api/users/${userId}/history?filter=${filter}&page=${pageParam}&pageSize=20`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
    enabled: !!userId,
  });
}

/** After any progress change, the boulder detail is patched in place and lists/stats are refreshed in the background. */
function useProgressCache(boulderId: string) {
  const qc = useQueryClient();
  return (viewer: ViewerProgress | undefined) => {
    qc.setQueryData<BoulderDetail>(boulderKeys.detail(boulderId), (d) => (d ? { ...d, viewer: viewer ?? null } : d));
    qc.invalidateQueries({ queryKey: ["boulders"], refetchType: "none" });
    qc.invalidateQueries({ queryKey: ["climbing"] });
  };
}

export function useSetAttempt(boulderId: string) {
  const update = useProgressCache(boulderId);
  return useMutation({
    mutationFn: (input: { attempts: number; completed: boolean }) => api.put<ViewerProgress | undefined>(`/api/boulders/${boulderId}/attempt`, input),
    onSuccess: update,
  });
}

export function useSetRating(boulderId: string) {
  const update = useProgressCache(boulderId);
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (rating: number | null) =>
      rating === null ? api.delete<ViewerProgress>(`/api/boulders/${boulderId}/rating`) : api.put<ViewerProgress>(`/api/boulders/${boulderId}/rating`, { rating }),
    onSuccess: (viewer) => { update(viewer); qc.invalidateQueries({ queryKey: boulderKeys.detail(boulderId) }); },
  });
}

export function useFollowBoulder(boulderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (follow: boolean) => (follow ? api.put(`/api/boulders/${boulderId}/follow`, {}) : api.delete(`/api/boulders/${boulderId}/follow`)),
    onMutate: (follow) => qc.setQueryData<BoulderDetail>(boulderKeys.detail(boulderId), (d) => (d ? { ...d, isFollowing: follow } : d)),
    onError: (_e, follow) => qc.setQueryData<BoulderDetail>(boulderKeys.detail(boulderId), (d) => (d ? { ...d, isFollowing: !follow } : d)),
  });
}

export function useFollowGym(gym: GymDetail) {
  const qc = useQueryClient();
  const key = gymKeys.detail(gym.slug);
  return useMutation({
    mutationFn: (next: { isFollowing: boolean; isFavorite: boolean }) =>
      next.isFollowing ? api.put(`/api/gyms/${gym.id}/follow`, { isFavorite: next.isFavorite }) : api.delete(`/api/gyms/${gym.id}/follow`),
    onMutate: (next) => {
      const previous = qc.getQueryData<GymDetail>(key);
      qc.setQueryData<GymDetail>(key, (d) => d && {
        ...d,
        follow: { isFollowing: next.isFollowing, isFavorite: next.isFollowing && next.isFavorite, notificationsEnabled: d.follow?.notificationsEnabled ?? true },
        followerCount: d.followerCount + (next.isFollowing === !!d.follow?.isFollowing ? 0 : next.isFollowing ? 1 : -1),
      });
      return { previous };
    },
    onError: (_e, _v, ctx) => ctx?.previous && qc.setQueryData(key, ctx.previous),
    onSettled: () => qc.invalidateQueries({ queryKey: ["climbing"] }),
  });
}

export function useFollowSector(gymId: string) {
  const qc = useQueryClient();
  const key = gymKeys.sectors(gymId);
  return useMutation({
    mutationFn: ({ sectorId, follow }: { sectorId: string; follow: boolean }) =>
      follow ? api.put(`/api/sectors/${sectorId}/follow`, {}) : api.delete(`/api/sectors/${sectorId}/follow`),
    onMutate: ({ sectorId, follow }) => qc.setQueryData<Sector[]>(key, (list) => list?.map((s) => (s.id === sectorId ? { ...s, isFollowing: follow } : s))),
    onError: () => qc.invalidateQueries({ queryKey: key }),
  });
}
