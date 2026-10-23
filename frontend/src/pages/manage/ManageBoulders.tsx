import { useState } from "react";
import { Link } from "react-router-dom";
import { CheckSquare, History, Mountain, Plus, RotateCcw, Trash2, X } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useBoulders, useRemoveBoulders, useRestoreBoulder, type RemoveResult } from "@/features/boulders/api";
import { useSectors } from "@/features/gyms/api";
import { useGradeSystems } from "@/features/grading/api";
import { useGymTags } from "@/features/boulders/tags";
import { BoulderCard } from "@/features/boulders/BoulderCard";
import { DeleteBoulderButton } from "@/features/boulders/DeleteBoulderButton";
import { useRememberedFilters } from "@/features/boulders/rememberedFilters";
import { BoulderRow, SortMenu, sortFor, useBoulderView, useRememberedSort, ViewToggle } from "@/features/boulders/listView";
import { activeFilters, boulderCount, FiltersPanel, FiltersToggle } from "@/features/boulders/BoulderFilters";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { plural, t } from "@/i18n/i18n";
import { atLeast } from "@/features/staff/roles";

export function ManageBoulders() {
  const { gym } = useManagedGym();
  const [filters, setFilters] = useRememberedFilters(`bt:manage-boulder-filters:${gym.id}`, () => ({ status: "ACTIVE" }));
  const [selecting, setSelecting] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [lastRemoval, setLastRemoval] = useState<RemoveResult | null>(null);
  const [notifyFollowers, setNotifyFollowers] = useState(true);
  const [showFilters, setShowFilters] = useState(false);
  const [sort, setSort] = useRememberedSort(`bt:manage-boulder-sort:${gym.id}`);
  const [view, setView] = useBoulderView();
  const sectors = useSectors(gym.id);
  const systems = useGradeSystems(gym.id);
  const tags = useGymTags(gym.id);
  const boulders = useBoulders(gym.id, filters, sortFor(sort, filters.status === "REMOVED"));
  const remove = useRemoveBoulders(gym.id);
  const restore = useRestoreBoulder();
  const toast = useToast();

  const removedView = filters.status === "REMOVED";
  const items = boulders.data?.pages.flatMap((p) => p.items) ?? [];
  const total = boulders.data?.pages[0]?.total ?? 0;
  const noSetup = (sectors.data && sectors.data.filter((s) => s.isActive).length === 0) || (systems.data && systems.data.filter((s) => s.isActive).length === 0);

  const toggle = (id: string) => setSelected((s) => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; });
  const stopSelecting = () => { setSelecting(false); setSelected(new Set()); };
  const selectAllLoaded = () => setSelected(new Set(items.map((b) => b.id)));

  function removeSelected() {
    remove.mutate({ boulderIds: [...selected], notifyFollowers }, {
      onSuccess: (result) => { setLastRemoval(result); stopSelecting(); },
      onError: (e) => toast.error(errorMessage(e)),
    });
  }

  /** On the wall or taken down: a different list, so a selection or a removal notice from the other one goes. */
  function switchView(status: "ACTIVE" | "REMOVED", next = filters) {
    stopSelecting();
    setLastRemoval(null);
    setFilters({ ...next, status });
  }

  return (
    <div className="stack">
      {lastRemoval && (
        <div className="notice notice--success" role="status">
          <History aria-hidden />
          <div>
            <p><strong>{plural(lastRemoval.removed, "{count} boulder removed", "{count} boulders removed")}</strong> — {lastRemoval.sectors.map((s) => `${s.sectorName} (${s.removed})`).join(", ")}.</p>
            <p className="list__sub">{t("They stay in climbers' history.")}{notifyFollowers ? " " + t("Followers got one notification per sector.") : ""}</p>
            {lastRemoval.sectors[0] && (
              <Link className="section__link" to={`/manage/${gym.slug}/announcements?new=1&sectorId=${lastRemoval.sectors[0].sectorId}&title=${encodeURIComponent(t("{sector} has been retraced", { sector: lastRemoval.sectors[0].sectorName }))}`}>
                {t("Write an update about it")}
              </Link>
            )}
          </div>
          <button type="button" className="icon-btn" onClick={() => setLastRemoval(null)} aria-label={t("Dismiss")}><X aria-hidden /></button>
        </div>
      )}

      {noSetup && (
        <EmptyState icon={<Mountain />} title={t("Set up sectors and grading first")}
          body={t("Every boulder needs a sector and at least one official grade.")}
          action={<div className="form__actions">
            <Link to={`/manage/${gym.slug}/sectors`} className="btn btn--secondary"><span>{t("Sectors")}</span></Link>
            <Link to={`/manage/${gym.slug}/grading`} className="btn btn--secondary"><span>{t("Grading")}</span></Link>
          </div>} />
      )}

      {/* As on the gym's page — how many, the filters (on the wall or taken down among them), the order, the view — with
          the button to select boulders. */}
      <div className="boulders-toolbar boulders-toolbar--one">
        <span className="section__meta boulders-toolbar__count">{!boulders.isPending && !boulders.isError && items.length > 0 ? boulderCount(total, removedView) : null}</span>
        {!removedView && !selecting && items.length > 0 && (
          <button type="button" className="icon-btn icon-btn--outlined icon-btn--sm" onClick={() => setSelecting(true)} aria-label={t("Select")} title={t("Select")}>
            <CheckSquare aria-hidden />
          </button>
        )}
        <FiltersToggle open={showFilters} count={activeFilters(filters, sectors.data ?? [], systems.data ?? [], tags.data ?? []).length + (removedView ? 1 : 0)}
          onToggle={() => setShowFilters((v) => !v)} />
        <SortMenu value={sort} onChange={setSort} removed={removedView} />
        <ViewToggle value={view} onChange={setView} />
      </div>
      {/* The one thing staff do most here, always under the thumb, however far down the list. */}
      {!removedView && !selecting && !noSetup && (
        <Link to={`/manage/${gym.slug}/boulders/new`} className="fab" aria-label={t("New boulder")} title={t("New boulder")}><Plus aria-hidden /></Link>
      )}
      <FiltersPanel open={showFilters} filters={filters} sectors={sectors.data ?? []} systems={systems.data ?? []} tags={tags.data ?? []} withStatus
        onChange={(next) => ((next.status === "REMOVED") !== removedView
          ? switchView(next.status === "REMOVED" ? "REMOVED" : "ACTIVE", next)
          : setFilters({ ...next, status: filters.status }))} />

      {boulders.isPending ? <LoadingState label={t("Loading boulders")} />
        : boulders.isError ? <ErrorState error={boulders.error} onRetry={() => boulders.refetch()} />
        : items.length === 0 ? <EmptyState icon={<Mountain />} title={removedView ? t("No removed boulders") : t("No boulders on the wall")} />
        : (
          <>
            {view === "list" ? (
              <ul className="boulder-rows">
                {items.map((b) => (
                  <li key={b.id}>
                    <BoulderRow boulder={b} to={removedView ? undefined : `/manage/${gym.slug}/boulders/${b.id}/edit`}
                      selectable={selecting} selected={selected.has(b.id)} onToggle={() => toggle(b.id)} />
                    {/* Taken down: every row carries the same two actions, so the rows stay alike. */}
                    {removedView && (
                      <div className="boulder-rows__actions">
                        <Button variant="secondary" icon={<RotateCcw aria-hidden />} loading={restore.isPending && restore.variables === b.id}
                          onClick={() => restore.mutate(b.id, { onSuccess: () => toast.success(t("Boulder restored")), onError: (e) => toast.error(errorMessage(e)) })}>
                          {t("Restore")}
                        </Button>
                        {atLeast(gym.viewerRole, "ADMIN") && <DeleteBoulderButton boulderId={b.id} />}
                      </div>
                    )}
                  </li>
                ))}
              </ul>
            ) : (
              <div className="boulder-grid">
                {items.map((b) => removedView ? (
                  <div key={b.id} className="boulder-grid__item">
                    <BoulderCard boulder={b} />
                    <Button variant="secondary" icon={<RotateCcw aria-hidden />} loading={restore.isPending && restore.variables === b.id}
                      onClick={() => restore.mutate(b.id, { onSuccess: () => toast.success(t("Boulder restored")), onError: (e) => toast.error(errorMessage(e)) })}>
                      {t("Restore")}
                    </Button>
                    {atLeast(gym.viewerRole, "ADMIN") && <DeleteBoulderButton boulderId={b.id} />}
                  </div>
                ) : (
                  <BoulderCard key={b.id} boulder={b} to={`/manage/${gym.slug}/boulders/${b.id}/edit`}
                    selectable={selecting} selected={selected.has(b.id)} onToggle={() => toggle(b.id)}
                    onLongPress={() => { setSelecting(true); setSelected(new Set([b.id])); }} />
                ))}
              </div>
            )}
            {boulders.hasNextPage && <Button variant="secondary" onClick={() => boulders.fetchNextPage()} loading={boulders.isFetchingNextPage}>{t("Show more")}</Button>}
          </>
        )}

      {selecting && (
        <div className="selection-bar" role="region" aria-label={t("Selection")}>
          <span className="selection-bar__count">{t("{count} selected", { count: selected.size })}</span>
          <label className="selection-bar__notify">
            <input type="checkbox" checked={notifyFollowers} onChange={(e) => setNotifyFollowers(e.target.checked)} />
            <span>{t("Notify followers")}</span>
          </label>
          <Button variant="on-dark" onClick={selectAllLoaded}>{t("All")}</Button>
          <Button variant="on-dark" onClick={stopSelecting}>{t("Cancel")}</Button>
          <ConfirmButton variant="danger" icon={<Trash2 aria-hidden />} confirmLabel={t("Remove {count}?", { count: selected.size })}
            disabled={selected.size === 0} loading={remove.isPending} onConfirm={removeSelected}>
            {t("Remove")}
          </ConfirmButton>
        </div>
      )}
    </div>
  );
}
