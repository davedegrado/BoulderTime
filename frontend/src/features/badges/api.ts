import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api";

/**
 * Badges (ADR-069). The server decides who has which and says, for each, its stable id, its category, its name and
 * description in the reader's language and the key of its picture; the app only shows them.
 */
export type BadgeCategory = "PROGRESSION" | "CONSISTENCY" | "EXPERIENCE" | "EXPLORATION" | "COMMUNITY" | "COMPETITION" | "SPECIAL";

export interface EarnedBadge {
  /** This award: a competition title can be won more than once (another month, another gym). */
  id: string;
  /** The badge's stable id, e.g. "PROGRESSION_COMPLETIONS_10"; never a file name. */
  badgeId: string;
  category: BadgeCategory;
  name: string;
  description: string;
  /** Key of the picture (see `badgeArtUrl`); unknown or missing keys show the generic picture. */
  art: string;
  earnedAt: string;
  /** For titles won at a gym. */
  gym?: { id: string; name: string; slug: string } | null;
  /** For titles of a period: the month or the year, from its first day. */
  period?: { kind: "MONTH" | "YEAR"; start: string } | null;
}

export interface UserBadges {
  /** The awards in the foreground, in their order (at most three). */
  featured: string[];
  badges: EarnedBadge[];
}

export const MAX_FEATURED = 3;

export const badgeKeys = { of: (userId: string) => ["badges", userId] as const };

/** A person's badges; nothing for a private profile the viewer may not see, as for the rest of its content. */
export function useUserBadges(userId: string | undefined, enabled = true) {
  return useQuery({
    queryKey: badgeKeys.of(userId ?? ""),
    queryFn: ({ signal }) => api.get<UserBadges>(`/api/users/${userId}/badges`, { signal }),
    enabled: Boolean(userId) && enabled,
    retry: false,
  });
}

/** The badges to put in the foreground: the server checks they are earned, distinct and at most three. */
export function useSetFeaturedBadges(userId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (ids: string[]) => api.put<UserBadges>("/api/users/me/badges/featured", { ids }),
    onMutate: (ids) => qc.setQueryData<UserBadges>(badgeKeys.of(userId), (b) => (b ? { ...b, featured: ids } : b)),
    onSuccess: (b) => qc.setQueryData(badgeKeys.of(userId), b),
    onError: () => qc.invalidateQueries({ queryKey: badgeKeys.of(userId) }),
  });
}
