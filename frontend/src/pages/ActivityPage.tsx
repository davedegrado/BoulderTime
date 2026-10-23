import type { ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Mountain } from "lucide-react";
import { useCurrentUser } from "@/features/users/api";
import { useHistory, useProfile, type HistoryFilter, type Profile } from "@/features/climbing/api";
import { HighestGrades, HistoryRow, WeeklyChart } from "@/features/climbing/ClimbingBits";
import { FavoriteSectorCard } from "@/features/climbing/FavoriteSector";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { t } from "@/i18n/i18n";

export function ActivityPage() {
  const me = useCurrentUser();
  if (me.isPending) return <LoadingState />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  return <ClimbingActivity userId={me.data.id} title={t("Your climbing")} />;
}

/** Stats, highest grades, weekly activity and full history: your own Activity. Public profiles arrange the same parts. */
export function ClimbingActivity({ userId, title, header }: { userId: string; title?: string; header?: ReactNode }) {
  const profile = useProfile(userId);

  if (profile.isPending) return <LoadingState label={t("Loading activity")} />;
  if (profile.isError) return <ErrorState error={profile.error} onRetry={() => profile.refetch()} />;
  const p = profile.data;

  return (
    <div className="page">
      {header ?? <header className="page__header"><h1 className="page__title">{title}</h1></header>}

      <StatsGrid stats={p.stats} />

      {p.favoriteSector && <FavoriteSectorCard sector={p.favoriteSector} isMe={p.isMe} />}

      <section className="section card" aria-labelledby="weekly-title">
        <h2 id="weekly-title" className="section__title">{t("Last 12 weeks")}</h2>
        <WeeklyChart weeks={p.weekly} />
      </section>

      <section className="section card" aria-labelledby="highest-title">
        <h2 id="highest-title" className="section__title">{t("Highest grades")}</h2>
        <HighestGrades grades={p.highestGrades} />
      </section>

      <HistorySection userId={userId} isMe={p.isMe} />
    </div>
  );
}

/** The four numbers of a climber. */
export function StatsGrid({ stats }: { stats: Profile["stats"] }) {
  return (
    <div className="stats">
      <div className="stat"><span className="stat__value">{stats.completed}</span><span className="stat__label">{t("Sends")}</span></div>
      <div className="stat"><span className="stat__value">{stats.projects}</span><span className="stat__label">{t("Projects")}</span></div>
      <div className="stat"><span className="stat__value">{stats.completedThisMonth}</span><span className="stat__label">{t("Sent this month")}</span></div>
      <div className="stat"><span className="stat__value">{stats.totalAttempts}</span><span className="stat__label">{t("Attempts")}</span></div>
    </div>
  );
}

/** The whole history, a page at a time, with its All / Completed / Projects choice kept in the address. */
export function HistorySection({ userId, isMe, titled = true }: { userId: string; isMe: boolean; titled?: boolean }) {
  // In the address, so coming back from a boulder of the history finds the same choice (and the same place in it).
  const [params, setParams] = useSearchParams();
  const asked = params.get("history") as HistoryFilter | null;
  const filter: HistoryFilter = asked === "COMPLETED" || asked === "PROJECTS" ? asked : "ALL";
  const setFilter = (f: HistoryFilter) => setParams((p) => { const n = new URLSearchParams(p); if (f === "ALL") n.delete("history"); else n.set("history", f); return n; }, { replace: true });
  const history = useHistory(userId, filter);
  const items = history.data?.pages.flatMap((pg) => pg.items) ?? [];
  return (
      <section className="section" aria-labelledby={titled ? "history-title" : undefined} aria-label={titled ? undefined : t("History")}>
        {titled && <h2 id="history-title" className="section__title">{t("History")}</h2>}
        <div className="chips" role="radiogroup" aria-label={t("Filter history")}>
          {(["ALL", "COMPLETED", "PROJECTS"] as HistoryFilter[]).map((f) => (
            <button key={f} role="radio" aria-checked={filter === f} className="chip" onClick={() => setFilter(f)}>
              {f === "ALL" ? t("All") : f === "COMPLETED" ? t("Completed") : t("Projects")}
            </button>
          ))}
        </div>
        {history.isPending ? <LoadingState label={t("Loading history")} />
          : history.isError ? <ErrorState error={history.error} onRetry={() => history.refetch()} />
          : items.length === 0 ? (
            <EmptyState icon={<Mountain />} title={filter === "PROJECTS" ? t("No open projects") : t("No climbs logged yet")}
              body={isMe ? t("Open a boulder at your gym and tap “Mark as completed” or add attempts.") : undefined}
              action={isMe ? <Link to="/explore" className="btn btn--primary"><span>{t("Find a gym")}</span></Link> : undefined} />
          ) : (
            <>
              <ul className="history">{items.map((i) => <HistoryRow key={i.boulder.id} item={i} />)}</ul>
              {history.hasNextPage && <Button variant="secondary" onClick={() => history.fetchNextPage()} loading={history.isFetchingNextPage}>{t("Show more")}</Button>}
            </>
          )}
      </section>
  );
}
