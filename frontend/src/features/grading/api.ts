import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { labels } from "@/lib/format";

export type GradeSystemType = "COLOR" | "FONTAINEBLEAU" | "V_SCALE" | "CUSTOM";

/** Read at render time, so the names follow the language in use. */
export const gradeSystemTypeLabel = labels<GradeSystemType>({
  COLOR: "Colour grades",
  FONTAINEBLEAU: "Fontainebleau",
  V_SCALE: "V-scale",
  CUSTOM: "Custom",
});

export interface GradeValue { id: string; label: string; rank: number; colorHex: string | null; isActive: boolean }
export interface GradeSystem { id: string; gymId: string; name: string; type: GradeSystemType; isActive: boolean; sortOrder: number; values: GradeValue[] }
export interface GradeValueInput { id?: string; label: string; colorHex?: string | null }

export const gradingKeys = { list: (gymId: string) => ["grading", gymId] as const };

export function useGradeSystems(gymId: string | undefined) {
  return useQuery({
    queryKey: gradingKeys.list(gymId ?? ""),
    queryFn: ({ signal }) => api.get<GradeSystem[]>(`/api/gyms/${gymId}/grade-systems`, { signal }),
    enabled: !!gymId,
  });
}

function useInvalidate(gymId: string) {
  const qc = useQueryClient();
  return () => {
    qc.invalidateQueries({ queryKey: gradingKeys.list(gymId) });
    qc.invalidateQueries({ queryKey: ["boulders"] });
  };
}

export function useCreateGradeSystem(gymId: string) {
  const invalidate = useInvalidate(gymId);
  return useMutation({
    mutationFn: (input: { type: GradeSystemType; name?: string; values?: GradeValueInput[] }) => api.post<GradeSystem>(`/api/gyms/${gymId}/grade-systems`, input),
    onSuccess: invalidate,
  });
}

export function useUpdateGradeSystem(gymId: string) {
  const invalidate = useInvalidate(gymId);
  return useMutation({
    mutationFn: ({ id, ...input }: { id: string; name?: string; isActive?: boolean }) => api.patch<GradeSystem>(`/api/grade-systems/${id}`, input),
    onSuccess: invalidate,
  });
}

export function useSetGradeValues(gymId: string) {
  const invalidate = useInvalidate(gymId);
  return useMutation({
    mutationFn: ({ id, values }: { id: string; values: GradeValueInput[] }) => api.put<GradeSystem>(`/api/grade-systems/${id}/values`, { values }),
    onSuccess: invalidate,
  });
}

export function useReorderGradeSystems(gymId: string) {
  const invalidate = useInvalidate(gymId);
  return useMutation({
    mutationFn: (gradeSystemIds: string[]) => api.put<GradeSystem[]>(`/api/gyms/${gymId}/grade-systems/order`, { gradeSystemIds }),
    onSuccess: invalidate,
  });
}
