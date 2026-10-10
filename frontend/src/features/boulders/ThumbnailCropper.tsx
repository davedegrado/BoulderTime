import { useCallback, useEffect, useLayoutEffect, useRef, useState, type KeyboardEvent, type PointerEvent } from "react";
import { Check, X } from "lucide-react";
import type { CropRect } from "@/features/boulders/imageResize";
import { t } from "@/i18n/i18n";

/** The cards show their picture at 5:4; the frame has the same shape, so what staff see is what the cards get. */
export const CARD_ASPECT = 5 / 4;
const MAX_ZOOM = 4;

interface View { zoom: number; x: number; y: number }

/**
 * Picks the part of the boulder's photo the cards show. The frame stays still and the photo moves under it: drag to
 * move, pinch or the slider to zoom. The photo always fills the frame, so there is never an empty edge in a card.
 */
export function ThumbnailCropper({ src, onCancel, onDone }: { src: string; onCancel: () => void; onDone: (rect: CropRect) => void }) {
  const frame = useRef<HTMLDivElement>(null);
  const done = useRef<HTMLButtonElement>(null);
  const [natural, setNatural] = useState<{ w: number; h: number } | null>(null);
  const [box, setBox] = useState<{ w: number; h: number } | null>(null);
  const [view, setView] = useState<View>({ zoom: 1, x: 0, y: 0 });
  const pointers = useRef(new Map<number, { x: number; y: number }>());
  const pinch = useRef<{ distance: number; zoom: number } | null>(null);

  // The smallest scale at which the photo still covers the whole frame.
  const base = natural && box ? Math.max(box.w / natural.w, box.h / natural.h) : 1;

  /** Keeps the photo over the whole frame, whatever the zoom and position asked for. */
  const clamp = useCallback((v: View): View => {
    if (!natural || !box) return v;
    const zoom = Math.min(MAX_ZOOM, Math.max(1, v.zoom));
    const s = base * zoom;
    const minX = box.w - natural.w * s;
    const minY = box.h - natural.h * s;
    return { zoom, x: Math.min(0, Math.max(minX, v.x)), y: Math.min(0, Math.max(minY, v.y)) };
  }, [natural, box, base]);

  useLayoutEffect(() => {
    const el = frame.current;
    if (!el) return;
    const measure = () => setBox({ w: el.clientWidth, h: el.clientHeight });
    measure();
    if (typeof ResizeObserver === "undefined") return;
    const ro = new ResizeObserver(measure);
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  // Start centred, once both sizes are known.
  useEffect(() => {
    if (!natural || !box) return;
    const s = base;
    setView({ zoom: 1, x: (box.w - natural.w * s) / 2, y: (box.h - natural.h * s) / 2 });
  }, [natural, box, base]);

  useEffect(() => {
    done.current?.focus();
    const onKey = (e: globalThis.KeyboardEvent) => { if (e.key === "Escape") onCancel(); };
    window.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { window.removeEventListener("keydown", onKey); document.body.style.overflow = overflow; };
  }, [onCancel]);

  /** Zooms keeping the centre of the frame on the same spot of the photo. */
  function zoomTo(zoom: number, from: View = view) {
    if (!box) return;
    const s0 = base * from.zoom;
    const s1 = base * Math.min(MAX_ZOOM, Math.max(1, zoom));
    const cx = (box.w / 2 - from.x) / s0;
    const cy = (box.h / 2 - from.y) / s0;
    setView(clamp({ zoom, x: box.w / 2 - cx * s1, y: box.h / 2 - cy * s1 }));
  }

  function onPointerDown(e: PointerEvent<HTMLDivElement>) {
    e.currentTarget.setPointerCapture(e.pointerId);
    pointers.current.set(e.pointerId, { x: e.clientX, y: e.clientY });
    if (pointers.current.size === 2) {
      const [a, b] = [...pointers.current.values()];
      pinch.current = { distance: Math.hypot(a!.x - b!.x, a!.y - b!.y), zoom: view.zoom };
    }
  }
  function onPointerMove(e: PointerEvent<HTMLDivElement>) {
    const last = pointers.current.get(e.pointerId);
    if (!last) return;
    pointers.current.set(e.pointerId, { x: e.clientX, y: e.clientY });
    if (pointers.current.size >= 2 && pinch.current) {
      const [a, b] = [...pointers.current.values()];
      const distance = Math.hypot(a!.x - b!.x, a!.y - b!.y);
      zoomTo(pinch.current.zoom * (distance / pinch.current.distance));
      return;
    }
    setView((v) => clamp({ ...v, x: v.x + e.clientX - last.x, y: v.y + e.clientY - last.y }));
  }
  function onPointerUp(e: PointerEvent<HTMLDivElement>) {
    pointers.current.delete(e.pointerId);
    if (pointers.current.size < 2) pinch.current = null;
  }
  function onKeyDown(e: KeyboardEvent<HTMLDivElement>) {
    const step = 16;
    const moves: Record<string, [number, number]> = { ArrowLeft: [step, 0], ArrowRight: [-step, 0], ArrowUp: [0, step], ArrowDown: [0, -step] };
    const move = moves[e.key];
    if (!move) return;
    e.preventDefault();
    setView((v) => clamp({ ...v, x: v.x + move[0], y: v.y + move[1] }));
  }

  function confirm() {
    if (!natural || !box) return;
    const s = base * view.zoom;
    onDone({ x: Math.max(0, -view.x / s), y: Math.max(0, -view.y / s), width: box.w / s, height: box.h / s });
  }

  const s = base * view.zoom;
  return (
    <div className="cropper" role="dialog" aria-modal="true" aria-labelledby="cropper-title">
      <div className="cropper__top">
        <button type="button" className="viewer__close" onClick={onCancel} aria-label={t("Cancel")}><X aria-hidden /></button>
        <h2 id="cropper-title" className="cropper__title">{t("Card picture")}</h2>
        <button ref={done} type="button" className="viewer__close cropper__done" onClick={confirm} disabled={!natural} aria-label={t("Use this part")}><Check aria-hidden /></button>
      </div>
      <div className="cropper__stage">
        <div ref={frame} className="cropper__frame" style={{ aspectRatio: String(CARD_ASPECT) }} tabIndex={0}
          aria-label={t("Drag to choose what the cards show")}
          onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerCancel={onPointerUp} onKeyDown={onKeyDown}>
          <img src={src} alt="" draggable={false} onLoad={(e) => setNatural({ w: e.currentTarget.naturalWidth, h: e.currentTarget.naturalHeight })}
            style={natural ? { width: natural.w * s, height: natural.h * s, transform: `translate(${view.x}px, ${view.y}px)` } : { visibility: "hidden" }} />
        </div>
      </div>
      <div className="cropper__controls">
        <p className="cropper__hint">{t("Drag to move, pinch or slide to zoom. This is what the boulder's cards show.")}</p>
        <label className="cropper__zoom">
          <span className="sr-only">{t("Zoom")}</span>
          <input type="range" min={1} max={MAX_ZOOM} step={0.01} value={view.zoom} onChange={(e) => zoomTo(Number(e.target.value))} />
        </label>
        <button type="button" className="btn btn--primary btn--block" onClick={confirm} disabled={!natural}><Check aria-hidden /><span>{t("Use this part")}</span></button>
      </div>
    </div>
  );
}
