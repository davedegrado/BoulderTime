import { Rail } from "@/components/Rail";
import { Link } from "react-router-dom";
import { useState } from "react";
import { ChevronDown, ChevronRight, Compass, Heart, Target, X } from "lucide-react";
import { useAuth } from "@/auth/AuthProvider";
import { useCurrentUser } from "@/features/users/api";
import { useHome, type HomeGym } from "@/features/climbing/api";
import { InvitationsCard } from "@/features/staff/InvitationsCard";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { HistoryRow } from "@/features/climbing/ClimbingBits";
import { AnnouncementCard } from "@/features/notifications/NotificationBits";
import { useHideUpdate, type Announcement } from "@/features/notifications/api";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { GymAvatar } from "@/components/GymAvatar";
import { Logo } from "@/components/Logo";
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
  const keepTrying = h.projects.filter((b) => !b.isProject);
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

      {updates.length > 0 && <GymUpdates updates={updates} total={h.updates.length} />}

      {h.freshToTry.length > 0 && (
        <section aria-labelledby="fresh-title" className="section">
          <h2 id="fresh-title" className="section__title">{t("Fresh on the wall")}</h2>
          <Rail>{h.freshToTry.map((b) => <BoulderCard key={b.id} boulder={b} showGym />)}</Rail>
        </section>
      )}

      {(h.markedProjects ?? []).length > 0 && (
        <section aria-labelledby="marked-title" className="section">
          <h2 id="marked-title" className="section__title">{t("Your projects")}</h2>
          <Rail>{h.markedProjects!.map((b) => <BoulderCard key={b.id} boulder={b} showGym showProjectMark={false} />)}</Rail>
        </section>
      )}
      <ProjectsInvitation count={h.unmarkedTries ?? 0} />

      {/* Tried and not completed; the projects already have their own row above. */}
      {keepTrying.length > 0 && (
        <section aria-labelledby="projects-title" className="section">
          <h2 id="projects-title" className="section__title">{t("Keep trying")}</h2>
          <Rail>{keepTrying.map((b) => <BoulderCard key={b.id} boulder={b} showGym />)}</Rail>
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

    </div>
  );
}

const COLLAPSED_KEY = "bt:home-updates-collapsed";
const readCollapsed = () => { try { return localStorage.getItem(COLLAPSED_KEY) === "1"; } catch { return false; } };

/**
 * The gyms' news on Home. It can be folded away, so following many gyms never buries the boulders under it (the choice
 * is remembered on this device), and any single update can be taken off Home; the gym's page still has them all.
 */
function GymUpdates({ updates, total }: { updates: Announcement[]; total: number }) {
  const [collapsed, setCollapsed] = useState(readCollapsed);
  const hide = useHideUpdate();
  const toast = useToast();
  const toggle = () => setCollapsed((c) => {
    try { localStorage.setItem(COLLAPSED_KEY, c ? "0" : "1"); } catch { /* remembered for this visit only */ }
    return !c;
  });
  return (
    <section aria-labelledby="updates-title" className="section">
      {/* The heading holds the button, not the other way round: a heading inside a button stops being one. */}
      <h2 id="updates-title" className="section__title section__title--toggle">
        <button type="button" className="section__toggle" aria-expanded={!collapsed} aria-controls="home-updates" onClick={toggle}>
          <span>{t("Gym updates")}</span>
          {collapsed && <span className="section__count">{plural(total, "1 update", "{count} updates")}</span>}
          <ChevronDown aria-hidden className={`section__chevron ${collapsed ? "" : "is-open"}`} />
        </button>
      </h2>
      {!collapsed && (
        <div id="home-updates" className="stack">
          {updates.map((a) => (
            <AnnouncementCard key={a.id} a={a} showGym
              onHide={() => hide.mutate(a.id, { onSuccess: () => toast.success(t("Hidden from Home. It stays on the gym's page.")), onError: (e) => toast.error(errorMessage(e)) })} />
          ))}
        </div>
      )}
    </section>
  );
}


const INVITATION_KEY = "bt.projectsInvitationSeen";

/**
 * Once, for climbers with boulders tried and not completed: projects are now chosen, so here is how (ADR-067). Nothing is
 * marked for them; it leads to what they tried, where each boulder can be made a project. Closing it is for good, on
 * this device.
 */
function ProjectsInvitation({ count }: { count: number }) {
  const [seen, setSeen] = useState(() => { try { return localStorage.getItem(INVITATION_KEY) === "1"; } catch { return false; } });
  if (seen || count === 0) return null;
  const close = () => {
    try { localStorage.setItem(INVITATION_KEY, "1"); } catch { /* shown again next time: harmless */ }
    setSeen(true);
  };
  return (
    <section className="card projects-invite" aria-labelledby="projects-invite-title">
      <span className="projects-invite__icon" aria-hidden><Target /></span>
      <div className="projects-invite__text">
        <h2 id="projects-invite-title" className="projects-invite__title">{t("Choose your projects")}</h2>
        <p>{plural(count, "You have 1 boulder tried and not completed. Open it and turn on “Project” if you want to work on it.",
          "You have {count} boulders tried and not completed. Open the ones you want to work on and turn on “Project”.")}</p>
        <Link to="/activity?history=PROJECTS" className="section__link" onClick={close}>{t("See what you tried")}</Link>
      </div>
      <button type="button" className="icon-btn" aria-label={t("Not now")} title={t("Not now")} onClick={close}><X aria-hidden /></button>
    </section>
  );
}
