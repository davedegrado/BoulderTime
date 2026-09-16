import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ExternalLink, Globe, Layers, Mail, MapPin, Phone, Settings2 } from "lucide-react";
import { useGym, useSectors, type GymDetail } from "@/features/gyms/api";
import { GymAvatar } from "@/components/GymAvatar";
import { Badge } from "@/components/Badge";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { ApiError } from "@/lib/apiError";
import { NotFoundPage } from "@/pages/NotFoundPage";
import { gymStatusLabel } from "@/lib/format";
import { useBoulders, type BoulderFilters } from "@/features/boulders/api";
import { useGradeSystems } from "@/features/grading/api";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { BoulderFiltersBar } from "@/features/boulders/BoulderFilters";
import { Button } from "@/components/Button";
import { Mountain } from "lucide-react";

type Tab = "boulders" | "sectors" | "info";
const TAB_LABEL: Record<Tab, string> = { boulders: "Boulders", sectors: "Sectors", info: "Info" };

export function GymPage() {
  const { slug = "" } = useParams();
  const gym = useGym(slug);
  const [tab, setTab] = useState<Tab>("boulders");

  if (gym.isPending) return <LoadingState label="Loading gym" />;
  if (gym.isError) return gym.error instanceof ApiError && gym.error.isNotFound ? <NotFoundPage /> : <ErrorState error={gym.error} onRetry={() => gym.refetch()} />;

  const g = gym.data;
  return (
    <div className="page page--flush">
      <header className="gym-hero">
        <div className="gym-hero__cover" style={g.coverImageUrl ? { backgroundImage: `url(${g.coverImageUrl})` } : undefined} />
        <div className="gym-hero__body">
          <GymAvatar name={g.name} logoUrl={g.logoUrl} size={76} />
          <div className="gym-hero__row">
            <div className="gym-hero__text">
              <h1 className="page__title">{g.name}</h1>
              <p className="gym-hero__meta"><MapPin aria-hidden /> {g.city}</p>
              {g.status !== "ACTIVE" && <Badge tone="dark">{gymStatusLabel[g.status]} · only staff can see this</Badge>}
            </div>
            {g.viewerRole && (
              <Link to={`/manage/${g.slug}`} className="btn btn--secondary"><Settings2 aria-hidden /><span>Manage</span></Link>
            )}
          </div>
        </div>
      </header>

      <div className="tabs" role="tablist" aria-label="Gym sections">
        {(["boulders", "sectors", "info"] as Tab[]).map((t) => (
          <button key={t} role="tab" aria-selected={tab === t} className="tabs__tab" onClick={() => setTab(t)}>{TAB_LABEL[t]}</button>
        ))}
      </div>

      <div className="page__pad">
        {tab === "boulders" ? <BouldersTab gymId={g.id} /> : tab === "sectors" ? <SectorsTab gymId={g.id} /> : <InfoTab gym={g} />}
      </div>
    </div>
  );
}

function BouldersTab({ gymId }: { gymId: string }) {
  const [filters, setFilters] = useState<BoulderFilters>({});
  const sectors = useSectors(gymId);
  const systems = useGradeSystems(gymId);
  const boulders = useBoulders(gymId, filters);
  const items = boulders.data?.pages.flatMap((p) => p.items) ?? [];
  const total = boulders.data?.pages[0]?.total ?? 0;
  const filtered = Object.values(filters).some(Boolean);

  return (
    <div className="stack">
      <BoulderFiltersBar filters={filters} onChange={setFilters} sectors={sectors.data ?? []} systems={systems.data ?? []} />
      {boulders.isPending ? <LoadingState label="Loading boulders" />
        : boulders.isError ? <ErrorState error={boulders.error} onRetry={() => boulders.refetch()} />
        : items.length === 0 ? (
          <EmptyState icon={<Mountain />}
            title={filtered ? "No boulders match these filters" : "No boulders on the wall yet"}
            body={filtered ? "Try another sector, grade or hold colour." : "This gym hasn't added its current boulders yet."}
            action={filtered ? <Button variant="secondary" onClick={() => setFilters({})}>Clear filters</Button> : undefined} />
        ) : (
          <>
            <p className="section__meta">{total} {total === 1 ? "boulder" : "boulders"}</p>
            <div className="boulder-grid">{items.map((b) => <BoulderCard key={b.id} boulder={b} />)}</div>
            {boulders.hasNextPage && <Button variant="secondary" onClick={() => boulders.fetchNextPage()} loading={boulders.isFetchingNextPage}>Show more</Button>}
          </>
        )}
    </div>
  );
}

function SectorsTab({ gymId }: { gymId: string }) {
  const sectors = useSectors(gymId);
  if (sectors.isPending) return <LoadingState label="Loading sectors" />;
  if (sectors.isError) return <ErrorState error={sectors.error} onRetry={() => sectors.refetch()} />;
  if (sectors.data.length === 0) return <EmptyState icon={<Layers />} title="No sectors yet" body="This gym hasn't set up its sectors on BoulderTime." />;

  return (
    <ul className="list">
      {sectors.data.map((s) => (
        <li key={s.id} className={`list__row ${s.isActive ? "" : "list__row--muted"}`}>
          <span className="sector-mark" aria-hidden />
          <div className="list__main">
            <p className="list__title">{s.name}</p>
            {s.description && <p className="list__sub">{s.description}</p>}
          </div>
          {!s.isActive && <Badge>Hidden</Badge>}
        </li>
      ))}
    </ul>
  );
}

function InfoTab({ gym }: { gym: GymDetail }) {
  const mapsUrl = `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent([gym.name, gym.address, gym.city].filter(Boolean).join(", "))}`;
  return (
    <div className="stack">
      {gym.description && <p className="prose">{gym.description}</p>}
      <ul className="list">
        <li className="list__row">
          <MapPin className="list__icon" aria-hidden />
          <div className="list__main">
            <p className="list__title">{gym.address ?? gym.city}</p>
            {gym.address && <p className="list__sub">{gym.city}</p>}
          </div>
          <a className="icon-link" href={mapsUrl} target="_blank" rel="noreferrer" aria-label="Open in maps"><ExternalLink aria-hidden /></a>
        </li>
        {gym.website && (
          <li className="list__row">
            <Globe className="list__icon" aria-hidden />
            <a className="list__main list__link" href={gym.website} target="_blank" rel="noreferrer">{gym.website.replace(/^https?:\/\//, "")}</a>
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
