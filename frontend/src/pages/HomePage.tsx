import { Link } from "react-router-dom";
import { ChevronRight, Compass, Heart, Plus, ShieldCheck } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { useHome, type HomeGym } from "@/features/climbing/api";
import { InvitationsCard } from "@/features/staff/InvitationsCard";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { HistoryRow } from "@/features/climbing/ClimbingBits";
import { AnnouncementCard } from "@/features/notifications/NotificationBits";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { GymAvatar } from "@/components/GymAvatar";
import { Logo } from "@/components/Logo";
import { roleLabel } from "@/lib/format";
import { plural, t } from "@/i18n/i18n";
import { GymBadges } from "@/features/gyms/GymBadges";

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
        {t("See what's set at your gym right now, log your sends and attempts, and watch beta for the problem in front of you.")}
      </p>
      <div className="hero__actions">
        <Link to="/sign-up" className="btn btn--primary btn--lg"><span>{t("Create an account")}</span></Link>
        <Link to="/explore" className="btn btn--on-dark btn--lg"><Compass aria-hidden /><span>{t("Find a gym")}</span></Link>
      </div>
    </section>
  );
}

function GymRow({ g, compact = false }: { g: HomeGym; compact?: boolean }) {
  return (
    <Link to={`/gyms/${g.gym.slug}`} className={`list__row list__row--link ${compact ? "home-gym--compact" : "home-gym"}`}>
      <GymAvatar name={g.gym.name} logoUrl={g.gym.logoUrl} size={compact ? 36 : 52} />
      <div className="list__main">
        <p className="list__title">{g.gym.name} {g.isFavorite && <Heart className="inline-fav" aria-label={t("Favourite")} />}</p>
        {!compact && <GymBadges isFoundingGym={g.gym.isFoundingGym} isEarlyPartner={g.gym.isEarlyPartner} size="compact" />}
        <p className="list__sub">
          {plural(g.activeBoulders, "{count} boulder", "{count} boulders")}
          {g.newThisWeek > 0 && <> · <strong className="home-gym__new">{plural(g.newThisWeek, "1 new", "{count} new")}</strong></>}
        </p>
      </div>
      <ChevronRight className="list__chevron" aria-hidden />
    </Link>
  );
}

/**
 * A climber's home: their gyms first — the favourites in front, as many as they marked — then what changed there
 * (the gym's news, the new boulders), then their own climbing: projects to keep trying and the last few sends.
 * Nothing here is new data; it is the same home feed, ordered by what a climber opens the app to check.
 */
function SignedInHome() {
  const me = useCurrentUser();
  const home = useHome();

  if (me.isPending || home.isPending) return <LoadingState label={t("Loading your climbing")} />;
  if (me.isError) return <ErrorState error={me.error} onRetry={() => me.refetch()} />;
  if (home.isError) return <ErrorState error={home.error} onRetry={() => home.refetch()} />;

  const user = me.data;
  const h = home.data;
  const firstName = user.displayName.split(" ")[0];
  const favourites = h.gyms.filter((g) => g.isFavorite);
  const others = h.gyms.filter((g) => !g.isFavorite);
  const favouriteIds = new Set(favourites.map((g) => g.gym.id));
  // News from favourite gyms first; within each group the feed's own order (newest first) is kept.
  const updates = [...h.updates].sort((a, b) => Number(favouriteIds.has(b.gymId)) - Number(favouriteIds.has(a.gymId))).slice(0, 2);

  return (
    <div className="page">
      <header className="page__header">
        <h1 className="page__title">{t("Hey {name}", { name: firstName ?? user.displayName })}</h1>
        <p className="page__subtitle">
          {h.stats.completedThisMonth > 0 ? t("{count} sent this month. Keep it going.", { count: h.stats.completedThisMonth }) : t("Ready for a session?")}
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
          <>
            {favourites.length > 0 && (
              <ul className="list">{favourites.map((g) => <li key={g.gym.id}><GymRow g={g} /></li>)}</ul>
            )}
            {others.length > 0 && (
              <>
                {favourites.length > 0 && <p className="section__meta">{t("You also follow")}</p>}
                <ul className="list">{others.map((g) => <li key={g.gym.id}><GymRow g={g} compact={favourites.length > 0} /></li>)}</ul>
              </>
            )}
            {favourites.length === 0 && <p className="field__hint">{t("Tap the heart on a gym's page to keep it at the top.")}</p>}
          </>
        )}
      </section>

      {updates.length > 0 && (
        <section aria-labelledby="updates-title" className="section">
          <h2 id="updates-title" className="section__title">{t("Gym updates")}</h2>
          <div className="stack">{updates.map((a) => <AnnouncementCard key={a.id} a={a} showGym />)}</div>
        </section>
      )}

      {h.freshToTry.length > 0 && (
        <section aria-labelledby="fresh-title" className="section">
          <h2 id="fresh-title" className="section__title">{t("Fresh on the wall")}</h2>
          <div className="rail">{h.freshToTry.map((b) => <BoulderCard key={b.id} boulder={b} />)}</div>
        </section>
      )}

      {h.projects.length > 0 && (
        <section aria-labelledby="projects-title" className="section">
          <h2 id="projects-title" className="section__title">{t("Keep trying")}</h2>
          <div className="rail">{h.projects.map((b) => <BoulderCard key={b.id} boulder={b} />)}</div>
        </section>
      )}

      {h.recentCompletions.length > 0 && (
        <section aria-labelledby="recent-title" className="section">
          <div className="section__row">
            <h2 id="recent-title" className="section__title">{t("Recent sends")}</h2>
            <Link to="/activity" className="section__link">{t("All activity")}</Link>
          </div>
          <ul className="history">{h.recentCompletions.slice(0, 3).map((i) => <HistoryRow key={i.boulder.id} item={i} />)}</ul>
        </section>
      )}

      {(user.staffGyms.length > 0 || user.isPlatformAdmin) && (
        <section aria-labelledby="work-title" className="section">
          <h2 id="work-title" className="section__title">{t("Gyms you manage")}</h2>
          <ul className="list">
            {user.staffGyms.map((g) => (
              <li key={g.gymId}>
                <Link to={`/manage/${g.slug}`} className="list__row list__row--link">
                  <GymAvatar name={g.name} logoUrl={g.logoUrl} size={36} />
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
