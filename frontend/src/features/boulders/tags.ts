import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";

/** A tag in the gym's list, with how many boulders on the wall carry it. */
export interface GymTag { id: string; name: string; boulders: number }

export const tagKeys = { list: (gymId: string) => ["tags", gymId] as const };

export function useGymTags(gymId: string | undefined) {
  return useQuery({
    queryKey: tagKeys.list(gymId ?? ""),
    queryFn: ({ signal }) => api.get<GymTag[]>(`/api/gyms/${gymId}/tags`, { signal }),
    enabled: !!gymId,
    staleTime: 60_000,
  });
}

function useRefresh(gymId: string) {
  const qc = useQueryClient();
  return () => Promise.all([
    qc.invalidateQueries({ queryKey: tagKeys.list(gymId) }),
    // Boulders show their tags by name: a rename or a delete changes them too.
    qc.invalidateQueries({ queryKey: ["boulders"] }),
  ]);
}

export function useCreateTag(gymId: string) {
  const refresh = useRefresh(gymId);
  return useMutation({ mutationFn: (name: string) => api.post<GymTag>(`/api/gyms/${gymId}/tags`, { name }), onSuccess: refresh });
}

export function useRenameTag(gymId: string) {
  const refresh = useRefresh(gymId);
  return useMutation({ mutationFn: ({ id, name }: { id: string; name: string }) => api.patch<GymTag>(`/api/tags/${id}`, { name }), onSuccess: refresh });
}

export function useDeleteTag(gymId: string) {
  const refresh = useRefresh(gymId);
  return useMutation({ mutationFn: (id: string) => api.delete<void>(`/api/tags/${id}`), onSuccess: refresh });
}
