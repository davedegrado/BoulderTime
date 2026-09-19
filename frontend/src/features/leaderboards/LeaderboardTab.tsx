import { useState } from "react";
import { Link } from "react-router-dom";
import { Info, Trophy } from "lucide-react";
import { useLeaderboard, metricLabel, periodLabel, type LeaderboardEntry, type LeaderboardMetric, type LeaderboardPeriod } from "@/features/leaderboards/api";
import { useAuth } from "@/auth/AuthProvider";
import { Avatar } from "@/components/Avatar";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { inkOn } from "@/features/boulders/holdColors";
import { plural, t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";

const METRICS: LeaderboardMetric[] = ["POINTS", "COMPLETED", "HIGHEST"];
const PERIODS: LeaderboardPeriod[] = ["WEEK", "MONTH", "YEAR", "ALL"];

function Value({ entry, metric }: { entry: LeaderboardEntry; metric: LeaderboardMetric }) {
  if (metric === "HIGHEST" && entry.highest) {
    const h = entry.highest;
    return h.colorHex
      ? <span className="grade grade--color grade--sm" style={{ background: h.colorHex, color: inkOn(h.colorHex) }}>{dataLabel(h.label)}</span>
      : <span className="grade grade--text grade--sm">{dataLabel(h.label)}</span>;
  }
  if (metric === "COMPLETED") return <span className="board__value">{entry.completed} <small>{plural(entry.completed, "send", "sends", { count: entry.completed }).replace(String(entry.completed), "").trim()}</small></span>;
  return <span className="board__value">{entry.points} <small>{t("pts")}</small></span>;
}

function Row({ entry, metric }: { entry: LeaderboardEntry; metric: LeaderboardMetric }) {
  return (
    <li className={`board__row ${entry.isViewer ? "is-viewer" : ""} ${entry.position <= 3 ? `is-top is-top-${entry.position}` : ""}`}>
      <span className="board__position" aria-label={t("Position {position}", { position: entry.position })}>{entry.position}</span>
      <Link to={`/users/${entry.climber.userId}`} className="board__climber">
        <Avatar name={entry.climber.displayName} url={entry.climber.avatarUrl} size={36} />
        <span className="board__name">{entry.climber.displayName}{entry.isViewer && <span className="list__you"> · you</span>}</span>
      </Link>
      <Value entry={entry} metric={metric} />
    </li>
  );
}

/** Per-gym leaderboard. Rankings are computed live; grades are only compared within this gym's scales. */
export function LeaderboardTab({ gymId }: { gymId: string }) {
  const { session } = useAuth();
  const [metric, setMetric] = useState<LeaderboardMetric>("POINTS");
  const [period, setPeriod] = useState<LeaderboardPeriod>("MONTH");
  const [explain, setExplain] = useState(false);
  const board = useLeaderboard(gymId, metric, period);

  return (
    <div className="stack">
      <div className="chips" role="radiogroup" aria-label={t("Ranking by")}>
        {METRICS.map((m) => <button key={m} role="radio" aria-checked={metric === m} className="chip" onClick={() => setMetric(m)}>{metricLabel[m]}</button>)}
      </div>
      <div className="chips chips--light" role="radiogroup" aria-label={t("Period")}>
        {PERIODS.map((p) => <button key={p} role="radio" aria-checked={period === p} className="chip" onClick={() => setPeriod(p)}>{periodLabel[p]}</button>)}
      </div>

      {board.isPending ? <LoadingState label={t("Loading leaderboard")} />
        : board.isError ? <ErrorState error={board.error} onRetry={() => board.refetch()} />
        : (() => {
          const b = board.data;
          const viewerOutside = b.viewer && !b.entries.some((e) => e.isViewer);
          return (
            <>
              <div className="section__row">
                <p className="section__meta">{plural(b.climbers, "{count} climber", "{count} climbers")} · {periodLabel[b.period].toLowerCase()}</p>
                {metric !== "COMPLETED" && (
                  <button type="button" className="text-btn" onClick={() => setExplain((v) => !v)} aria-expanded={explain}><Info aria-hidden /> {t("How it works")}</button>
                )}
              </div>
              {explain && metric !== "COMPLETED" && (
                <p className="notice notice--inline">
                  {metric === "POINTS" ? b.scoringExplanation : t("Highest completed grade in the gym's {system} scale.", { system: b.gradeSystemName ? dataLabel(b.gradeSystemName) : t("primary") })}
                </p>
              )}
              {b.entries.length === 0 ? (
                <EmptyState icon={<Trophy />} title={t("No sends yet for this period")}
                  body={session ? t("Mark boulders as completed to get on the board.") : t("Sign in and log your sends to get on the board.")} />
              ) : (
                <ol className="board" aria-label={t("{metric} leaderboard", { metric: metricLabel[metric] })}>
                  {b.entries.map((e) => <Row key={e.climber.userId} entry={e} metric={metric} />)}
                </ol>
              )}
              {viewerOutside && b.viewer && (
                <ol className="board board--viewer" aria-label={t("Your position")}><Row entry={b.viewer} metric={metric} /></ol>
              )}
            </>
          );
        })()}
    </div>
  );
}
