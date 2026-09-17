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
  latitude: number | null;
  longitude: number | null;
  distanceKm?: number | null;
}

export interface GymPin { id: string; slug: string; name: string; city: string; logoUrl: string | null; latitude: number; longitude: number }
export interface Bounds { south: number; west: number; north: number; east: number }

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
  latitude?: number | null;
  longitude?: number | null;
  clearLocation?: boolean;
}

export const gymKeys = {
  all: ["gyms"] as const,
  search: (q: string) => ["gyms", "search", q] as const,
  detail: (slug: string) => ["gyms", "detail", slug] as const,
  sectors: (gymId: string) => ["gyms", gymId, "sectors"] as const,
};

const PAGE_SIZE = 20;

export function useGymSearch(query: string, near?: { lat: number; lng: number } | null) {
  const nearKey = near ? `${near.lat.toFixed(2)},${near.lng.toFixed(2)}` : "";
  return useInfiniteQuery({
    queryKey: [...gymKeys.search(query), nearKey],
    queryFn: ({ pageParam, signal }) =>
      api.get<PagedResult<GymSummary>>(`/api/gyms?q=${encodeURIComponent(query)}&page=${pageParam}&pageSize=${PAGE_SIZE}${near ? `&lat=${near.lat}&lng=${near.lng}` : ""}`, { signal }),
    initialPageParam: 1,
    getNextPageParam: (last) => (last.hasMore ? last.page + 1 : undefined),
    placeholderData: keepPreviousData,
  });
}

export function useGymPins(bounds: Bounds | null) {
  // Rounded so small pans reuse the cache.
  const key = bounds ? [bounds.south, bounds.west, bounds.north, bounds.east].map((n) => n.toFixed(2)).join(",") : "";
  return useQuery({
    queryKey: ["gyms", "pins", key],
    queryFn: ({ signal }) => api.get<GymPin[]>(`/api/gyms/map?south=${bounds!.south}&west=${bounds!.west}&north=${bounds!.north}&east=${bounds!.east}`, { signal }),
    enabled: !!bounds,
    placeholderData: keepPreviousData,
    staleTime: 5 * 60_000,
  });
}

export function geocodeGym(gymId: string, query: string) {
  return api.get<{ latitude: number; longitude: number; displayName: string }>(`/api/gyms/${gymId}/geocode?q=${encodeURIComponent(query)}`);
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
