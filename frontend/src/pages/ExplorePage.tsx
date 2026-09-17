import { lazy, Suspense, useCallback, useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Compass, List, LocateFixed, Map as MapIcon, Plus, SearchX } from "lucide-react";
import { useGymPins, useGymSearch, type Bounds } from "@/features/gyms/api";
import { GymCard } from "@/features/gyms/GymCard";
import { useUserLocation } from "@/features/map/useUserLocation";
import { SearchField } from "@/components/SearchField";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useDebounced } from "@/lib/useDebounced";

// Leaflet loads only when the map view is opened.
const GymMap = lazy(() => import("@/features/map/GymMap").then((m) => ({ default: m.GymMap })));

type View = "list" | "map";

export function ExplorePage() {
  const [params, setParams] = useSearchParams();
  const view: View = params.get("view") === "map" ? "map" : "list";
  const [text, setText] = useState(params.get("q") ?? "");
  const query = useDebounced(text.trim(), 300);
  // The position is only requested when the map is opened; a previously known one still sorts the list.
  const { location, locate } = useUserLocation();
  const here = location.status === "found" ? { lat: location.lat, lng: location.lng } : null;
  const [bounds, setBounds] = useState<Bounds | null>(null);
  const [focus, setFocus] = useState<{ lat: number; lng: number; zoom: number } | null>(null);
  const pins = useGymPins(view === "map" ? bounds : null);
  const search = useGymSearch(query, here);

  useEffect(() => { if (view === "map") locate(); }, [view, locate]);

  const onBounds = useCallback((b: Bounds) => setBounds(b), []);
  const setView = (next: View) => setParams((p) => { next === "map" ? p.set("view", "map") : p.delete("view"); return p; }, { replace: true });

  function onQuery(value: string) {
    setText(value);
    setParams((p) => { value ? p.set("q", value) : p.delete("q"); return p; }, { replace: true });
  }

  function showOnMap(gym: { latitude: number | null; longitude: number | null }) {
    if (gym.latitude === null || gym.longitude === null) return;
    setFocus({ lat: gym.latitude, lng: gym.longitude, zoom: 15 });
    setView("map");
  }

  const gyms = search.data?.pages.flatMap((p) => p.items) ?? [];
  const total = search.data?.pages[0]?.total ?? 0;

  return (
    <div className="page">
      <header className="page__header">
        <h1 className="page__title">Explore gyms</h1>
        <p className="page__subtitle">Search by gym name or city, or switch to the map to see what's around you.</p>
      </header>

      <SearchField label="Search gyms" placeholder="Gym name or city" value={text} onChange={onQuery} />

      <div className="chips" role="radiogroup" aria-label="View">
        <button role="radio" aria-checked={view === "list"} className="chip" onClick={() => setView("list")}><List aria-hidden /> List</button>
        <button role="radio" aria-checked={view === "map"} className="chip" onClick={() => setView("map")}><MapIcon aria-hidden /> Map</button>
      </div>

      {view === "map" ? (
        <section className="map-card" aria-label="Map of gyms">
          <Suspense fallback={<div className="gym-map gym-map--loading"><MapIcon aria-hidden /></div>}>
            <GymMap pins={pins.data ?? []} userPosition={here} focus={focus} onBounds={onBounds} />
          </Suspense>
          <button type="button" className="map-card__locate" onClick={() => { locate(); if (here) setFocus({ ...here, zoom: 13 }); }}
            aria-label="Center the map on my position">
            <LocateFixed aria-hidden />
          </button>
          {location.status === "locating" && <p className="map-card__note">Finding your position…</p>}
          {location.status === "denied" && <p className="map-card__note">Location is off, so the map shows Italy. Allow location in your browser to see gyms near you.</p>}
        </section>
      ) : search.isPending ? (
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
                  <button type="button" className="text-btn" onClick={() => showOnMap(g)}>
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
