import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthProvider";
import type { GymRole } from "@/lib/format";
import { t } from "@/i18n/i18n";
import type { ProfileSection } from "@/features/climbing/api";

export interface StaffGym {
  gymId: string;
  slug: string;
  name: string;
  city: string;
  logoUrl: string | null;
  role: GymRole;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  isPlatformAdmin: boolean;
  createdAt: string;
  staffGyms: StaffGym[];
  pendingInvitations: number;
  language: "it" | "en";
  /** The climber chose to stay out of leaderboards. */
  leaderboardOptOut: boolean;
  /** BoulderTime excluded them; shown only to themselves, so a missing name doesn't read as a broken app. */
  leaderboardExcluded: boolean;
  /** The version of the terms this person accepted, if any. */
  acceptedLegalVersion: string | null;
  /** True when the terms changed, or were never accepted: the app asks before letting them in. */
  legalAcceptanceNeeded: boolean;
  /** BoulderTime suspended this account: the app shows why and nothing else (ADR-037). */
  isSuspended?: boolean;
  /** Declared being at least 14 (ADR-041); the terms screen asks whoever hasn't. */
  minimumAgeConfirmed?: boolean;
  /** Only they see the content of their profile (ADR-061). Absent from older servers. */
  profilePrivate?: boolean;
  /** What they wrote about themselves, shown under their name. Absent from older servers. */
  bio?: string | null;
}

export interface UpdateProfileInput {
  displayName: string;
  language?: "it" | "en";
  /** Left out, the bio stays as it is; blank clears it. */
  bio?: string;
}

export const userKeys = {
  me: ["users", "me"] as const,
};

/** The signed-in user's BoulderTime profile, staff memberships and pending invitation count. */
export function useCurrentUser() {
  const { session } = useAuth();
  return useQuery({
    queryKey: userKeys.me,
    queryFn: ({ signal }) => api.get<CurrentUser>("/api/users/me", { signal }),
    enabled: !!session,
    staleTime: 60_000,
  });
}

export function useUpdateProfile() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateProfileInput) => api.patch<CurrentUser>("/api/users/me", input),
    onSuccess: (user) => qc.setQueryData(userKeys.me, user),
  });
}


/** Hides or shows the climber in every leaderboard. */
export function useLeaderboardVisibility() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (hidden: boolean) => api.put("/api/users/me/leaderboard-visibility", { hidden }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: userKeys.me });
      qc.invalidateQueries({ queryKey: ["leaderboard"] });
    },
  });
}


/** Public or private profile content (ADR-061), independent of the leaderboards. */
export function useProfilePrivacy() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (isPrivate: boolean) => api.put("/api/users/me/profile-privacy", { private: isPrivate }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: userKeys.me });
      qc.invalidateQueries({ queryKey: ["climbing", "profile"] });
      qc.invalidateQueries({ queryKey: ["climbing", "history"] });
    },
  });
}

/** Saves the arrangement of the profile's sections, or (null) goes back to the default one. */
export function useSaveProfileSections() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (sections: ProfileSection[] | null) => sections
      ? api.put<ProfileSection[]>("/api/users/me/profile-sections", { sections })
      : api.delete<ProfileSection[]>("/api/users/me/profile-sections"),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["climbing", "profile"] }),
  });
}

export interface BlockedPerson { userId: string; displayName: string; avatarUrl: string | null; blockedAt: string }

/** People you have chosen not to see. Personal and immediate: no moderator is involved. */
export function useBlockedPeople() {
  return useQuery({
    queryKey: ["users", "me", "blocks"],
    queryFn: ({ signal }) => api.get<BlockedPerson[]>("/api/users/me/blocks", { signal }),
  });
}

export function useBlockPerson() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ userId, blocked, reason }: { userId: string; blocked: boolean; reason?: string }) =>
      blocked ? api.put(`/api/users/me/blocks/${userId}`, { reason }) : api.delete(`/api/users/me/blocks/${userId}`),
    onSuccess: () => {
      // Comments and videos are filtered server-side, so everything the person appears in has to be refetched.
      qc.invalidateQueries();
    },
  });
}


export type PersonReportReason = "HARASSMENT" | "SPAM" | "IMPERSONATION" | "INAPPROPRIATE_PROFILE" | "OTHER";

/** Read at render time, so the labels follow the language in use. */
export const personReportReasons = (): { value: PersonReportReason; label: string }[] => [
  { value: "HARASSMENT", label: t("Harassment or insults") },
  { value: "SPAM", label: t("Spam or advertising") },
  { value: "IMPERSONATION", label: t("Pretending to be someone else") },
  { value: "INAPPROPRIATE_PROFILE", label: t("Inappropriate name or photo") },
  { value: "OTHER", label: t("Other") },
];

/**
 * Reports a person — not a single comment — to BoulderTime. Content reports go to the gym; a person spans every gym,
 * so only BoulderTime decides, and the person is never told who reported them.
 */
export function useReportPerson() {
  return useMutation({
    mutationFn: ({ userId, reason, description }: { userId: string; reason: PersonReportReason; description?: string }) =>
      api.post(`/api/users/${userId}/reports`, { reason, description }),
  });
}
