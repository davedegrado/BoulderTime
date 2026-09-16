import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Compass, Plus, SearchX } from "lucide-react";
import { useGymSearch } from "@/features/gyms/api";
import { GymCard } from "@/features/gyms/GymCard";
import { SearchField } from "@/components/SearchField";
import { Button } from "@/components/Button";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useDebounced } from "@/lib/useDebounced";

export function ExplorePage() {
  const [params, setParams] = useSearchParams();
  const [text, setText] = useState(params.get("q") ?? "");
  const query = useDebounced(text.trim(), 300);
  const search = useGymSearch(query);

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
        <p className="page__subtitle">Find where to climb by gym name or city.</p>
      </header>

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
          <p className="section__meta">{total} {total === 1 ? "gym" : "gyms"}{query && ` for "${query}"`}</p>
          <div className="gym-grid">{gyms.map((g) => <GymCard key={g.id} gym={g} />)}</div>
          {search.hasNextPage && (
            <Button variant="secondary" onClick={() => search.fetchNextPage()} loading={search.isFetchingNextPage}>Show more</Button>
          )}
          <p className="section__footnote">Missing a gym? <Link to="/gyms/suggest">Suggest it</Link></p>
        </section>
      )}
    </div>
  );
}
