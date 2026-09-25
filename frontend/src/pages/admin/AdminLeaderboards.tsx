import { useState } from "react";
import { Link } from "react-router-dom";
import { Flag, Trophy } from "lucide-react";
import { useExcludedClimbers, useHandleLeaderboardReport, useLeaderboardReports } from "@/features/leaderboards/api";
import { Avatar } from "@/components/Avatar";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { TextField } from "@/components/TextField";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { formatDate } from "@/lib/format";
import { t } from "@/i18n/i18n";

/**
 * Leaderboard fairness. Gyms report climbers whose results look implausible; only BoulderTime excludes anyone,
 * so a gym never ends up judging its own members.
 */
export function AdminLeaderboards() {
  const reports = useLeaderboardReports();
  const excluded = useExcludedClimbers();
  const { handle, allow } = useHandleLeaderboardReport();
  const toast = useToast();
  const [notes, setNotes] = useState<Record<string, string>>({});
  const onError = (e: unknown) => toast.error(errorMessage(e));

  if (reports.isPending) return <LoadingState label={t("Loading reports")} />;
  if (reports.isError) return <ErrorState error={reports.error} onRetry={() => reports.refetch()} />;

  return (
    <div className="stack">
      <section className="card stack" aria-labelledby="reports-title">
        <h2 id="reports-title" className="section__title"><Flag aria-hidden className="title-icon" /> {t("Reported climbers")}</h2>
        <p className="field__hint">{t("Sent by gym staff. Excluding a climber removes them from every leaderboard; they are told on their profile, without a reason.")}</p>
        {reports.data.length === 0 ? (
          <EmptyState icon={<Flag />} title={t("No open reports")} body={t("All clear.")} />
        ) : (
          <ul className="list">
            {reports.data.map((r) => (
              <li key={r.id} className="list__row list__row--stack">
                <div className="list__main">
                  <Link to={`/users/${r.climber.userId}`} className="list__title">
                    <Avatar name={r.climber.displayName} url={r.climber.avatarUrl} size={28} /> {r.climber.displayName}
                  </Link>
                  <p className="list__sub">{r.gymName} · {t("reported by {name}", { name: r.reportedBy.displayName })} · {formatDate(r.createdAt)}</p>
                  <p className="list__sub">{r.reason}</p>
                  <TextField label={t("Note (optional)")} value={notes[r.id] ?? ""} maxLength={500}
                    onChange={(e) => setNotes((n) => ({ ...n, [r.id]: e.target.value }))} />
                </div>
                <div className="row">
                  <ConfirmButton confirmLabel={t("Exclude?")} loading={handle.isPending}
                    onConfirm={() => handle.mutate({ reportId: r.id, exclude: true, note: notes[r.id] }, {
                      onSuccess: () => toast.success(t("{name} no longer appears in leaderboards", { name: r.climber.displayName })), onError,
                    })}>
                    {t("Exclude from leaderboards")}
                  </ConfirmButton>
                  <Button variant="secondary" loading={handle.isPending}
                    onClick={() => handle.mutate({ reportId: r.id, exclude: false, note: notes[r.id] }, {
                      onSuccess: () => toast.success(t("Report dismissed")), onError,
                    })}>
                    {t("Dismiss")}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="card stack" aria-labelledby="excluded-title">
        <h2 id="excluded-title" className="section__title"><Trophy aria-hidden className="title-icon" /> {t("Excluded from leaderboards")}</h2>
        {excluded.isPending ? <LoadingState label={t("Loading")} />
          : excluded.isError ? <ErrorState error={excluded.error} onRetry={() => excluded.refetch()} />
          : excluded.data.length === 0 ? <p className="list__sub">{t("Nobody is excluded.")}</p>
          : (
            <ul className="list">
              {excluded.data.map((x) => (
                <li key={x.climber.userId} className="list__row list__row--stack">
                  <div className="list__main">
                    <Link to={`/users/${x.climber.userId}`} className="list__title">{x.climber.displayName}</Link>
                    <p className="list__sub">{t("Excluded on {date}", { date: formatDate(x.excludedAt) })}</p>
                  </div>
                  <ConfirmButton confirmLabel={t("Allow?")} loading={allow.isPending}
                    onConfirm={() => allow.mutate(x.climber.userId, {
                      onSuccess: () => toast.success(t("{name} appears in leaderboards again", { name: x.climber.displayName })), onError,
                    })}>
                    {t("Allow again")}
                  </ConfirmButton>
                </li>
              ))}
            </ul>
          )}
      </section>
    </div>
  );
}
