import { Link } from "react-router-dom";
import { Bell, BellRing, Check, Star } from "lucide-react";
import type { RatingSummary } from "@/features/boulders/api";
import type { HighestGrade, HistoryItem, WeekActivity } from "@/features/climbing/api";
import { GradeLine, HoldBadge } from "@/features/boulders/BoulderBits";
import { inkOn } from "@/features/boulders/holdColors";
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
  return (
    <li>
      <Link to={`/boulders/${b.id}`} className="history-row">
        <img className="history-row__thumb" src={b.photoUrl} alt="" loading="lazy" />
        <div className="history-row__main">
          <div className="history-row__top">
            {item.completed ? <span className="sent-mark" aria-label={t("Completed")}><Check aria-hidden /></span> : <span className="project-mark">{t("Project")}</span>}
            <GradeLine grades={b.grades} />
          </div>
          <p className="list__sub">{b.sectorName} · {b.gymName}</p>
          <p className="history-row__meta">
            {item.completed && item.completedAt ? t("Completed {date}", { date: formatDate(item.completedAt) }) : t("Last tried {date}", { date: formatDate(item.updatedAt) })}
            {` · ${plural(item.attempts, t("{count} attempt"), t("{count} attempts"))}`}
          </p>
          <div className="history-row__tags">
            <HoldBadge color={b.holdColor} compact />
            {b.status === "REMOVED" && <span className="tag">{t("Boulder removed")}</span>}
          </div>
        </div>
      </Link>
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
            ? <span className="grade grade--color grade--md" style={{ background: g.colorHex, color: inkOn(g.colorHex) }}>{dataLabel(g.label)}</span>
            : <span className="grade grade--text grade--md">{dataLabel(g.label)}</span>}
          <span className="list__sub">{dataLabel(g.systemName)}{g.systemType === "COLOR" || g.systemType === "CUSTOM" ? ` · ${g.gymName}` : ""}</span>
        </li>
      ))}
    </ul>
  );
}
