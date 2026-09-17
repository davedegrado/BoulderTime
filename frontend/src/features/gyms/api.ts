import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { PagedResult } from "@/lib/paging";
import type { GymRole, GymStatus } from "@/lib/format";

export interface GymSummary {
  id: string;
  slug: string;
  name: string;
  city: string;
  logoUrl: string | null;
  coverImageUrl: string | null;
  status: GymStatus;
}

export interface GymDetail extends GymSummary {
  description: string | null;
  address: string | null;
  website: string | null;
  email: string | null;
  phone: string | null;
  createdAt: string;
  viewerRole: GymRole | null;
  follow: { isFollowing: boolean; isFavorite: boolean; notificationsEnabled: boolean } | null;
  followerCount: number;
}

export interface Sector {
  id: string;
  gymId: string;
  name: string;
  description: string | null;
  imageUrl: string | null;
  sortOrder: number;
  isActive: boolean;
  isFollowing: boolean;
}

export interface UpdateGymInput {
  name: string;
  city: string;
  description: string;
  address: string;
  website: string;
  email: string;
  phone: string;
}

export const gymKeys = {
  all: ["gyms"] as const,
  search: (q: string) => ["gyms", "search", q] as const,
  detail: (slug: string) => ["gyms", "detail", slug] as const,
  sectors: (gymId: string) => ["gyms", gymId, "sectors"] as const,
};

const PAGE_SIZE = 20;

export function useGymSearch(query: string) {
  return useInfiniteQuery({
    queryKey: gymKeys.search(query),
    queryFn: ({ pageParam, signal }) =>
      api.get<PagedResult<GymSummary>>(`/api/gyms?q=${encodeURIComponent(query)}&page=${pageParam}&pageSize=${PAGE_SIZE}`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
    placeholderData: keepPreviousData,
  });
}

export function useGym(slug: string) {
  return useQuery({
    queryKey: gymKeys.detail(slug),
    queryFn: ({ signal }) => api.get<GymDetail>(`/api/gyms/${encodeURIComponent(slug)}`, { signal }),
  });
}

export function useSectors(gymId: string | undefined) {
  return useQuery({
    queryKey: gymKeys.sectors(gymId ?? ""),
    queryFn: ({ signal }) => api.get<Sector[]>(`/api/gyms/${gymId}/sectors`, { signal }),
    enabled: !!gymId,
  });
}

export function useUpdateGym(gymId: string, slug: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateGymInput) => api.patch<GymDetail>(`/api/gyms/${gymId}`, input),
    onSuccess: (gym) => {
      qc.setQueryData(gymKeys.detail(slug), gym);
      qc.invalidateQueries({ queryKey: ["gyms", "search"] });
    },
  });
}

export function useCreateSector(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: { name: string; description?: string }) => api.post<Sector>(`/api/gyms/${gymId}/sectors`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: gymKeys.sectors(gymId) }),
  });
}

export function useUpdateSector(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...input }: { id: string; name?: string; description?: string; isActive?: boolean }) =>
      api.patch<Sector>(`/api/sectors/${id}`, input),
    onSuccess: () => qc.invalidateQueries({ queryKey: gymKeys.sectors(gymId) }),
  });
}

export function useReorderSectors(gymId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (sectorIds: string[]) => api.put<Sector[]>(`/api/gyms/${gymId}/sectors/order`, { sectorIds }),
    // Optimistic: the list reorders immediately under the thumb, then reconciles with the server.
    onMutate: async (sectorIds) => {
      await qc.cancelQueries({ queryKey: gymKeys.sectors(gymId) });
      const previous = qc.getQueryData<Sector[]>(gymKeys.sectors(gymId));
      if (previous) {
        const byId = new Map(previous.map((s) => [s.id, s]));
        qc.setQueryData(gymKeys.sectors(gymId), sectorIds.map((id, i) => ({ ...byId.get(id)!, sortOrder: i })));
      }
      return { previous };
    },
    onError: (_e, _v, ctx) => ctx?.previous && qc.setQueryData(gymKeys.sectors(gymId), ctx.previous),
    onSettled: () => qc.invalidateQueries({ queryKey: gymKeys.sectors(gymId) }),
  });
}
