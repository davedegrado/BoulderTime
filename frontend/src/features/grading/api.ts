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

/**
 * `colorHex2`: second colour of an in-between grade ("green/blue"). `points`: what a send of it scores —
 * `pointsOverride` when staff set one, otherwise `automaticPoints` (10 for the easiest grade, 100 for the hardest).
 */
export interface GradeValue {
  id: string; label: string; rank: number; colorHex: string | null; isActive: boolean;
  colorHex2?: string | null; points?: number; automaticPoints?: number; pointsOverride?: number | null;
}
export interface GradeSystem { id: string; gymId: string; name: string; type: GradeSystemType; isActive: boolean; sortOrder: number; values: GradeValue[] }
/** `colorHex2`: "" makes it one colour. `points`: 0 goes back to automatic. Left out, both stay as they are. */
export interface GradeValueInput { id?: string; label: string; colorHex?: string | null; colorHex2?: string | null; points?: number }

/**
 * The automatic points of the grade at `index` (0 = easiest) on a scale of `count` grades in use: 10 for the easiest,
 * 100 for the hardest, evenly spaced. The same rule the server uses (RankBasedScoring), for previews while editing.
 */
export function automaticPoints(index: number, count: number): number {
  if (count <= 1) return 100;
  return Math.round(10 + (90 * index) / (count - 1));
}

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
    // Points follow the grades, past sends included.
    qc.invalidateQueries({ queryKey: ["leaderboard"] });
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
