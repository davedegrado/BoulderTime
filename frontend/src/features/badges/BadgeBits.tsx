import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { ChevronRight, Medal } from "lucide-react";
import { BadgeArt } from "@/features/badges/BadgeArt";
import { useUserBadges, type BadgeCategory, type EarnedBadge } from "@/features/badges/api";
import { formatDate } from "@/lib/format";
import { activeLanguage, plural, t } from "@/i18n/i18n";

export const CATEGORY_ORDER: BadgeCategory[] = ["SPECIAL", "COMPETITION", "PROGRESSION", "CONSISTENCY", "EXPERIENCE", "EXPLORATION", "COMMUNITY"];

export function categoryLabel(c: BadgeCategory): string {
  switch (c) {
    case "PROGRESSION": return t("Progression");
    case "CONSISTENCY": return t("Consistency");
    case "EXPERIENCE": return t("Experience");
    case "EXPLORATION": return t("Exploration");
    case "COMMUNITY": return t("Community");
    case "COMPETITION": return t("Competitive");
    default: return t("Special");
  }
}

/** "Crimp Factory · ottobre 2026": where and when a title was won; nothing for other badges. */
export function badgeContext(b: EarnedBadge): string | null {
  const when = b.period
    ? b.period.kind === "YEAR"
      ? String(new Date(b.period.start).getUTCFullYear())
      : new Date(b.period.start).toLocaleDateString(activeLanguage(), { month: "long", year: "numeric", timeZone: "UTC" })
    : null;
  const parts = [b.gym?.name, when].filter(Boolean);
  return parts.length ? parts.join(" · ") : null;
}

/**
 * The badges a person put in the foreground, on their profile's overview, with a way to all of them. Nothing at all
 * for someone with no badges; no empty slots when fewer than three are chosen. Privacy is the server's: a private
 * profile's badges don't come for whoever may not see its content.
 */
export function FeaturedBadges({ userId, isMe }: { userId: string; isMe: boolean }) {
  const badges = useUserBadges(userId);
  const data = badges.data;
  if (!data || data.badges.length === 0) return null;
  const byId = new Map(data.badges.map((b) => [b.id, b]));
  // A featured badge no longer in the list (taken out of the catalogue) is simply skipped.
  const featured = data.featured.map((id) => byId.get(id)).filter((b): b is EarnedBadge => Boolean(b));
  return (
    <section className="profile-block" aria-labelledby="badges-title">
      <div className="profile-block__head">
        <h2 id="badges-title" className="profile-block__title">{t("Badges")}</h2>
        <Link to={`/users/${userId}/badges`} className="section__link">{t("See all ({count})", { count: data.badges.length })}</Link>
      </div>
      {featured.length > 0 ? (
        <ul className="badge-featured">
          {featured.map((b) => (
            <li key={b.id} className="badge-featured__item">
              <BadgeArt art={b.art} size={64} />
              <span className="badge-featured__name">{b.name}</span>
              {badgeContext(b) && <span className="badge-featured__context">{badgeContext(b)}</span>}
            </li>
          ))}
        </ul>
      ) : isMe ? (
        <Link to={`/users/${userId}/badges`} className="badge-featured__empty">
          <Medal aria-hidden />
          <span>{plural(data.badges.length, "You have 1 badge: choose up to three to show here.", "You have {count} badges: choose up to three to show here.")}</span>
          <ChevronRight aria-hidden />
        </Link>
      ) : null}
    </section>
  );
}

/** One earned badge in the full list: picture, name, what it is for, when, and where for a title. */
export function BadgeRow({ badge: b, action }: { badge: EarnedBadge; action?: ReactNode }) {
  const context = badgeContext(b);
  return (
    <li className="badge-row">
      <BadgeArt art={b.art} size={56} />
      <div className="badge-row__text">
        <p className="badge-row__name">{b.name}</p>
        <p className="badge-row__desc">{b.description}</p>
        <p className="badge-row__meta">{[context, t("Earned on {date}", { date: formatDate(b.earnedAt) })].filter(Boolean).join(" · ")}</p>
      </div>
      {action}
    </li>
  );
}
