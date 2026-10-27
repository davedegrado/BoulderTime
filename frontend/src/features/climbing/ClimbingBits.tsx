import { Link } from "react-router-dom";
import { Bell, BellRing, Check, MountainSnow, RotateCw, Star, Target } from "lucide-react";
import type { RatingSummary } from "@/features/boulders/api";
import type { HighestGrade, HistoryItem, WeekActivity } from "@/features/climbing/api";
import { ColorGrade, GradeBadge, GradeLine, HoldBadge, HoldIcon } from "@/features/boulders/BoulderBits";
import { holdLabel } from "@/features/boulders/holdColors";
import { Button } from "@/components/Button";
import { formatDate } from "@/lib/format";
import { plural, t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";

export function RatingSummaryText({ rating, compact = false }: { rating: RatingSummary; compact?: boolean }) {
  if (!rating.count || rating.average === null) return compact ? null : <span className="rating-summary rating-summary--empty">{t("No ratings yet")}</span>;
  return (
    <span className="rating-summary" aria-label={t("Rated {average} out of 5 by {count} climbers", { average: rating.average, count: rating.count })}>
      <Star aria-hidden className="rating-summary__star" />
      <strong>{rating.average.toFixed(1)}</strong>
      {!compact && <span className="rating-summary__of">/ 5 · {plural(rating.count, "{count} rating", "{count} ratings")}</span>}
    </span>
  );
}

/** Five large tap targets. Tapping the current rating again clears it. */
export function StarInput({ value, onChange, disabled }: { value: number | null; onChange: (v: number | null) => void; disabled?: boolean }) {
  return (
    <div className="star-input" role="radiogroup" aria-label={t("Your rating")}>
      {[1, 2, 3, 4, 5].map((n) => (
        <button key={n} type="button" role="radio" aria-checked={value === n} aria-label={plural(n, t("{count} star"), t("{count} stars"))}
          className={`star-input__star ${value !== null && n <= value ? "is-on" : ""}`} disabled={disabled}
          onClick={() => onChange(value === n ? null : n)}>
          <Star aria-hidden />
        </button>
      ))}
    </div>
  );
}

export function FollowButton({ following, onToggle, label = t("Follow"), loading }: { following: boolean; onToggle: () => void; label?: string; loading?: boolean }) {
  return (
    <Button variant={following ? "secondary" : "primary"} icon={following ? <BellRing aria-hidden /> : <Bell aria-hidden />} onClick={onToggle}
      aria-pressed={following} loading={loading}>
      {following ? t("Following") : label}
    </Button>
  );
}

/** "✓ Yellow · 6A — Cave · Crimp Factory — Completed 12 Sept 2026 · 3 attempts · Boulder removed" */
export function HistoryRow({ item }: { item: HistoryItem }) {
  const b = item.boulder;
  // A deleted boulder keeps its place in the history it belongs to, but there is no page left to open.
  const gone = b.status === "DELETED";
  const inside = (
    <>
        {b.photoUrl
          ? <img className="history-row__thumb" src={b.photoUrl} alt="" loading="lazy" />
          : <span className="history-row__thumb history-row__thumb--gone" aria-hidden><MountainSnow /></span>}
        <div className="history-row__main">
          <div className="history-row__top">
            {item.completed ? <span className="sent-mark" aria-label={t("Completed")}><Check aria-hidden /></span> : <span className="project-mark">{t("Project")}</span>}
            <GradeLine grades={b.grades} />
            {b.points != null && <span className="history-row__points">{t("+{points} pt", { points: b.points })}</span>}
          </div>
          {/* As on the cards: the name when the boulder has one, never something else in its place. */}
          {b.name && <p className="history-row__name">{b.name}</p>}
          <p className="list__sub">{b.sectorName} · {b.gymName}</p>
          <p className="history-row__meta">
            {item.completed && item.completedAt ? t("Completed {date}", { date: formatDate(item.completedAt) }) : t("Last tried {date}", { date: formatDate(item.updatedAt) })}
            {` · ${plural(item.attempts, "{count} attempt", "{count} attempts")}`}
          </p>
          <div className="history-row__tags">
            <HoldBadge color={b.holdColor} compact />
            {b.status === "REMOVED" && <span className="tag">{t("Boulder removed")}</span>}
            {gone && <span className="tag">{t("No longer at the gym")}</span>}
          </div>
        </div>
    </>
  );
  return (
    <li>
      {gone
        ? <div className="history-row history-row--gone">{inside}</div>
        : <Link to={`/boulders/${b.id}`} className="history-row">{inside}</Link>}
    </li>
  );
}

/**
 * The same entry of a history as HistoryRow, on fewer lines, for a person's profile. The person's state sits on the
 * picture's corner, as on the cards: a check when completed, the target for a project they marked, the attempts for
 * anything else tried. Then the main grade with the others small beside it, and the name when there is one, cut short
 * rather than pushed aside; the holds as a hold of their colour with sector and gym; date and attempts. The points on
 * the right, in the middle.
 */
export function CompactHistoryRow({ item }: { item: HistoryItem }) {
  const b = item.boulder;
  const gone = b.status === "DELETED";
  const [primary, ...others] = b.grades;
  const state = item.completed ? "done" : item.isProject ? "project" : "tried";
  const stateLabel = state === "done" ? t("Completed") : state === "project" ? t("Project") : plural(item.attempts, "{count} attempt", "{count} attempts");
  const inside = (
    <>
      <span className="hrow__pic">
        {b.photoUrl
          ? <img className="hrow__thumb" src={b.photoUrl} alt="" loading="lazy" />
          : <span className="hrow__thumb history-row__thumb--gone" aria-hidden><MountainSnow /></span>}
        <span className={`hrow__state is-${state}`} title={stateLabel}>
          {state === "done" ? <Check aria-hidden /> : state === "project" ? <Target aria-hidden /> : <><RotateCw aria-hidden />{item.attempts}</>}
          <span className="sr-only">{stateLabel}</span>
        </span>
      </span>
      <span className="hrow__main">
        <span className="hrow__top">
          {primary
            ? <GradeBadge grade={primary} size="sm" />
            : <span className="grade grade--text grade--ungraded grade--sm">{t("Ungraded")}</span>}
          {others.length > 0 && <span className="hrow__grades">{others.map((g) => dataLabel(g.label)).join(" · ")}</span>}
          {b.name && <span className="hrow__name">{b.name}</span>}
        </span>
        <span className="hrow__where">
          <HoldIcon color={b.holdColor} />
          <span className="sr-only">{holdLabel(b.holdColor)}, </span>
          <span className="hrow__place">{b.sectorName} · {b.gymName}</span>
        </span>
        <span className="hrow__meta">
          {item.completed && item.completedAt ? formatDate(item.completedAt) : t("Last tried {date}", { date: formatDate(item.updatedAt) })}
          {` · ${plural(item.attempts, "{count} attempt", "{count} attempts")}`}
          {b.status === "REMOVED" && ` · ${t("Boulder removed")}`}
          {gone && ` · ${t("No longer at the gym")}`}
        </span>
      </span>
      {b.points != null && <span className="hrow__points">{t("+{points} pt", { points: b.points })}</span>}
    </>
  );
  return (
    <li>
      {gone ? <div className="hrow hrow--gone">{inside}</div> : <Link to={`/boulders/${b.id}`} className="hrow">{inside}</Link>}
    </li>
  );
}

export function WeeklyChart({ weeks }: { weeks: WeekActivity[] }) {
  const max = Math.max(1, ...weeks.map((w) => w.completed));
  const total = weeks.reduce((n, w) => n + w.completed, 0);
  return (
    <figure className="weekly">
      <div className="weekly__bars" role="img" aria-label={t("{total} completions in the last {weeks} weeks", { total, weeks: weeks.length })}>
        {weeks.map((w, i) => (
          <div key={w.weekStart} className="weekly__col" title={t("Week of {date}: {count}", { date: formatDate(w.weekStart, { day: "numeric", month: "short" }), count: w.completed })}>
            <span className={`weekly__bar ${i === weeks.length - 1 ? "is-current" : ""}`} style={{ height: `${Math.max(4, (w.completed / max) * 100)}%` }} />
          </div>
        ))}
      </div>
      <figcaption className="weekly__caption">
        <span>{formatDate(weeks[0]?.weekStart ?? new Date().toISOString(), { day: "numeric", month: "short" })}</span>
        <span>{t("This week")}</span>
      </figcaption>
    </figure>
  );
}

export function HighestGrades({ grades }: { grades: HighestGrade[] }) {
  if (grades.length === 0) return <p className="list__sub">{t("Complete a boulder to see your highest grades.")}</p>;
  return (
    <ul className="highest">
      {grades.map((g) => (
        <li key={g.gradeSystemId} className="highest__item">
          {g.systemType === "COLOR" && g.colorHex
            ? <ColorGrade label={g.label} colorHex={g.colorHex} colorHex2={g.colorHex2} />
            : <span className="grade grade--text grade--md">{dataLabel(g.label)}</span>}
          <span className="list__sub">{dataLabel(g.systemName)}{g.systemType === "COLOR" || g.systemType === "CUSTOM" ? ` · ${g.gymName}` : ""}</span>
        </li>
      ))}
    </ul>
  );
}
