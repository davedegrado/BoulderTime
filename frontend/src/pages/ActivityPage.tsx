import type { ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Mountain } from "lucide-react";
import { useCurrentUser } from "@/features/users/api";
import { useHistory, useProfile, type HistoryFilter, type Profile } from "@/features/climbing/api";
import { CompactHistoryRow, HighestGrades, HistoryRow, WeeklyChart } from "@/features/climbing/ClimbingBits";
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

/** Every boulder tried, completed or not: the sends and those tried and not completed yet (projects among them). */
export const triedCount = (stats: Profile["stats"]) => stats.completed + stats.projects;

/** One number of the statistics: the total, and this month's share where the server counts it by month. */
export interface StatItem { value: number; label: string; month?: number | null }

/**
 * The numbers, each with this month's share below it: sends, projects, everything tried. Attempts are kept as a running
 * total per boulder, so they have no month. Projects are the ones chosen (ADR-067); an older server only knows what was
 * tried and not completed, and then there is no separate count of everything tried.
 */
export function statItems(stats: Profile["stats"]): StatItem[] {
  return [
    { value: stats.completed, label: t("Sends"), month: stats.completedThisMonth },
    { value: stats.markedProjects ?? stats.projects, label: t("Projects"), month: stats.markedProjects != null ? stats.markedProjectsThisMonth : null },
    ...(stats.markedProjects != null ? [{ value: triedCount(stats), label: t("Tried"), month: stats.triedThisMonth }] : []),
    { value: stats.totalAttempts, label: t("Attempts") },
  ];
}

/** "+3 this month" under a number, when the month is known. */
export function MonthShare({ month }: { month?: number | null }) {
  return month != null ? <span className="stat__month">{t("+{count} this month", { count: month })}</span> : null;
}

/** A climber's numbers. */
export function StatsGrid({ stats }: { stats: Profile["stats"] }) {
  return (
    <div className="stats">
      {statItems(stats).map((s) => (
        <div key={s.label} className="stat"><span className="stat__value">{s.value}</span><span className="stat__label">{s.label}</span><MonthShare month={s.month} /></div>
      ))}
    </div>
  );
}

/**
 * The whole history, a page at a time, with its choice kept in the address: everything tried (completed or not), the
 * sends, the projects the person marked. "Tried" is everything, so there is no separate "tried and not completed".
 */
/** `compact`: the shorter rows of a person's profile; this page keeps its own. */
export function HistorySection({ userId, isMe, titled = true, compact = false }: { userId: string; isMe: boolean; titled?: boolean; compact?: boolean }) {
  // In the address, so coming back from a boulder of the history finds the same choice (and the same place in it).
  const [params, setParams] = useSearchParams();
  const asked = params.get("history") as HistoryFilter | null;
  // An old link to "tried and not completed" opens everything tried.
  const filter: HistoryFilter = asked === "COMPLETED" || asked === "MARKED" ? asked : "ALL";
  const setFilter = (f: HistoryFilter) => setParams((p) => { const n = new URLSearchParams(p); if (f === "ALL") n.delete("history"); else n.set("history", f); return n; }, { replace: true });
  const history = useHistory(userId, filter);
  const items = history.data?.pages.flatMap((pg) => pg.items) ?? [];
  return (
      <section className="section" aria-labelledby={titled ? "history-title" : undefined} aria-label={titled ? undefined : t("History")}>
        {titled && <h2 id="history-title" className="section__title">{t("History")}</h2>}
        <div className="chips" role="radiogroup" aria-label={t("Filter history")}>
          {(["ALL", "COMPLETED", "MARKED"] as HistoryFilter[]).map((f) => (
            <button key={f} role="radio" aria-checked={filter === f} className="chip" onClick={() => setFilter(f)}>
              {f === "ALL" ? t("Tried") : f === "COMPLETED" ? t("Sends") : t("Projects")}
            </button>
          ))}
        </div>
        {history.isPending ? <LoadingState label={t("Loading history")} />
          : history.isError ? <ErrorState error={history.error} onRetry={() => history.refetch()} />
          : items.length === 0 ? (
            <EmptyState icon={<Mountain />} title={filter === "MARKED" ? t("No open projects") : t("No climbs logged yet")}
              body={isMe ? (filter === "MARKED" ? t("Open a boulder and turn on “Project” to keep it here until you complete it.") : t("Open a boulder at your gym and tap “Mark as completed” or add attempts.")) : undefined}
              action={isMe ? <Link to="/explore" className="btn btn--primary"><span>{t("Find a gym")}</span></Link> : undefined} />
          ) : (
            <>
              {compact
                ? <ul className="hlist">{items.map((i) => <CompactHistoryRow key={i.boulder.id} item={i} />)}</ul>
                : <ul className="history">{items.map((i) => <HistoryRow key={i.boulder.id} item={i} />)}</ul>}
              {history.hasNextPage && <Button variant="secondary" onClick={() => history.fetchNextPage()} loading={history.isFetchingNextPage}>{t("Show more")}</Button>}
            </>
          )}
      </section>
  );
}
