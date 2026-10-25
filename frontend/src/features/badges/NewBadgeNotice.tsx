import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { X } from "lucide-react";
import { api } from "@/lib/api";
import { plural, t } from "@/i18n/i18n";
import { BadgeArt } from "@/features/badges/BadgeArt";
import { badgeKeys, type EarnedBadge } from "@/features/badges/api";

const newKey = ["badges", "new"] as const;

/**
 * A quiet card above the tab bar when the signed-in person has earned badges they haven't been shown (ADR-069): one
 * badge by name, several by number. Opening or closing it marks them seen, so it comes once. A server without badges
 * answers 404 and nothing shows.
 */
export function NewBadgeNotice({ userId }: { userId: string }) {
  const qc = useQueryClient();
  const fresh = useQuery({
    queryKey: newKey,
    queryFn: ({ signal }) => api.get<EarnedBadge[]>("/api/users/me/badges/new", { signal }),
    retry: false,
    staleTime: 60_000,
  });
  const seen = useMutation({
    mutationFn: () => api.post<void>("/api/users/me/badges/seen"),
    onMutate: () => qc.setQueryData<EarnedBadge[]>(newKey, []),
    onSettled: () => qc.invalidateQueries({ queryKey: badgeKeys.of(userId) }),
  });
  const badges = fresh.data ?? [];
  const first = badges[0];
  if (!first) return null;
  return (
    <div className="badge-notice" role="status">
      <Link to={`/users/${userId}/badges`} className="badge-notice__link" onClick={() => seen.mutate()}>
        <BadgeArt art={first.art} size={44} />
        <span className="badge-notice__text">
          <strong>{badges.length === 1 ? t("New badge: {name}", { name: first.name }) : plural(badges.length, "1 new badge", "{count} new badges")}</strong>
          <span>{badges.length === 1 ? first.description : t("See them on your profile.")}</span>
        </span>
      </Link>
      <button type="button" className="icon-btn badge-notice__close" aria-label={t("Close")} onClick={() => seen.mutate()}><X aria-hidden /></button>
    </div>
  );
}
