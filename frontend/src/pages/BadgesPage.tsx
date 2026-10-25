import { useParams } from "react-router-dom";
import { Medal, Pin, PinOff } from "lucide-react";
import { DetailHeader } from "@/components/DetailHeader";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { useProfile } from "@/features/climbing/api";
import { MAX_FEATURED, useSetFeaturedBadges, useUserBadges } from "@/features/badges/api";
import { BadgeRow, CATEGORY_ORDER, categoryLabel } from "@/features/badges/BadgeBits";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

/**
 * Every badge a person earned, by category (ADR-069). Only what was earned: no wall of locked badges. On one's own,
 * each can be put in the foreground of the profile or taken out of it, up to three.
 */
export function BadgesPage() {
  const { id } = useParams();
  const profile = useProfile(id);
  const badges = useUserBadges(id);
  const isMe = profile.data?.isMe ?? false;
  const setFeatured = useSetFeaturedBadges(id ?? "");
  const toast = useToast();

  const head = <DetailHeader title={profile.data ? t("{name}'s badges", { name: profile.data.displayName }) : t("Badges")} titleAlways backTo={`/users/${id}`} />;
  if (badges.isPending) return <div className="page page--flush">{head}<LoadingState label={t("Loading badges")} /></div>;
  if (badges.isError) return <div className="page page--flush">{head}<div className="page__pad"><ErrorState error={badges.error} onRetry={() => badges.refetch()} /></div></div>;

  const { featured, badges: list } = badges.data;
  const toggle = (badgeId: string) => {
    const on = featured.includes(badgeId);
    if (!on && featured.length >= MAX_FEATURED) { toast.error(t("Up to three badges in the foreground: take one out first.")); return; }
    setFeatured.mutate(on ? featured.filter((x) => x !== badgeId) : [...featured, badgeId], { onError: (e) => toast.error(errorMessage(e)) });
  };

  return (
    <div className="page page--flush badges-page">
      {head}
      <div className="page__pad stack">
        {list.length === 0 ? (
          <EmptyState icon={<Medal />} title={isMe ? t("No badges yet") : t("No badges to show")}
            body={isMe ? t("Complete boulders, come back on other days, try new gyms: badges come by themselves.") : undefined} />
        ) : (
          <>
            {isMe && <p className="field__hint">{t("{count} of {max} in the foreground of your profile.", { count: featured.length, max: MAX_FEATURED })}</p>}
            {CATEGORY_ORDER.map((c) => {
              const items = list.filter((b) => b.category === c);
              if (items.length === 0) return null;
              return (
                <section key={c} className="profile-block" aria-labelledby={`badges-${c}`}>
                  <h2 id={`badges-${c}`} className="profile-block__title">{categoryLabel(c)}</h2>
                  <ul className="badge-list">
                    {items.map((b) => {
                      const on = featured.includes(b.id);
                      return (
                        <BadgeRow key={b.id} badge={b} action={isMe ? (
                          <button type="button" className={`icon-btn badge-pin ${on ? "is-on" : ""}`} aria-pressed={on}
                            aria-label={on ? t("Take {name} out of the foreground", { name: b.name }) : t("Put {name} in the foreground", { name: b.name })}
                            onClick={() => toggle(b.id)}>
                            {on ? <PinOff aria-hidden /> : <Pin aria-hidden />}
                          </button>
                        ) : undefined} />
                      );
                    })}
                  </ul>
                </section>
              );
            })}
          </>
        )}
      </div>
    </div>
  );
}
