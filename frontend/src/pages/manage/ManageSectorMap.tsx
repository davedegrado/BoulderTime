import { useEffect, useRef, useState, type MouseEvent, type PointerEvent } from "react";
import { Link } from "react-router-dom";
import { ArrowLeft, Check, ImagePlus, Map as MapIcon, RotateCcw, Save, Trash2, Undo2 } from "lucide-react";
import { useManagedGym } from "@/pages/manage/ManageLayout";
import { useSectors, useSetSectorZone, type MapPoint, type Sector } from "@/features/gyms/api";
import { useSetFloorPlan } from "@/features/images/api";
import { floorPlanOf, type FloorPlan } from "@/features/gyms/SectorMap";
import { MediaInput, type MediaInputHandle } from "@/components/MediaInput";
import { Button } from "@/components/Button";
import { ConfirmButton } from "@/components/ConfirmButton";
import { EmptyState, ErrorState, LoadingState } from "@/components/States";
import { useToast } from "@/components/Toast";
import { errorMessage } from "@/lib/apiError";
import { t } from "@/i18n/i18n";

interface Draft { points: MapPoint[]; closed: boolean; label: MapPoint | null; showLabel: boolean; dirty: boolean }
const EMPTY: Draft = { points: [], closed: false, label: null, showLabel: true, dirty: false };
const draftOf = (s: Sector | undefined): Draft =>
  s?.zone ? { points: s.zone.points, closed: true, label: s.zone.label, showLabel: !s.zone.hideLabel, dirty: false } : EMPTY;

/**
 * Staff put the gym's floor plan in and draw each sector on it: pick a sector, tap the corners of its area, tap the
 * first corner again to close it. Corners and the label can be dragged afterwards. Everything is kept in fractions of
 * the plan's size, so a new plan of the same room still fits.
 */
export function ManageSectorMap() {
  const { gym } = useManagedGym();
  const sectors = useSectors(gym.id);
  const setPlan = useSetFloorPlan(gym);
  const toast = useToast();
  const fileInput = useRef<MediaInputHandle>(null);
  const plan = floorPlanOf(gym);

  function upload(file: File | undefined) {
    if (!file) return;
    setPlan.mutate(file, { onSuccess: () => toast.success(t("Floor plan saved")), onError: (e) => toast.error(errorMessage(e)) });
  }

  return (
    <div className="stack">
      <div className="editor__head">
        <Link to={`/manage/${gym.slug}/sectors`} className="manage-head__back" aria-label={t("Back to sectors")}><ArrowLeft aria-hidden /></Link>
        <h2 className="section__title">{t("Sector map")}</h2>
      </div>
      <MediaInput ref={fileInput} kind="image" accept="image/jpeg,image/png,image/webp,image/*" onFile={upload} />

      {!plan ? (
        <EmptyState icon={<MapIcon />} title={t("Add your gym's floor plan")}
          body={t("A drawing or a photo of the plan, seen from above. You'll draw each sector on it, and climbers tap them to find their boulders.")}
          action={<Button icon={<ImagePlus aria-hidden />} loading={setPlan.isPending} onClick={() => fileInput.current?.open()}>{t("Upload the floor plan")}</Button>} />
      ) : sectors.isPending ? <LoadingState label={t("Loading sectors")} />
        : sectors.isError ? <ErrorState error={sectors.error} onRetry={() => sectors.refetch()} />
        : (
          <>
            <ZoneEditor plan={plan} sectors={sectors.data.filter((s) => s.isActive)} gymId={gym.id} />
            <div className="plan-actions">
              <Button variant="secondary" icon={<ImagePlus aria-hidden />} loading={setPlan.isPending} onClick={() => fileInput.current?.open()}>{t("Replace the plan")}</Button>
              <ConfirmButton variant="ghost" icon={<Trash2 aria-hidden />} confirmLabel={t("Remove the plan?")}
                onConfirm={() => setPlan.mutate(null, { onError: (e) => toast.error(errorMessage(e)) })}>{t("Remove the plan")}</ConfirmButton>
            </div>
            <p className="section__footnote">{t("A new plan keeps the sectors drawn on it: if it shows the room the same way, they still fit; if not, check them here.")}</p>
          </>
        )}
    </div>
  );
}

function ZoneEditor({ plan, sectors, gymId }: { plan: FloorPlan; sectors: Sector[]; gymId: string }) {
  const save = useSetSectorZone(gymId);
  const toast = useToast();
  const [selectedId, setSelectedId] = useState<string | null>(() => sectors.find((s) => !s.zone)?.id ?? sectors[0]?.id ?? null);
  const selected = sectors.find((s) => s.id === selectedId);
  const [draft, setDraft] = useState<Draft>(() => draftOf(selected));
  const [zoom, setZoom] = useState(1);
  const canvas = useRef<HTMLDivElement>(null);
  const dragging = useRef<{ index: number | "label"; moved: boolean } | null>(null);
  const justDragged = useRef(false);

  // Picking another sector starts from what is saved for it.
  useEffect(() => { setDraft(draftOf(sectors.find((s) => s.id === selectedId))); }, [selectedId]); // eslint-disable-line react-hooks/exhaustive-deps

  const { width: w, height: h } = plan;
  const others = sectors.filter((s) => s.id !== selectedId && s.zone);
  const missing = sectors.filter((s) => !s.zone);

  /** Where a pointer is on the plan, in fractions of its size, kept on the plan. */
  function at(clientX: number, clientY: number): MapPoint {
    const r = canvas.current!.getBoundingClientRect();
    const clamp = (v: number) => Math.min(1, Math.max(0, v));
    return { x: clamp((clientX - r.left) / r.width), y: clamp((clientY - r.top) / r.height) };
  }

  function addPoint(e: MouseEvent<HTMLDivElement>) {
    if (justDragged.current) { justDragged.current = false; return; }
    if (!selected || draft.closed || draft.points.length >= 64) return;
    const p = at(e.clientX, e.clientY);
    setDraft((d) => ({ ...d, points: [...d.points, p], dirty: true }));
  }

  function startDrag(e: PointerEvent<HTMLElement>, index: number | "label") {
    e.stopPropagation();
    e.currentTarget.setPointerCapture?.(e.pointerId);
    dragging.current = { index, moved: false };
  }
  function drag(e: PointerEvent<HTMLElement>) {
    const d = dragging.current;
    if (!d) return;
    d.moved = true;
    const p = at(e.clientX, e.clientY);
    setDraft((cur) => d.index === "label"
      ? { ...cur, label: p, dirty: true }
      : { ...cur, points: cur.points.map((q, i) => (i === d.index ? p : q)), dirty: true });
  }
  function endDrag(e: PointerEvent<HTMLElement>, index: number | "label") {
    const d = dragging.current;
    dragging.current = null;
    if (!d) return;
    e.stopPropagation();
    if (d.moved) { justDragged.current = true; return; }
    // A tap (no drag) on the first corner closes the outline.
    if (index === 0 && !draft.closed && draft.points.length >= 3) {
      justDragged.current = true;
      setDraft((cur) => ({ ...cur, closed: true, dirty: true }));
    }
  }

  function saveZone() {
    if (!selected || !draft.closed) return;
    save.mutate({ sectorId: selected.id, points: draft.points, label: draft.label, hideLabel: !draft.showLabel }, {
      onSuccess: () => {
        toast.success(t("“{name}” is on the map", { name: selected.name }));
        setDraft((d) => ({ ...d, dirty: false }));
        // On to the next sector still to draw, so a whole gym goes in one sitting.
        const next = sectors.find((s) => s.id !== selected.id && !s.zone);
        if (next) setSelectedId(next.id);
      },
      onError: (e) => toast.error(errorMessage(e)),
    });
  }

  function removeZone() {
    if (!selected) return;
    save.mutate({ sectorId: selected.id, points: [] }, {
      onSuccess: () => setDraft(EMPTY),
      onError: (e) => toast.error(errorMessage(e)),
    });
  }

  const centre = draft.points.length ? { x: draft.points.reduce((a, p) => a + p.x, 0) / draft.points.length, y: draft.points.reduce((a, p) => a + p.y, 0) / draft.points.length } : null;
  const label = draft.label ?? centre;
  const hint = !selected ? t("Choose a sector to draw.")
    : !draft.closed ? (draft.points.length < 3 ? t("Tap the plan at the corners of {name}.", { name: selected.name }) : t("Tap the first corner to close the outline."))
    : t("Drag the corners to adjust, or the label to move it. Then save.");

  return (
    <section className="zone-editor" aria-label={t("Draw the sectors")}>
      <div className="chips zone-editor__sectors" role="radiogroup" aria-label={t("Sector to draw")}>
        {sectors.map((s) => (
          <button key={s.id} type="button" role="radio" aria-checked={s.id === selectedId} className="chip" onClick={() => setSelectedId(s.id)}>
            {s.zone && <Check aria-hidden className="chip__check" />}{s.name}
          </button>
        ))}
      </div>
      <p className="zone-editor__hint" aria-live="polite">{hint}</p>

      <div className="zone-editor__stage">
        <div className="zone-editor__inner" style={{ width: `${zoom * 100}%` }}>
          <div ref={canvas} className={`zone-editor__canvas ${selected && !draft.closed ? "is-drawing" : ""}`} style={{ aspectRatio: `${w} / ${h}` }} onClick={addPoint}>
            <img src={plan.url} alt={t("Floor plan")} draggable={false} />
            <svg viewBox={`0 0 ${w} ${h}`} preserveAspectRatio="none" aria-hidden>
              {others.map((s) => (
                <polygon key={s.id} className="zone-editor__other" points={s.zone!.points.map((p) => `${p.x * w},${p.y * h}`).join(" ")} />
              ))}
              {draft.points.length > 1 && (draft.closed
                ? <polygon className="zone-editor__draft" points={draft.points.map((p) => `${p.x * w},${p.y * h}`).join(" ")} />
                : <polyline className="zone-editor__draft is-open" points={draft.points.map((p) => `${p.x * w},${p.y * h}`).join(" ")} />)}
            </svg>
            {others.map((s) => (
              <span key={s.id} className={`zone-editor__other-label ${s.zone!.hideLabel ? "is-hidden" : ""}`} style={{ left: `${s.zone!.label.x * 100}%`, top: `${s.zone!.label.y * 100}%` }}>{s.name}</span>
            ))}
            {draft.points.map((p, i) => (
              <button key={i} type="button" className={`zone-editor__corner ${i === 0 && !draft.closed && draft.points.length >= 3 ? "is-first" : ""}`}
                style={{ left: `${p.x * 100}%`, top: `${p.y * 100}%` }}
                aria-label={i === 0 && !draft.closed ? t("First corner: tap to close") : t("Corner {n}", { n: i + 1 })}
                onPointerDown={(e) => startDrag(e, i)} onPointerMove={drag} onPointerUp={(e) => endDrag(e, i)} onClick={(e) => e.stopPropagation()} />
            ))}
            {selected && draft.closed && label && (
              <span className={`zone-editor__label ${draft.showLabel ? "" : "is-hidden"}`} style={{ left: `${label.x * 100}%`, top: `${label.y * 100}%` }}
                onPointerDown={(e) => startDrag(e, "label")} onPointerMove={drag} onPointerUp={(e) => endDrag(e, "label")} onClick={(e) => e.stopPropagation()}>
                {selected.name}
              </span>
            )}
          </div>
        </div>
      </div>

      {selected && draft.closed && (
        <label className="check check--plain">
          <input type="checkbox" checked={draft.showLabel} onChange={(e) => { const showLabel = e.target.checked; setDraft((d) => ({ ...d, showLabel, dirty: true })); }} />
          <span className="check__text">
            <strong>{t("Show the name on the map")}</strong>
            <span className="field__hint">{t("Turn it off when names overlap: climbers still see it by tapping the sector.")}</span>
          </span>
        </label>
      )}

      <label className="zone-editor__zoom">
        <span>{t("Zoom")}</span>
        <input type="range" min={1} max={3} step={0.1} value={zoom} onChange={(e) => setZoom(Number(e.target.value))} />
      </label>

      <div className="zone-editor__actions">
        {!draft.closed && draft.points.length > 0 && (
          <Button variant="secondary" icon={<Undo2 aria-hidden />} onClick={() => setDraft((d) => ({ ...d, points: d.points.slice(0, -1) }))}>{t("Undo last corner")}</Button>
        )}
        {draft.points.length > 0 && (
          <Button variant="ghost" icon={<RotateCcw aria-hidden />} onClick={() => setDraft({ ...EMPTY, dirty: true })}>{t("Start again")}</Button>
        )}
        {selected?.zone && !draft.dirty && (
          <Button variant="ghost" icon={<Trash2 aria-hidden />} loading={save.isPending} onClick={removeZone}>{t("Take off the map")}</Button>
        )}
        <Button icon={<Save aria-hidden />} disabled={!draft.closed || !draft.dirty} loading={save.isPending} onClick={saveZone}>{t("Save the sector")}</Button>
      </div>

      {missing.length > 0 && (
        <p className="section__footnote">{t("Not on the map yet: {names}.", { names: missing.map((s) => s.name).join(", ") })}</p>
      )}
    </section>
  );
}
