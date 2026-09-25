import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import type { Person } from "@/features/community/api";
import type { GradeSystemType } from "@/features/grading/api";
import { t } from "@/i18n/i18n";

export type LeaderboardMetric = "POINTS" | "COMPLETED" | "HIGHEST";
export type LeaderboardPeriod = "WEEK" | "MONTH" | "YEAR" | "ALL";

export interface LeaderboardEntry {
  position: number; climber: Person; points: number; completed: number;
  highest: { label: string; rank: number; colorHex: string | null } | null; isViewer: boolean;
}

export interface Leaderboard {
  gymId: string; metric: LeaderboardMetric; period: LeaderboardPeriod; from: string | null;
  gradeSystemId: string | null; gradeSystemName: string | null; gradeSystemType: GradeSystemType | null; scoringExplanation: string;
  climbers: number; entries: LeaderboardEntry[]; viewer: LeaderboardEntry | null;
}

/** Labels are read at render time so they follow the language in use. */
export const metricLabel: Record<LeaderboardMetric, string> = new Proxy({} as Record<LeaderboardMetric, string>, {
  get: (_, key: string) => t({ POINTS: "Points", COMPLETED: "Sends", HIGHEST: "Highest grade" }[key as LeaderboardMetric] ?? key),
});
export const periodLabel: Record<LeaderboardPeriod, string> = new Proxy({} as Record<LeaderboardPeriod, string>, {
  get: (_, key: string) => t({ WEEK: "This week", MONTH: "This month", YEAR: "This year", ALL: "All time" }[key as LeaderboardPeriod] ?? key),
});

export function useLeaderboard(gymId: string, metric: LeaderboardMetric, period: LeaderboardPeriod) {
  return useQuery({
    queryKey: ["leaderboard", gymId, metric, period],
    queryFn: ({ signal }) => api.get<Leaderboard>(`/api/gyms/${gymId}/leaderboard?metric=${metric}&period=${period}`, { signal }),
    placeholderData: keepPreviousData,
    staleTime: 60_000,
  });
}


export interface LeaderboardReport {
  id: string; gymId: string; gymName: string;
  climber: { userId: string; displayName: string; avatarUrl: string | null };
  reportedBy: { userId: string; displayName: string; avatarUrl: string | null };
  reason: string; status: "PENDING" | "EXCLUDED" | "DISMISSED"; createdAt: string; climberIsExcluded: boolean;
}

export interface ExcludedClimber {
  climber: { userId: string; displayName: string; avatarUrl: string | null };
  excludedAt: string;
}

/** Gym staff flag a climber to BoulderTime; the gym never excludes anyone itself. */
export function useReportClimber(gymId: string) {
  return useMutation({
    mutationFn: ({ userId, reason }: { userId: string; reason: string }) =>
      api.post<LeaderboardReport>(`/api/gyms/${gymId}/leaderboard-reports`, { userId, reason }),
  });
}

export function useLeaderboardReports(includeHandled = false) {
  return useQuery({
    queryKey: ["admin", "leaderboard-reports", includeHandled],
    queryFn: ({ signal }) => api.get<LeaderboardReport[]>(`/api/admin/leaderboard-reports?includeHandled=${includeHandled}`, { signal }),
  });
}

export function useExcludedClimbers() {
  return useQuery({
    queryKey: ["admin", "leaderboard-exclusions"],
    queryFn: ({ signal }) => api.get<ExcludedClimber[]>("/api/admin/leaderboard-exclusions", { signal }),
  });
}

export function useHandleLeaderboardReport() {
  const qc = useQueryClient();
  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ["admin"] });
    qc.invalidateQueries({ queryKey: ["leaderboard"] });
  };
  return {
    handle: useMutation({
      mutationFn: ({ reportId, exclude, note }: { reportId: string; exclude: boolean; note?: string }) =>
        api.post<LeaderboardReport>(`/api/admin/leaderboard-reports/${reportId}/handle`, { exclude, note }),
      onSuccess: invalidate,
    }),
    allow: useMutation({
      mutationFn: (userId: string) => api.delete(`/api/admin/leaderboard-exclusions/${userId}`),
      onSuccess: invalidate,
    }),
  };
}
