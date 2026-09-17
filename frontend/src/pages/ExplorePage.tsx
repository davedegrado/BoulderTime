import { lazy, Suspense, useCallback, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Compass, LocateFixed, Map as MapIcon, Plus, SearchX } from "lucide-react";
import { useGymPins, useGymSearch, type Bounds } from "@/features/gyms/api";
import { GymCard } from "@/features/gyms/GymCard";
import { useUserLocation } from "@/features/map/useUserLocation";
import { SearchField } from "@/components/SearchField";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useDebounced } from "@/lib/useDebounced";

// Leaflet is loaded only when the map is shown.
const GymMap = lazy(() => import("@/features/map/GymMap").then((m) => ({ default: m.GymMap })));

export function ExplorePage() {
  const [params, setParams] = useSearchParams();
  const [text, setText] = useState(params.get("q") ?? "");
  const query = useDebounced(text.trim(), 300);
  const { location, locate } = useUserLocation();
  const here = location.status === "found" ? { lat: location.lat, lng: location.lng } : null;
  const [bounds, setBounds] = useState<Bounds | null>(null);
  const [focus, setFocus] = useState<{ lat: number; lng: number; zoom: number } | null>(null);
  const pins = useGymPins(bounds);
  const search = useGymSearch(query, here);

  const onBounds = useCallback((b: Bounds) => setBounds(b), []);

  function onChange(value: string) {
    setText(value);
    setParams(value ? { q: value } : {}, { replace: true });
  }

  const gyms = search.data?.pages.flatMap((p) => p.items) ?? [];
  const total = search.data?.pages[0]?.total ?? 0;

  return (
    <div className="page">
      <header className="page__header">
        <h1 className="page__title">Explore gyms</h1>
        <p className="page__subtitle">Find where to climb near you, or search by gym name or city.</p>
      </header>

      <section className="map-card" aria-label="Map of gyms">
        <Suspense fallback={<div className="gym-map gym-map--loading"><MapIcon aria-hidden /></div>}>
          <GymMap pins={pins.data ?? []} userPosition={here} focus={focus} onBounds={onBounds} />
        </Suspense>
        <button type="button" className="map-card__locate" onClick={() => { locate(); if (here) setFocus({ ...here, zoom: 13 }); }}
          aria-label="Center the map on my position">
          <LocateFixed aria-hidden />
        </button>
        {location.status === "denied" && (
          <p className="map-card__note">Location is off, so the map shows Italy. Allow location in your browser to see gyms near you.</p>
        )}
      </section>

      <SearchField label="Search gyms" placeholder="Gym name or city" value={text} onChange={onChange} />

      {search.isPending ? (
        <LoadingState label="Finding gyms" />
      ) : search.isError ? (
        <ErrorState error={search.error} onRetry={() => search.refetch()} />
      ) : gyms.length === 0 ? (
        <EmptyState
          icon={query ? <SearchX /> : <Compass />}
          title={query ? `No gyms match "${query}"` : "No gyms on BoulderTime yet"}
          body="Is your gym missing? Suggest it and we'll reach out to them."
          action={<Link to="/gyms/suggest" className="btn btn--primary"><Plus aria-hidden /><span>Suggest a gym</span></Link>}
        />
      ) : (
        <section className="section" aria-live="polite">
          <p className="section__meta">
            {total} {total === 1 ? "gym" : "gyms"}{query && ` for "${query}"`}{here && !query ? " · nearest first" : ""}
          </p>
          <div className="gym-grid">
            {gyms.map((g) => (
              <div key={g.id} className="gym-grid__item">
                <GymCard gym={g} />
                {g.latitude !== null && g.longitude !== null && (
                  <button type="button" className="text-btn" onClick={() => { setFocus({ lat: g.latitude!, lng: g.longitude!, zoom: 15 }); window.scrollTo({ top: 0, behavior: "smooth" }); }}>
                    <MapIcon aria-hidden /> Show on map{g.distanceKm != null && ` · ${g.distanceKm < 10 ? g.distanceKm.toFixed(1) : Math.round(g.distanceKm)} km`}
                  </button>
                )}
              </div>
            ))}
          </div>
          {search.hasNextPage && (
            <Button variant="secondary" onClick={() => search.fetchNextPage()} loading={search.isFetchingNextPage}>Show more</Button>
          )}
          <p className="section__footnote">Missing a gym? <Link to="/gyms/suggest">Suggest it</Link></p>
        </section>
      )}
    </div>
  );
}
