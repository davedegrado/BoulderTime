import { useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { Mountain } from "lucide-react";
import { useCurrentUser } from "@/features/users/api";
import { useHistory, useProfile, type HistoryFilter } from "@/features/climbing/api";
import { HighestGrades, HistoryRow, WeeklyChart } from "@/features/climbing/ClimbingBits";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { t } from "@/i18n/i18n";

export function ActivityPage() {
  const me = useCurrentUser();
  if (me.isPending) return <LoadingState />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  return <ClimbingActivity userId={me.data.id} title={t("Your climbing")} />;
}

/** Stats, highest grades, weekly activity and full history. Used for your own Activity and for public profiles. */
export function ClimbingActivity({ userId, title, header }: { userId: string; title?: string; header?: ReactNode }) {
  const profile = useProfile(userId);
  const [filter, setFilter] = useState<HistoryFilter>("ALL");
  const history = useHistory(userId, filter);

  if (profile.isPending) return <LoadingState label={t("Loading activity")} />;
  if (profile.isError) return <ErrorState error={profile.error} onRetry={() => profile.refetch()} />;
  const p = profile.data;
  const items = history.data?.pages.flatMap((pg) => pg.items) ?? [];

  return (
    <div className="page">
      {header ?? <header className="page__header"><h1 className="page__title">{title}</h1></header>}

      <div className="stats">
        <div className="stat"><span className="stat__value">{p.stats.completed}</span><span className="stat__label">{t("Completed")}</span></div>
        <div className="stat"><span className="stat__value">{p.stats.projects}</span><span className="stat__label">{t("Projects")}</span></div>
        <div className="stat"><span className="stat__value">{p.stats.completedThisMonth}</span><span className="stat__label">{t("Sent this month")}</span></div>
        <div className="stat"><span className="stat__value">{p.stats.totalAttempts}</span><span className="stat__label">{t("Attempts")}</span></div>
      </div>

      <section className="section card" aria-labelledby="weekly-title">
        <h2 id="weekly-title" className="section__title">{t("Last 12 weeks")}</h2>
        <WeeklyChart weeks={p.weekly} />
      </section>

      <section className="section card" aria-labelledby="highest-title">
        <h2 id="highest-title" className="section__title">{t("Highest grades")}</h2>
        <HighestGrades grades={p.highestGrades} />
      </section>

      <section className="section" aria-labelledby="history-title">
        <h2 id="history-title" className="section__title">{t("History")}</h2>
        <div className="chips" role="radiogroup" aria-label={t("Filter history")}>
          {(["ALL", "COMPLETED", "PROJECTS"] as HistoryFilter[]).map((f) => (
            <button key={f} role="radio" aria-checked={filter === f} className="chip" onClick={() => setFilter(f)}>
              {f === "ALL" ? "All" : f === "COMPLETED" ? "Completed" : "Projects"}
            </button>
          ))}
        </div>
        {history.isPending ? <LoadingState label={t("Loading history")} />
          : history.isError ? <ErrorState error={history.error} onRetry={() => history.refetch()} />
          : items.length === 0 ? (
            <EmptyState icon={<Mountain />} title={filter === "PROJECTS" ? "No open projects" : "No climbs logged yet"}
              body={p.isMe ? "Open a boulder at your gym and tap “Mark as completed” or add attempts." : undefined}
              action={p.isMe ? <Link to="/explore" className="btn btn--primary"><span>{t("Find a gym")}</span></Link> : undefined} />
          ) : (
            <>
              <ul className="history">{items.map((i) => <HistoryRow key={i.boulder.id} item={i} />)}</ul>
              {history.hasNextPage && <Button variant="secondary" onClick={() => history.fetchNextPage()} loading={history.isFetchingNextPage}>{t("Show more")}</Button>}
            </>
          )}
      </section>
    </div>
  );
}
