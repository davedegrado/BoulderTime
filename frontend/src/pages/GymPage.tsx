import { lazy, Suspense, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { ExternalLink, Globe, Layers, Mail, MapPin, Phone, Settings2, SlidersHorizontal, X } from "lucide-react";
import { useGym, useSectors, type GymDetail } from "@/features/gyms/api";
import { GymAvatar } from "@/components/GymAvatar";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { ApiError } from "@/lib/apiError";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { gymStatusLabel } from "@/lib/format";
import { useBoulders, type BoulderFilters } from "@/features/boulders/api";
import { holdLabel } from "@/features/boulders/holdColors";
import { dataLabel } from "@/i18n/data";
import type { Sector } from "@/features/gyms/api";
import type { GradeSystem } from "@/features/grading/api";
import { useGradeSystems } from "@/features/grading/api";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { BoulderFiltersBar } from "@/features/boulders/BoulderFilters";
import { Button } from "@/components/Button";
import { Bell, BellRing, Heart, Megaphone, Mountain } from "lucide-react";
import { useAnnouncements } from "@/features/notifications/api";
import { AnnouncementCard } from "@/features/notifications/NotificationBits";
import { LeaderboardTab } from "@/features/leaderboards/LeaderboardTab";
import { useFollowGym, useFollowSector } from "@/features/climbing/api";
import { FollowButton } from "@/features/climbing/ClimbingBits";
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

  if (gym.isPending) return <LoadingState label={translate("Loading gym")} />;
  if (gym.isError) return gym.error instanceof ApiError && gym.error.isNotFound ? <NotFoundPage /> : <ErrorState error={gym.error} onRetry={() => gym.refetch()} />;

  const g = gym.data;
  return (
    <div className="page page--flush">
      <header className="gym-hero">
        <div className="gym-hero__cover" style={g.coverImageUrl ? { backgroundImage: `url(${g.coverImageUrl})` } : undefined} />
        <div className="gym-hero__body">
          <div className="gym-hero__identity">
            <GymAvatar name={g.name} logoUrl={g.logoUrl} size={72} />
            <div className="gym-hero__text">
              <h1 className="page__title gym-hero__name">{g.name}</h1>
              <p className="gym-hero__meta"><MapPin aria-hidden /> {g.city}{g.followerCount > 0 && ` · ${plural(g.followerCount, "{count} follower", "{count} followers")}`}</p>
            </div>
          </div>
          <div className="gym-hero__row">
            <GymBadges isFoundingGym={g.isFoundingGym} isEarlyPartner={g.isEarlyPartner} />
            {g.status !== "ACTIVE" && <Badge tone="dark">{gymStatusLabel[g.status]} · {t("only staff can see this")}</Badge>}
            <div className="gym-hero__actions">
              <GymFollowControls gym={g} />
              {g.viewerRole && (
                <Link to={`/manage/${g.slug}`} className="btn btn--secondary"><Settings2 aria-hidden /><span>{translate("Manage")}</span></Link>
              )}
            </div>
          </div>
        </div>
      </header>

      <div className="tabs" role="tablist" aria-label={translate("Gym sections")}>
        {TABS.map((t) => (
          <button key={t} role="tab" aria-selected={tab === t} className="tabs__tab" onClick={() => setTab(t)}>{translate(TAB_LABEL[t])}</button>
        ))}
      </div>

      <div className="page__pad">
        {tab === "boulders" ? <BouldersTab gymId={g.id} /> : tab === "sectors" ? <SectorsTab gymId={g.id} /> : tab === "updates" ? <UpdatesTab gymId={g.id} /> : tab === "ranking" ? <LeaderboardTab gymId={g.id} /> : <InfoTab gym={g} />}
      </div>
    </div>
  );
}

function GymFollowControls({ gym }: { gym: GymDetail }) {
  const { session } = useAuth();
  const follow = useFollowGym(gym);
  if (!session) return <Link to={`/sign-in?next=/gyms/${gym.slug}`} className="btn btn--primary"><Bell aria-hidden /><span>{translate("Follow")}</span></Link>;
  const state = gym.follow ?? { isFollowing: false, isFavorite: false };
  return (
    <>
      <FollowButton following={state.isFollowing} onToggle={() => follow.mutate({ isFollowing: !state.isFollowing, isFavorite: false })} />
      {state.isFollowing && (
        <button type="button" className={`icon-btn fav-btn ${state.isFavorite ? "is-on" : ""}`} aria-pressed={state.isFavorite}
          aria-label={state.isFavorite ? translate("Remove from favourites") : translate("Add to favourites")}
          onClick={() => follow.mutate({ isFollowing: true, isFavorite: !state.isFavorite })}>
          <Heart aria-hidden />
        </button>
      )}
    </>
  );
}

type Narrowing = Exclude<keyof BoulderFilters, "status">;

/** The filters in use, each with its own label, so they can be read and removed one by one. */
function activeFilters(f: BoulderFilters, sectors: Sector[], systems: GradeSystem[]): { key: Narrowing; label: string }[] {
  const out: { key: Narrowing; label: string }[] = [];
  if (f.sectorId) out.push({ key: "sectorId", label: sectors.find((s) => s.id === f.sectorId)?.name ?? translate("Sector") });
  if (f.gradeValueId) {
    const value = systems.flatMap((s) => s.values).find((v) => v.id === f.gradeValueId);
    out.push({ key: "gradeValueId", label: value ? dataLabel(value.label) : translate("Grade") });
  }
  if (f.holdColor) out.push({ key: "holdColor", label: holdLabel(f.holdColor) });
  if (f.progress) out.push({ key: "progress", label: f.progress === "UNTRIED" ? translate("Not tried") : f.progress === "PROJECTS" ? translate("Projects") : translate("Completed") });
  if (f.minRating) out.push({ key: "minRating", label: f.minRating === "4" ? translate("4+ stars") : translate("3+ stars") });
  return out;
}

function BouldersTab({ gymId }: { gymId: string }) {
  const [filters, setFilters] = useState<BoulderFilters>({});
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
        <div className="chips" role="radiogroup" aria-label={translate("Boulder status")}>
          <button role="radio" aria-checked={!showingRemoved} className="chip" onClick={() => setFilters({ ...filters, status: undefined })}>{translate("On the wall")}</button>
          <button role="radio" aria-checked={showingRemoved} className="chip" onClick={() => setFilters({ ...filters, status: "REMOVED" })}>{translate("Taken down")}</button>
        </div>
        <button type="button" className={`chip filter-toggle ${active.length ? "is-active" : ""}`} aria-expanded={showFilters} aria-controls="boulder-filters"
          onClick={() => setShowFilters((v) => !v)}>
          <SlidersHorizontal aria-hidden /> {translate("Filters")}{active.length > 0 && <span className="filter-toggle__count">{active.length}</span>}
        </button>
      </div>
      {showFilters && (
        <div id="boulder-filters" className="filter-panel">
          <BoulderFiltersBar filters={filters} onChange={(f) => setFilters({ ...f, status: filters.status })} sectors={sectors.data ?? []} systems={systems.data ?? []} />
        </div>
      )}
      {active.length > 0 && (
        <ul className="active-filters" aria-label={translate("Filters in use")}>
          {active.map((a) => (
            <li key={a.key}>
              <button type="button" className="active-filter" onClick={() => setFilters({ ...filters, [a.key]: undefined })}
                aria-label={translate("Remove filter {name}", { name: a.label })}>
                {a.label} <X aria-hidden />
              </button>
            </li>
          ))}
        </ul>
      )}
      {boulders.isPending ? <LoadingState label={translate("Loading boulders")} />
        : boulders.isError ? <ErrorState error={boulders.error} onRetry={() => boulders.refetch()} />
        : items.length === 0 ? (
          <EmptyState icon={<Mountain />}
            title={filtered ? translate("No boulders match these filters") : showingRemoved ? translate("Nothing has been taken down yet") : translate("No boulders on the wall yet")}
            body={filtered ? translate("Try another sector, grade or hold colour.") : showingRemoved ? translate("Boulders that come off the wall stay here, so you can find what you climbed.") : translate("This gym hasn't added its current boulders yet.")}
            action={filtered ? <Button variant="secondary" onClick={() => setFilters({ status: filters.status })}>{translate("Clear filters")}</Button> : undefined} />
        ) : (
          <>
            <p className="section__meta">{showingRemoved ? plural(total, "{count} taken down", "{count} taken down") : plural(total, "{count} boulder", "{count} boulders")}</p>
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

function SectorsTab({ gymId }: { gymId: string }) {
  const sectors = useSectors(gymId);
  const { session } = useAuth();
  const followSector = useFollowSector(gymId);
  if (sectors.isPending) return <LoadingState label={translate("Loading sectors")} />;
  if (sectors.isError) return <ErrorState error={sectors.error} onRetry={() => sectors.refetch()} />;
  if (sectors.data.length === 0) return <EmptyState icon={<Layers />} title={translate("No sectors yet")} body={translate("This gym hasn't set up its sectors on BoulderTime.")} />;

  return (
    <ul className="list">
      {sectors.data.map((s) => (
        <li key={s.id} className={`list__row ${s.isActive ? "" : "list__row--muted"}`}>
          <span className="sector-mark" aria-hidden />
          <div className="list__main">
            <p className="list__title">{s.name}</p>
            {s.description && <p className="list__sub">{s.description}</p>}
          </div>
          {!s.isActive && <Badge>{translate("Hidden")}</Badge>}
          {session && s.isActive && (
            <button type="button" className={`icon-btn ${s.isFollowing ? "is-following" : ""}`} aria-pressed={s.isFollowing}
              aria-label={s.isFollowing ? translate("Unfollow {sector}", { sector: s.name }) : translate("Follow {sector}", { sector: s.name })}
              onClick={() => followSector.mutate({ sectorId: s.id, follow: !s.isFollowing })}>
              {s.isFollowing ? <BellRing aria-hidden /> : <Bell aria-hidden />}
            </button>
          )}
        </li>
      ))}
    </ul>
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
