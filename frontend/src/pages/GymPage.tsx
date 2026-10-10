import { lazy, Suspense, useEffect, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { ExternalLink, Globe, Layers, Mail, MapPin, Maximize2, Phone, Settings2, X, ZoomIn, ZoomOut } from "lucide-react";
import { useGym, useSectors, type GymDetail, type Sector } from "@/features/gyms/api";
import { floorPlanOf, SectorMap, type FloorPlan } from "@/features/gyms/SectorMap";
import { GymAvatar } from "@/components/GymAvatar";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { ApiError } from "@/lib/apiError";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { gymStatusLabel } from "@/lib/format";
import { useBoulders, type BoulderFilters } from "@/features/boulders/api";
import { useGradeSystems } from "@/features/grading/api";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { activeFilters, BoulderStatusSelect, boulderCount, FiltersPanel, FiltersToggle } from "@/features/boulders/BoulderFilters";
import { Button } from "@/components/Button";
import { Bell, BellRing, Heart, Megaphone, Mountain } from "lucide-react";
import { useAnnouncements } from "@/features/notifications/api";
import { AnnouncementCard } from "@/features/notifications/NotificationBits";
import { LeaderboardTab } from "@/features/leaderboards/LeaderboardTab";
import { useFollowGym, useFollowSector } from "@/features/climbing/api";
import { useAuth } from "@/auth/AuthProvider";
import { t, plural, t as translate } from "@/i18n/i18n";
import { GymBadges } from "@/features/gyms/GymBadges";
import { FacebookIcon, InstagramIcon } from "@/components/BrandIcons";

const StaticGymMap = lazy(() => import("@/features/map/GymMap").then((m) => ({ default: m.StaticGymMap })));

type Tab = "boulders" | "sectors" | "updates" | "ranking" | "info";
const TABS: Tab[] = ["boulders", "sectors", "updates", "ranking", "info"];
const TAB_LABEL: Record<Tab, string> = { boulders: "Boulders", sectors: "Sectors", updates: "Updates", ranking: "Ranking", info: "Info" };

export function GymPage() {
  const { slug = "" } = useParams();
  const gym = useGym(slug);
  const [params, setParams] = useSearchParams();
  const tab: Tab = TABS.includes(params.get("tab") as Tab) ? (params.get("tab") as Tab) : "boulders";
  const setTab = (t: Tab) => setParams(t === "boulders" ? {} : { tab: t }, { replace: true });
  // "?sector=…" opens the boulders of one sector: from the map, the sector list, or a notification about it.
  const sectorParam = params.get("sector");

  if (gym.isPending) return <LoadingState label={translate("Loading gym")} />;
  if (gym.isError) return gym.error instanceof ApiError && gym.error.isNotFound ? <NotFoundPage /> : <ErrorState error={gym.error} onRetry={() => gym.refetch()} />;

  const g = gym.data;
  return (
    <div className="page page--flush gym-page">
      <header className="gym-hero">
        <div className="gym-hero__cover" style={g.coverImageUrl ? { backgroundImage: `url(${g.coverImageUrl})` } : undefined} />
        <div className="gym-hero__body">
          {/* The logo overlaps the cover; the actions sit level with it, as icons, so the name gets the full width. */}
          <div className="gym-hero__top">
            <GymAvatar name={g.name} logoUrl={g.logoUrl} size={72} />
            <div className="gym-hero__actions">
              <GymFollowControls gym={g} />
              {g.viewerRole && (
                <Link to={`/manage/${g.slug}`} className="icon-btn icon-btn--outlined" aria-label={translate("Manage")} title={translate("Manage")}>
                  <Settings2 aria-hidden />
                </Link>
              )}
            </div>
          </div>
          <div className="gym-hero__text">
            <h1 className="page__title gym-hero__name">{g.name}</h1>
            <GymBadges isFoundingGym={g.isFoundingGym} isEarlyPartner={g.isEarlyPartner} />
            <p className="gym-hero__meta"><MapPin aria-hidden /> {g.city}{g.followerCount > 0 && ` · ${plural(g.followerCount, "{count} follower", "{count} followers")}`}</p>
            {g.status !== "ACTIVE" && <Badge tone="dark">{gymStatusLabel[g.status]} · {t("only staff can see this")}</Badge>}
          </div>
        </div>
      </header>

      <div className="tabs" role="tablist" aria-label={translate("Gym sections")}>
        {TABS.map((t) => (
          <button key={t} role="tab" aria-selected={tab === t} className="tabs__tab" onClick={() => setTab(t)}>{translate(TAB_LABEL[t])}</button>
        ))}
      </div>

      <div className="page__pad">
        {tab === "boulders" ? <BouldersTab key={sectorParam ?? "all"} gymId={g.id} initialSectorId={sectorParam} /> : tab === "sectors" ? <SectorsTab gym={g} onShowBoulders={(sectorId) => setParams({ sector: sectorId }, { replace: true })} /> : tab === "updates" ? <UpdatesTab gymId={g.id} /> : tab === "ranking" ? <LeaderboardTab gymId={g.id} /> : <InfoTab gym={g} />}
      </div>
    </div>
  );
}

function GymFollowControls({ gym }: { gym: GymDetail }) {
  const { session } = useAuth();
  const follow = useFollowGym(gym);
  if (!session) {
    return (
      <Link to={`/sign-in?next=/gyms/${gym.slug}`} className="icon-btn icon-btn--outlined" aria-label={translate("Follow")} title={translate("Follow")}>
        <Bell aria-hidden />
      </Link>
    );
  }
  const state = gym.follow ?? { isFollowing: false, isFavorite: false };
  const followLabel = state.isFollowing ? translate("Stop following this gym") : translate("Follow this gym");
  return (
    <>
      <button type="button" className={`icon-btn icon-btn--outlined ${state.isFollowing ? "is-following" : ""}`} aria-pressed={state.isFollowing}
        aria-label={followLabel} title={followLabel} onClick={() => follow.mutate({ isFollowing: !state.isFollowing, isFavorite: false })}>
        {state.isFollowing ? <BellRing aria-hidden /> : <Bell aria-hidden />}
      </button>
      {state.isFollowing && (
        <button type="button" className={`icon-btn icon-btn--outlined fav-btn ${state.isFavorite ? "is-on" : ""}`} aria-pressed={state.isFavorite}
          aria-label={state.isFavorite ? translate("Remove from favourites") : translate("Add to favourites")}
          onClick={() => follow.mutate({ isFollowing: true, isFavorite: !state.isFavorite })}>
          <Heart aria-hidden />
        </button>
      )}
    </>
  );
}

function BouldersTab({ gymId, initialSectorId }: { gymId: string; initialSectorId?: string | null }) {
  const [filters, setFilters] = useState<BoulderFilters>(() => (initialSectorId ? { sectorId: initialSectorId } : {}));
  const [showFilters, setShowFilters] = useState(false);
  const showingRemoved = filters.status === "REMOVED";
  const sectors = useSectors(gymId);
  const systems = useGradeSystems(gymId);
  const boulders = useBoulders(gymId, filters);
  const items = boulders.data?.pages.flatMap((p) => p.items) ?? [];
  const total = boulders.data?.pages[0]?.total ?? 0;
  // Status is a tab, not a filter: an empty "taken down" list means nothing has come off the wall, not that the
  // filters are too narrow, and "clear filters" must not throw the reader back to the other tab.
  const { status: _status, ...narrowing } = filters;
  const filtered = Object.values(narrowing).some(Boolean);
  const active = activeFilters(filters, sectors.data ?? [], systems.data ?? []);

  return (
    <div className="stack">
      {/* A boulder you sent is part of your history long after it comes off the wall, so climbers can look back. */}
      <div className="boulders-toolbar">
        <span className="section__meta boulders-toolbar__count">{!boulders.isPending && !boulders.isError && items.length > 0 ? boulderCount(total, showingRemoved) : null}</span>
        <div className="boulders-toolbar__end">
          <BoulderStatusSelect removed={showingRemoved} onChange={(removed) => setFilters({ ...filters, status: removed ? "REMOVED" : undefined })} />
          <FiltersToggle open={showFilters} count={active.length} onToggle={() => setShowFilters((v) => !v)} />
        </div>
      </div>
      <FiltersPanel open={showFilters} filters={filters} onChange={setFilters} sectors={sectors.data ?? []} systems={systems.data ?? []} />
      {boulders.isPending ? <LoadingState label={translate("Loading boulders")} />
        : boulders.isError ? <ErrorState error={boulders.error} onRetry={() => boulders.refetch()} />
        : items.length === 0 ? (
          <EmptyState icon={<Mountain />}
            title={filtered ? translate("No boulders match these filters") : showingRemoved ? translate("Nothing has been taken down yet") : translate("No boulders on the wall yet")}
            body={filtered ? translate("Try another sector, grade or hold colour.") : showingRemoved ? translate("Boulders that come off the wall stay here, so you can find what you climbed.") : translate("This gym hasn't added its current boulders yet.")}
            action={filtered ? <Button variant="secondary" onClick={() => setFilters({ status: filters.status })}>{translate("Clear filters")}</Button> : undefined} />
        ) : (
          <>
            <div className="boulder-grid">{items.map((b) => <BoulderCard key={b.id} boulder={b} />)}</div>
            {boulders.hasNextPage && <Button variant="secondary" onClick={() => boulders.fetchNextPage()} loading={boulders.isFetchingNextPage}>{translate("Show more")}</Button>}
          </>
        )}
    </div>
  );
}

function UpdatesTab({ gymId }: { gymId: string }) {
  const updates = useAnnouncements(gymId);
  const items = updates.data?.pages.flatMap((p) => p.items) ?? [];
  if (updates.isPending) return <LoadingState label={translate("Loading updates")} />;
  if (updates.isError) return <ErrorState error={updates.error} onRetry={() => updates.refetch()} />;
  if (items.length === 0) return <EmptyState icon={<Megaphone />} title={translate("No updates yet")} body={translate("Events, new circuits and schedule changes will show up here.")} />;
  return (
    <div className="stack">
      {items.map((a) => <AnnouncementCard key={a.id} a={a} />)}
      {updates.hasNextPage && <Button variant="secondary" onClick={() => updates.fetchNextPage()} loading={updates.isFetchingNextPage}>{translate("Older updates")}</Button>}
    </div>
  );
}

function SectorsTab({ gym, onShowBoulders }: { gym: GymDetail; onShowBoulders: (sectorId: string) => void }) {
  const sectors = useSectors(gym.id);
  const { session } = useAuth();
  const followSector = useFollowSector(gym.id);
  const [openId, setOpenId] = useState<string | null>(null);
  const [fullScreen, setFullScreen] = useState(false);
  if (sectors.isPending) return <LoadingState label={translate("Loading sectors")} />;
  if (sectors.isError) return <ErrorState error={sectors.error} onRetry={() => sectors.refetch()} />;
  if (sectors.data.length === 0) return <EmptyState icon={<Layers />} title={translate("No sectors yet")} body={translate("This gym hasn't set up its sectors on BoulderTime.")} />;

  const plan = floorPlanOf(gym);
  const open = sectors.data.find((s) => s.id === openId) ?? null;
  const toggleFollow = (s: Sector) => followSector.mutate({ sectorId: s.id, follow: !s.isFollowing });

  return (
    <div className="stack">
      {/* The plan comes first when the gym has one; the list stays, for following and for whoever reads it better. */}
      {plan && sectors.data.some((s) => s.zone) && (
        <section className="sector-map-card" aria-label={translate("Gym map")}>
          <SectorMap plan={plan} sectors={sectors.data} onSelect={(s) => setOpenId(s.id)} />
          <div className="sector-map-card__foot">
            <p className="sector-map-card__hint">{translate("Tap a sector to see its boulders.")}</p>
            <button type="button" className="icon-btn icon-btn--outlined icon-btn--sm" onClick={() => setFullScreen(true)} aria-label={translate("Enlarge the map")} title={translate("Enlarge the map")}>
              <Maximize2 aria-hidden />
            </button>
          </div>
        </section>
      )}
      <ul className="list">
        {sectors.data.map((s) => (
          <li key={s.id} className={`list__row ${s.isActive ? "" : "list__row--muted"}`}>
            <button type="button" className="sector-row" onClick={() => setOpenId(s.id)}>
              <span className="sector-mark" aria-hidden />
              <span className="list__main">
                <span className="list__title">{s.name}</span>
                <span className="list__sub">{sectorCounts(s)}</span>
              </span>
            </button>
            {!s.isActive && <Badge>{translate("Hidden")}</Badge>}
            {session && s.isActive && (
              <button type="button" className={`icon-btn ${s.isFollowing ? "is-following" : ""}`} aria-pressed={s.isFollowing}
                aria-label={s.isFollowing ? translate("Unfollow {sector}", { sector: s.name }) : translate("Follow {sector}", { sector: s.name })}
                onClick={() => toggleFollow(s)}>
                {s.isFollowing ? <BellRing aria-hidden /> : <Bell aria-hidden />}
              </button>
            )}
          </li>
        ))}
      </ul>
      {fullScreen && plan && <MapViewer plan={plan} sectors={sectors.data} onSelect={(s) => setOpenId(s.id)} onClose={() => setFullScreen(false)} />}
      {open && (
        <SectorSheet sector={open} signedIn={!!session} onClose={() => setOpenId(null)} onToggleFollow={() => toggleFollow(open)}
          onShowBoulders={() => { setOpenId(null); setFullScreen(false); onShowBoulders(open.id); }} />
      )}
    </div>
  );
}

/** "18 on the wall · 3 new", or that there is nothing up yet. */
function sectorCounts(s: Sector): string {
  const active = s.activeBoulders ?? 0;
  if (active === 0) return translate("No boulders on the wall");
  const base = boulderCount(active, false);
  return (s.newThisWeek ?? 0) > 0 ? `${base} · ${plural(s.newThisWeek ?? 0, "1 new", "{count} new")}` : base;
}

/** One sector, from the plan or the list: what is on it, its boulders, and following it. */
function SectorSheet({ sector, signedIn, onClose, onShowBoulders, onToggleFollow }: {
  sector: Sector; signedIn: boolean; onClose: () => void; onShowBoulders: () => void; onToggleFollow: () => void;
}) {
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);
  return (
    <div className="sheet-backdrop" onClick={onClose}>
      <div className="sheet sector-sheet" role="dialog" aria-modal="true" aria-labelledby="sector-sheet-title" onClick={(e) => e.stopPropagation()}>
        <div className="sector-sheet__head">
          <h2 id="sector-sheet-title" className="section__title">{sector.name}</h2>
          <button type="button" className="icon-btn" onClick={onClose} aria-label={translate("Close")}><X aria-hidden /></button>
        </div>
        <p className="list__sub">{sectorCounts(sector)}</p>
        {sector.description && <p className="sector-sheet__text">{sector.description}</p>}
        <div className="sector-sheet__actions">
          <Button icon={<Mountain aria-hidden />} onClick={onShowBoulders}>{translate("See the boulders")}</Button>
          {signedIn && (
            <Button variant="secondary" icon={sector.isFollowing ? <BellRing aria-hidden /> : <Bell aria-hidden />} onClick={onToggleFollow} aria-pressed={sector.isFollowing}>
              {sector.isFollowing ? translate("Following") : translate("Follow")}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}

/** The plan full screen, larger and scrollable, for a gym with many small sectors. */
function MapViewer({ plan, sectors, onSelect, onClose }: { plan: FloorPlan; sectors: Sector[]; onSelect: (s: Sector) => void; onClose: () => void }) {
  const [zoom, setZoom] = useState(1);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { window.removeEventListener("keydown", onKey); document.body.style.overflow = overflow; };
  }, [onClose]);
  return (
    <div className="photo-viewer map-viewer" role="dialog" aria-modal="true" aria-label={translate("Gym map")}>
      <div className="viewer__top">
        <button type="button" className="viewer__close" onClick={() => setZoom((z) => (z >= 3 ? 1 : z + 1))} aria-label={zoom >= 3 ? translate("Zoom out") : translate("Zoom in")}>
          {zoom >= 3 ? <ZoomOut aria-hidden /> : <ZoomIn aria-hidden />}
        </button>
        <button type="button" className="viewer__close" onClick={onClose} aria-label={translate("Close")}><X aria-hidden /></button>
      </div>
      <div className="map-viewer__stage">
        <div className="map-viewer__inner" style={{ width: `${zoom * 100}%` }}>
          <SectorMap plan={plan} sectors={sectors} onSelect={onSelect} />
        </div>
      </div>
    </div>
  );
}

function InfoTab({ gym }: { gym: GymDetail }) {
  const mapsUrl = `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent([gym.name, gym.address, gym.city].filter(Boolean).join(", "))}`;
  return (
    <div className="stack">
      {gym.latitude != null && gym.longitude != null && (
        <Suspense fallback={<div className="gym-map gym-map--static gym-map--loading" />}>
          <StaticGymMap lat={gym.latitude} lng={gym.longitude} name={gym.name} />
        </Suspense>
      )}
      {gym.description && <p className="prose">{gym.description}</p>}
      <ul className="list">
        <li className="list__row">
          <MapPin className="list__icon" aria-hidden />
          <div className="list__main">
            <p className="list__title">{gym.address ?? gym.city}</p>
            {gym.address && <p className="list__sub">{gym.city}</p>}
          </div>
          <a className="icon-link" href={mapsUrl} target="_blank" rel="noreferrer" aria-label={translate("Open in maps")}><ExternalLink aria-hidden /></a>
        </li>
        {gym.website && (
          <li className="list__row">
            <Globe className="list__icon" aria-hidden />
            <a className="list__main list__link" href={gym.website} target="_blank" rel="noreferrer">{gym.website.replace(/^https?:\/\//, "")}</a>
          </li>
        )}
        {gym.instagramUrl && (
          <li className="list__row">
            <InstagramIcon className="list__icon" aria-hidden />
            <a className="list__main list__link" href={gym.instagramUrl} target="_blank" rel="noreferrer">
              {gym.instagramUrl.replace(/^https?:\/\/(www\.)?instagram\.com\//, "@").replace(/\/$/, "")}
            </a>
          </li>
        )}
        {gym.facebookUrl && (
          <li className="list__row">
            <FacebookIcon className="list__icon" aria-hidden />
            <a className="list__main list__link" href={gym.facebookUrl} target="_blank" rel="noreferrer">{translate("Facebook")}</a>
          </li>
        )}
        {gym.email && (
          <li className="list__row">
            <Mail className="list__icon" aria-hidden />
            <a className="list__main list__link" href={`mailto:${gym.email}`}>{gym.email}</a>
          </li>
        )}
        {gym.phone && (
          <li className="list__row">
            <Phone className="list__icon" aria-hidden />
            <a className="list__main list__link" href={`tel:${gym.phone.replace(/\s/g, "")}`}>{gym.phone}</a>
          </li>
        )}
      </ul>
    </div>
  );
}
