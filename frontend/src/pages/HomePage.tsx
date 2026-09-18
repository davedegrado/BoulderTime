import { Link } from "react-router-dom";
import { ChevronRight, Compass, Heart, Plus, ShieldCheck } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { useHome } from "@/features/climbing/api";
import { InvitationsCard } from "@/features/staff/InvitationsCard";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { HistoryRow } from "@/features/climbing/ClimbingBits";
import { AnnouncementCard } from "@/features/notifications/NotificationBits";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { GymAvatar } from "@/components/GymAvatar";
import { Logo } from "@/components/Logo";
import { roleLabel } from "@/lib/format";
import { plural, t } from "@/i18n/i18n";

export function HomePage() {
  const { session, initializing } = useAuth();
  if (initializing) return <LoadingState />;
  return session ? <SignedInHome /> : <GuestHome />;
}

function GuestHome() {
  return (
    <section className="hero">
      <div className="hero__mark"><Logo variant="icon" height={96} /></div>
      <h1 className="hero__title">{t("Every problem on the wall, in your pocket.")}</h1>
      <p className="hero__lede">
        See what's set at your gym right now, log your sends and attempts, and watch beta for the problem in front of you.
      </p>
      <div className="hero__actions">
        <Link to="/sign-up" className="btn btn--primary btn--lg"><span>{t("Create an account")}</span></Link>
        <Link to="/explore" className="btn btn--on-dark btn--lg"><Compass aria-hidden /><span>{t("Find a gym")}</span></Link>
      </div>
    </section>
  );
}

function SignedInHome() {
  const me = useCurrentUser();
  const home = useHome();

  if (me.isPending || home.isPending) return <LoadingState label={t("Loading your climbing")} />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  if (home.isError) return <ErrorState error={home.error} onRetry={() => home.refetch()} />;

  const user = me.data;
  const h = home.data;
  const firstName = user.displayName.split(" ")[0];

  return (
    <div className="page">
      <header className="page__header">
        <h1 className="page__title">{t("Hey {name}", { name: firstName ?? user.displayName })}</h1>
        <p className="page__subtitle">
          {h.stats.completedThisMonth > 0 ? t("{count} sent this month. Keep it going.", { count: h.stats.completedThisMonth }) : "Ready for a session?"}
        </p>
      </header>

      <InvitationsCard count={user.pendingInvitations} />

      <section aria-labelledby="gyms-title" className="section">
        <h2 id="gyms-title" className="section__title">{t("Your gyms")}</h2>
        {h.gyms.length === 0 ? (
          <EmptyState icon={<Compass />} title={t("You aren't following any gyms yet.")}
            body={t("Follow the gyms you climb at to see new boulders and your projects here.")}
            action={<Link to="/explore" className="btn btn--primary"><Compass aria-hidden /><span>{t("Find a gym")}</span></Link>} />
        ) : (
          <ul className="list">
            {h.gyms.map((g) => (
              <li key={g.gym.id}>
                <Link to={`/gyms/${g.gym.slug}`} className="list__row list__row--link">
                  <GymAvatar name={g.gym.name} logoUrl={g.gym.logoUrl} size={44} />
                  <div className="list__main">
                    <p className="list__title">{g.gym.name} {g.isFavorite && <Heart className="inline-fav" aria-label={t("Favourite")} />}</p>
                    <p className="list__sub">{plural(g.activeBoulders, "{count} boulder", "{count} boulders")}{g.newThisWeek > 0 && ` · ${t("{count} new this week", { count: g.newThisWeek })}`}</p>
                  </div>
                  <ChevronRight className="list__chevron" aria-hidden />
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      {h.updates.length > 0 && (
        <section aria-labelledby="updates-title" className="section">
          <h2 id="updates-title" className="section__title">{t("Gym updates")}</h2>
          <div className="stack">{h.updates.slice(0, 3).map((a) => <AnnouncementCard key={a.id} a={a} showGym />)}</div>
        </section>
      )}

      {h.projects.length > 0 && (
        <section aria-labelledby="projects-title" className="section">
          <h2 id="projects-title" className="section__title">{t("Keep trying")}</h2>
          <div className="rail">{h.projects.map((b) => <BoulderCard key={b.id} boulder={b} />)}</div>
        </section>
      )}

      {h.freshToTry.length > 0 && (
        <section aria-labelledby="fresh-title" className="section">
          <h2 id="fresh-title" className="section__title">{t("Fresh on the wall")}</h2>
          <div className="rail">{h.freshToTry.map((b) => <BoulderCard key={b.id} boulder={b} />)}</div>
        </section>
      )}

      {h.recentCompletions.length > 0 && (
        <section aria-labelledby="recent-title" className="section">
          <div className="section__row">
            <h2 id="recent-title" className="section__title">{t("Recent sends")}</h2>
            <Link to="/activity" className="section__link">{t("All activity")}</Link>
          </div>
          <ul className="history">{h.recentCompletions.map((i) => <HistoryRow key={i.boulder.id} item={i} />)}</ul>
        </section>
      )}

      {(user.staffGyms.length > 0 || user.isPlatformAdmin) && (
        <section aria-labelledby="work-title" className="section">
          <h2 id="work-title" className="section__title">{t("Gyms you manage")}</h2>
          <ul className="list">
            {user.staffGyms.map((g) => (
              <li key={g.gymId}>
                <Link to={`/manage/${g.slug}`} className="list__row list__row--link">
                  <GymAvatar name={g.name} logoUrl={g.logoUrl} size={40} />
                  <div className="list__main">
                    <p className="list__title">{g.name}</p>
                    <p className="list__sub">{roleLabel[g.role]} · {g.city}</p>
                  </div>
                  <ChevronRight className="list__chevron" aria-hidden />
                </Link>
              </li>
            ))}
            {user.isPlatformAdmin && (
              <li>
                <Link to="/admin" className="list__row list__row--link">
                  <span className="list__badge-icon"><ShieldCheck aria-hidden /></span>
                  <div className="list__main">
                    <p className="list__title">{t("BoulderTime admin")}</p>
                    <p className="list__sub">{t("Gyms, suggestions and users")}</p>
                  </div>
                  <ChevronRight className="list__chevron" aria-hidden />
                </Link>
              </li>
            )}
          </ul>
        </section>
      )}

      <Link to="/gyms/suggest" className="btn btn--ghost"><Plus aria-hidden /><span>{t("Suggest a gym")}</span></Link>
    </div>
  );
}
