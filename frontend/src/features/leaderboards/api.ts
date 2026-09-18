import { keepPreviousData, useQuery } from "@tanstack/react-query";
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
