import { useState } from "react";
import { Link } from "react-router-dom";
import { Info, Trophy } from "lucide-react";
import { useLeaderboard, metricLabel, periodLabel, periodShortLabel, type Leaderboard, type LeaderboardEntry, type LeaderboardMetric, type LeaderboardPeriod } from "@/features/leaderboards/api";
import { useAuth } from "@/auth/AuthProvider";
import { Avatar } from "@/components/Avatar";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { ColorGrade } from "@/features/boulders/BoulderBits";
import { plural, t } from "@/i18n/i18n";
import { dataLabel } from "@/i18n/data";
import { Flag } from "lucide-react";
import { useCurrentUser, useLeaderboardVisibility } from "@/features/users/api";
import { useReportClimber } from "@/features/leaderboards/api";
import { TextField } from "@/components/TextField";
import { Button } from "@/components/Button";
import { Toggle } from "@/features/notifications/NotificationBits";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";

const METRICS: LeaderboardMetric[] = ["POINTS", "COMPLETED", "HIGHEST"];
const PERIODS: LeaderboardPeriod[] = ["WEEK", "MONTH", "YEAR", "ALL"];

function Value({ entry, metric }: { entry: LeaderboardEntry; metric: LeaderboardMetric }) {
  if (metric === "HIGHEST" && entry.highest) {
    const h = entry.highest;
    return h.colorHex
      ? <ColorGrade label={h.label} colorHex={h.colorHex} colorHex2={h.colorHex2} size="sm" />
      : <span className="grade grade--text grade--sm">{dataLabel(h.label)}</span>;
  }
  if (metric === "COMPLETED") return <span className="board__value">{entry.completed} <small>{plural(entry.completed, "send", "sends", { count: entry.completed }).replace(String(entry.completed), "").trim()}</small></span>;
  return <span className="board__value">{entry.points} <small>{t("pts")}</small></span>;
}

function Row({ entry, metric, onReport }: { entry: LeaderboardEntry; metric: LeaderboardMetric; onReport?: (entry: LeaderboardEntry) => void }) {
  return (
    <li className={`board__row ${entry.isViewer ? "is-viewer" : ""} ${entry.position <= 3 ? `is-top is-top-${entry.position}` : ""}`}>
      <span className="board__position" aria-label={t("Position {position}", { position: entry.position })}>{entry.position}</span>
      <Link to={`/users/${entry.climber.userId}`} className="board__climber">
        <Avatar name={entry.climber.displayName} url={entry.climber.avatarUrl} size={36} />
        <span className="board__name">{entry.climber.displayName}{entry.isViewer && <span className="list__you"> · {t("you")}</span>}</span>
      </Link>
      <Value entry={entry} metric={metric} />
      {onReport && !entry.isViewer && (
        <button type="button" className="board__report" onClick={() => onReport(entry)}
          aria-label={t("Report {name} to BoulderTime", { name: entry.climber.displayName })}>
          <Flag aria-hidden />
        </button>
      )}
    </li>
  );
}

/** Per-gym leaderboard. Rankings are computed live; grades are only compared within this gym's scales. */
/** Asks the reporting staff member what looks wrong. Only BoulderTime sees it. */
function ReportPanel({ entry, busy, onCancel, onSend }: {
  entry: LeaderboardEntry; busy: boolean; onCancel: () => void; onSend: (reason: string) => void;
}) {
  const [reason, setReason] = useState("");
  return (
    <section className="card stack" aria-label={t("Report {name} to BoulderTime", { name: entry.climber.displayName })}>
      <p className="list__title">{t("Report {name} to BoulderTime", { name: entry.climber.displayName })}</p>
      <p className="field__hint">{t("Only BoulderTime sees this. The climber isn't told, and nothing changes in your gym.")}</p>
      <TextField label={t("What looks wrong?")} value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500}
        hint={t("For example: thirty sends in ten minutes, or grades nobody at the gym climbs.")} />
      <div className="row">
        <Button disabled={reason.trim().length === 0} loading={busy} onClick={() => onSend(reason.trim())}>{t("Send report")}</Button>
        <Button variant="ghost" onClick={onCancel}>{t("Cancel")}</Button>
      </div>
    </section>
  );
}

/**
 * How points work, in the reader's language, from the numbers the server sends (never from its English sentence, which
 * stays only for older app builds).
 */
export function scoringText(b: Leaderboard): string {
  const vars = { system: b.gradeSystemName ? dataLabel(b.gradeSystemName) : t("primary"), min: b.scoring?.minPoints ?? 10, max: b.scoring?.maxPoints ?? 100 };
  const rule = t("Each completed boulder scores by its grade on the {system} scale: the easiest grade is worth {min} points and the hardest {max}, evenly spaced in between. A retired grade keeps the points it had.", vars);
  return b.scoring?.customised ? `${rule} ${t("This gym set some points by hand: each boulder's card shows what it is worth.")}` : rule;
}

export function LeaderboardTab({ gymId }: { gymId: string }) {
  const { session } = useAuth();
  const [metric, setMetric] = useState<LeaderboardMetric>("POINTS");
  const [period, setPeriod] = useState<LeaderboardPeriod>("MONTH");
  const [explain, setExplain] = useState(false);
  const board = useLeaderboard(gymId, metric, period);
  // Staff of this gym can flag results that look implausible. BoulderTime decides; the gym never excludes anyone.
  const me = useCurrentUser();
  const isStaff = (me.data?.staffGyms ?? []).some((g) => g.gymId === gymId) || (me.data?.isPlatformAdmin ?? false);
  const [reporting, setReporting] = useState<LeaderboardEntry | null>(null);
  const report = useReportClimber(gymId);
  const toast = useToast();
  const visibility = useLeaderboardVisibility();
  // Leaderboards are opt-in: someone signed in and not taking part is told so, with the switch right here.
  const outOfBoard = !!session && !!me.data && me.data.leaderboardOptOut && !me.data.leaderboardExcluded;

  return (
    <div className="stack">
      {outOfBoard && (
        // The same switch as in the profile, where the person is looking at the leaderboard: one tap to take part.
        <div className="card leaderboard-optin">
          <Toggle
            label={t("You're not on the leaderboard")}
            description={t("Turn it on to appear by name. Your sends count for you either way.")}
            checked={false}
            disabled={visibility.isPending}
            onChange={() => visibility.mutate(false, {
              onSuccess: () => toast.success(t("You appear in leaderboards now")),
              onError: (e) => toast.error(errorMessage(e)),
            })} />
        </div>
      )}
      <div className="chips" role="radiogroup" aria-label={t("Ranking by")}>
        {METRICS.map((m) => <button key={m} role="radio" aria-checked={metric === m} className="chip" onClick={() => setMetric(m)}>{metricLabel[m]}</button>)}
      </div>
      <div className="segmented" role="radiogroup" aria-label={t("Period")}>
        {PERIODS.map((p) => <button key={p} role="radio" aria-checked={period === p} className="segmented__option" onClick={() => setPeriod(p)}>{periodShortLabel[p]}</button>)}
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
                  {metric === "POINTS" ? scoringText(b) : t("Highest completed grade in the gym's {system} scale.", { system: b.gradeSystemName ? dataLabel(b.gradeSystemName) : t("primary") })}
                </p>
              )}
              {b.entries.length === 0 ? (
                <EmptyState icon={<Trophy />} title={t("No sends yet for this period")}
                  body={session ? t("Mark boulders as completed to get on the board.") : t("Sign in and log your sends to get on the board.")} />
              ) : (
                <ol className="board" aria-label={t("{metric} leaderboard", { metric: metricLabel[metric] })}>
                  {b.entries.map((e) => <Row key={e.climber.userId} entry={e} metric={metric} onReport={isStaff ? setReporting : undefined} />)}
                </ol>
              )}
              {viewerOutside && b.viewer && (
                <ol className="board board--viewer" aria-label={t("Your position")}><Row entry={b.viewer} metric={metric} /></ol>
              )}
              {reporting && (
                <ReportPanel entry={reporting} busy={report.isPending} onCancel={() => setReporting(null)}
                  onSend={(reason) => report.mutate({ userId: reporting.climber.userId, reason }, {
                    onSuccess: () => { toast.success(t("Sent to BoulderTime. We'll look into it.")); setReporting(null); },
                    onError: (e) => toast.error(errorMessage(e)),
                  })} />
              )}
            </>
          );
        })()}
    </div>
  );
}
