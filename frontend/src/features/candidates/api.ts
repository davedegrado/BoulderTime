import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { CandidateStatus } from "@/lib/format";

export interface GymCandidate {
  id: string;
  gymName: string;
  city: string;
  officialEmail: string | null;
  website: string | null;
  notes: string | null;
  status: CandidateStatus;
  createdAt: string;
  handledAt: string | null;
  gymId: string | null;
  submittedBy: string | null;
  submittedByEmail: string | null;
}

export interface SubmitCandidateInput {
  gymName: string;
  city: string;
  officialEmail: string;
  website: string;
  notes: string;
}

export const candidateKeys = { mine: ["gym-candidates", "mine"] as const };

export function useMyCandidates() {
  return useQuery({
    queryKey: candidateKeys.mine,
    queryFn: ({ signal }) => api.get<GymCandidate[]>("/api/users/me/gym-candidates", { signal }),
  });
}

export function useSubmitCandidate() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: SubmitCandidateInput) => api.post<GymCandidate>("/api/gym-candidates", input),
    onSuccess: () => qc.invalidateQueries({ queryKey: candidateKeys.mine }),
  });
}
