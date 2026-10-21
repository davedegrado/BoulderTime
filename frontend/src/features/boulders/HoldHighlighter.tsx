import { useEffect, useLayoutEffect, useRef, useState, type PointerEvent, type WheelEvent } from "react";
import { Check, Eraser, Eye, Hand, Maximize, Paintbrush, RotateCcw, Undo2, Wand2, X, ZoomIn, ZoomOut } from "lucide-react";
import { HOLD_COLORS, holdColorName, type HoldColor } from "@/features/boulders/holdColors";
import { canDetect, compose, composeArea, detectHolds, featherMask, paintStroke } from "@/features/boulders/holdHighlight";
import { decodeImage } from "@/features/boulders/imageResize";
import { clampView, fitted, fitView, MAX_ZOOM, pan, pinch, zoomAt, type Point, type Size, type View } from "@/features/boulders/zoomView";
import { Button } from "@/components/Button";
import { t } from "@/i18n/i18n";

/** A photo the holds can be highlighted on: a sector's wall, or the photo just picked for the boulder. */
export interface HighlightSource { id: string; label: string; load: () => Promise<Blob> }

/**
 * Painted at the size the result is saved at: zoomed in on a small hold, the edge painted is the edge saved, not a
 * blurred enlargement of a smaller copy. Wall photos are stored at 1600 px at most, so this is the photo as it is.
 */
const WORK_SIDE = 1600;
const UNDO_LIMIT = 12;
/** Brush diameter in screen pixels: the same size under the finger whatever the zoom, so zooming in paints finer. */
const BRUSH_MIN = 6, BRUSH_MAX = 64, BRUSH_DEFAULT = 22;

type Phase = { name: "setup" } | { name: "working" } | { name: "edit" } | { name: "saving" } | { name: "error"; message: string };
type Tool = "add" | "remove" | "move";

const sizeOf = (image: CanvasImageSource) =>
  "naturalWidth" in image ? { w: image.naturalWidth, h: image.naturalHeight } : { w: (image as ImageBitmap).width, h: (image as ImageBitmap).height };

/** Draws a decoded picture into a new canvas no larger than `side` on its longest edge, and reads its pixels. */
function pixelsOf(image: CanvasImageSource, side: number) {
  const { w, h } = sizeOf(image);
  const scale = Math.min(1, side / Math.max(w, h));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(w * scale));
  canvas.height = Math.max(1, Math.round(h * scale));
  const ctx = canvas.getContext("2d");
  if (!ctx) throw new Error(t("This device can't edit pictures."));
  ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
  return { canvas, ctx, data: ctx.getImageData(0, 0, canvas.width, canvas.height) };
}

/**
 * "Highlight the holds": staff pick a photo of the wall and the holds' colour; the holds of that colour stay in colour
 * and the rest turns grey. The automatic part is a first guess — the same colour on the wall may be another boulder's —
 * so staff then paint colour in or out with a brush, with undo, back to the automatic guess, and a look at the
 * original. Two fingers zoom and move the photo (one finger paints), so small holds on a photo of a whole sector can
 * be painted precisely. The result becomes the boulder's photo only when the form is saved; cancelling leaves the
 * form as it was.
 */
export function HoldHighlighter({ sources, initialSourceId, initialColor, onCancel, onDone }: {
  sources: HighlightSource[];
  initialSourceId?: string;
  initialColor: HoldColor | "";
  onCancel: () => void;
  onDone: (photo: Blob, color: HoldColor) => void;
}) {
  const [sourceId, setSourceId] = useState(initialSourceId ?? sources[0]?.id ?? "");
  const [color, setColor] = useState<HoldColor | "">(initialColor);
  const [phase, setPhase] = useState<Phase>({ name: "setup" });
  const [tool, setTool] = useState<Tool>("add");
  const [brush, setBrush] = useState(BRUSH_DEFAULT);
  const [showOriginal, setShowOriginal] = useState(false);
  const [undoCount, setUndoCount] = useState(0);
  const [view, setView] = useState<View>({ scale: 1, x: 0, y: 0 });
  const [base, setBase] = useState<Size>({ w: 0, h: 0 });
  const [ring, setRing] = useState<Point | null>(null);

  const box = useRef<HTMLDivElement>(null);
  const canvas = useRef<HTMLCanvasElement>(null);
  const work = useRef<{ blob: Blob; src: ImageData; out: ImageData; mask: Uint8Array; auto: Uint8Array; undo: Uint8Array[]; color: HoldColor } | null>(null);
  const showOriginalRef = useRef(false);
  const viewRef = useRef(view);
  viewRef.current = view;
  // Photos already fetched, so changing the colour or trying again doesn't download the same wall twice.
  const fetched = useRef(new Map<string, Blob>());
  // Fingers (or the mouse) on the photo, and what they are doing.
  const pointers = useRef(new Map<number, Point>());
  const gesture = useRef<
    | { kind: "paint"; last: Point }
    | { kind: "pan"; last: Point }
    | { kind: "pinch"; start: { view: View; mid: Point; spread: number } }
    | null
  >(null);
  const frame = useRef(0);
  const dirty = useRef<{ x0: number; y0: number; x1: number; y1: number } | null>(null);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onCancel(); };
    window.addEventListener("keydown", onKey);
    return () => { window.removeEventListener("keydown", onKey); window.cancelAnimationFrame(frame.current); };
  }, [onCancel]);

  const boxSize = (): Size => ({ w: box.current?.clientWidth ?? 0, h: box.current?.clientHeight ?? 0 });

  /** Fits the photo in the box, and again whenever the box changes size (the phone turns). */
  useLayoutEffect(() => {
    if (phase.name !== "edit" && phase.name !== "saving") return;
    const fit = () => {
      const w = work.current;
      const size = boxSize();
      if (!w || !size.w || !size.h) return;
      const b = fitted({ w: w.src.width, h: w.src.height }, size);
      setBase(b);
      setView((v) => (v.scale === 1 ? fitView(b, size) : clampView(v, b, size)));
    };
    fit();
    const observer = typeof ResizeObserver === "function" ? new ResizeObserver(fit) : null;
    if (box.current) observer?.observe(box.current);
    return () => observer?.disconnect();
  }, [phase.name]);

  /** Redraws everything: after the guess, an undo, a new colour, the original/result switch. */
  function drawAll() {
    const w = work.current, el = canvas.current;
    const ctx = el?.getContext("2d");
    if (!w || !el || !ctx) return;
    if (el.width !== w.src.width || el.height !== w.src.height) { el.width = w.src.width; el.height = w.src.height; }
    if (showOriginalRef.current) { ctx.putImageData(w.src, 0, 0); return; }
    composeArea(w.src.data, w.src.width, w.mask, w.out.data, 0, 0, w.src.width, w.src.height);
    ctx.putImageData(w.out, 0, 0);
  }
  /** Redraws only what the brush has touched since the last frame. */
  function drawDirty() {
    const w = work.current, d = dirty.current, ctx = canvas.current?.getContext("2d");
    dirty.current = null;
    if (!w || !d || !ctx || showOriginalRef.current) return;
    composeArea(w.src.data, w.src.width, w.mask, w.out.data, d.x0, d.y0, d.x1, d.y1);
    ctx.putImageData(w.out, 0, 0, d.x0, d.y0, d.x1 - d.x0, d.y1 - d.y0);
  }
  useEffect(() => { showOriginalRef.current = showOriginal; drawAll(); }, [showOriginal, phase.name]);

  async function generate() {
    const source = sources.find((s) => s.id === sourceId);
    if (!source || !color) return;
    setPhase({ name: "working" });
    // A moment for the spinner to show before the device gets busy.
    await new Promise((r) => window.setTimeout(r, 30));
    try {
      const blob = fetched.current.get(sourceId) ?? await source.load();
      fetched.current.set(sourceId, blob);
      const picture = await decodeImage(blob).catch(() => { throw new Error(t("This photo can't be opened here. Use a JPEG, PNG or WebP photo.")); });
      const { ctx, data } = pixelsOf(picture.image, WORK_SIDE);
      picture.release();
      const auto = detectHolds(data.data, data.width, data.height, color);
      work.current = { blob, src: data, out: ctx.createImageData(data.width, data.height), mask: auto.slice(), auto, undo: [], color };
      setUndoCount(0);
      setShowOriginal(false);
      setView({ scale: 1, x: 0, y: 0 });
      setPhase({ name: "edit" });
    } catch (e) {
      setPhase({ name: "error", message: e instanceof Error && e.message ? e.message : t("Couldn't prepare the picture. Try again.") });
    }
  }

  function remember() {
    const w = work.current;
    if (!w) return;
    w.undo.push(w.mask.slice());
    if (w.undo.length > UNDO_LIMIT) w.undo.shift();
    setUndoCount(w.undo.length);
  }

  // ---- Painting, zooming and moving ----

  const inBox = (e: PointerEvent | WheelEvent): Point => {
    const r = box.current!.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  };
  /** A point of the box to a pixel of the photo, through the current zoom. */
  const toImage = (p: Point): Point => {
    const w = work.current!, v = viewRef.current;
    const k = w.src.width / base.w;
    return { x: ((p.x - v.x) / v.scale) * k, y: ((p.y - v.y) / v.scale) * k };
  };
  /** The brush's radius in photo pixels: its size on screen, through the zoom. */
  const radius = () => (brush / 2) * (work.current!.src.width / (base.w * viewRef.current.scale));

  function paint(from: Point, to: Point) {
    const w = work.current!;
    const r = radius();
    paintStroke(w.mask, w.src.width, w.src.height, from, to, r, tool === "add" ? 255 : 0);
    const x0 = Math.max(0, Math.floor(Math.min(from.x, to.x) - r - 1)), x1 = Math.min(w.src.width, Math.ceil(Math.max(from.x, to.x) + r + 1));
    const y0 = Math.max(0, Math.floor(Math.min(from.y, to.y) - r - 1)), y1 = Math.min(w.src.height, Math.ceil(Math.max(from.y, to.y) + r + 1));
    const d = dirty.current;
    dirty.current = d ? { x0: Math.min(d.x0, x0), y0: Math.min(d.y0, y0), x1: Math.max(d.x1, x1), y1: Math.max(d.y1, y1) } : { x0, y0, x1, y1 };
    window.cancelAnimationFrame(frame.current);
    frame.current = window.requestAnimationFrame(drawDirty);
  }

  const twoFingers = () => {
    const [a, b] = [...pointers.current.values()];
    return { mid: { x: (a!.x + b!.x) / 2, y: (a!.y + b!.y) / 2 }, spread: Math.hypot(a!.x - b!.x, a!.y - b!.y) };
  };

  function onPointerDown(e: PointerEvent<HTMLDivElement>) {
    if (!work.current || phase.name !== "edit") return;
    e.currentTarget.setPointerCapture?.(e.pointerId);
    const p = inBox(e);
    pointers.current.set(e.pointerId, p);
    if (pointers.current.size === 2) {
      // The second finger turns a stroke just begun into a pinch: the stroke is taken back.
      if (gesture.current?.kind === "paint") undo();
      const { mid, spread } = twoFingers();
      gesture.current = { kind: "pinch", start: { view: viewRef.current, mid, spread } };
      setRing(null);
      return;
    }
    if (pointers.current.size > 2) return;
    if (tool === "move" || showOriginal) { gesture.current = { kind: "pan", last: p }; return; }
    remember();
    const ip = toImage(p);
    gesture.current = { kind: "paint", last: ip };
    paint(ip, ip);
    setRing(p);
  }

  function onPointerMove(e: PointerEvent<HTMLDivElement>) {
    if (!work.current) return;
    const p = inBox(e);
    if (e.pointerType === "mouse" && !pointers.current.size) { setRing(tool === "move" || showOriginal ? null : p); return; }
    if (!pointers.current.has(e.pointerId)) return;
    pointers.current.set(e.pointerId, p);
    const g = gesture.current;
    if (!g) return;
    if (g.kind === "pinch" && pointers.current.size >= 2) {
      const { mid, spread } = twoFingers();
      setView(pinch(g.start, mid, spread, base, boxSize()));
    } else if (g.kind === "pan") {
      setView((v) => pan(v, p.x - g.last.x, p.y - g.last.y, base, boxSize()));
      g.last = p;
    } else if (g.kind === "paint") {
      const ip = toImage(p);
      paint(g.last, ip);
      g.last = ip;
      setRing(p);
    }
  }

  function onPointerUp(e: PointerEvent<HTMLDivElement>) {
    pointers.current.delete(e.pointerId);
    if (gesture.current?.kind === "pinch" && pointers.current.size === 1) {
      // One finger left after a pinch: it moves the photo rather than painting by surprise.
      const [last] = [...pointers.current.values()];
      gesture.current = { kind: "pan", last: last! };
      return;
    }
    if (pointers.current.size === 0) {
      gesture.current = null;
      if (e.pointerType !== "mouse") setRing(null);
    }
  }

  function onWheel(e: WheelEvent<HTMLDivElement>) {
    if (!work.current) return;
    setView((v) => zoomAt(v, v.scale * Math.pow(1.0015, -e.deltaY), inBox(e), base, boxSize()));
  }
  const zoomBy = (factor: number) => {
    const size = boxSize();
    setView((v) => zoomAt(v, v.scale * factor, { x: size.w / 2, y: size.h / 2 }, base, size));
  };
  const fitAll = () => setView(fitView(base, boxSize()));

  // ---- Undo, automatic result, colour ----

  function undo() {
    const w = work.current;
    const previous = w?.undo.pop();
    if (!w || !previous) return;
    w.mask = previous;
    setUndoCount(w.undo.length);
    drawAll();
  }
  function backToAutomatic() {
    const w = work.current;
    if (!w) return;
    remember();
    w.mask = w.auto.slice();
    drawAll();
  }
  /** Another colour: a new automatic guess, which can be undone like a stroke. */
  function detectAgain(next: HoldColor) {
    setColor(next);
    const w = work.current;
    if (!w) return;
    remember();
    w.auto = detectHolds(w.src.data, w.src.width, w.src.height, next);
    w.mask = w.auto.slice();
    w.color = next;
    drawAll();
  }

  async function confirm() {
    const w = work.current;
    if (!w || !color) return;
    setPhase({ name: "saving" });
    await new Promise((r) => window.setTimeout(r, 30));
    try {
      const picture = await decodeImage(w.blob);
      const { canvas: full, ctx, data } = pixelsOf(picture.image, WORK_SIDE);
      picture.release();
      const out = ctx.createImageData(full.width, full.height);
      compose(data.data, full.width, full.height, featherMask(w.mask, w.src.width, w.src.height, 1), w.src.width, w.src.height, out.data);
      ctx.putImageData(out, 0, 0);
      const blob = await new Promise<Blob>((resolve, reject) =>
        full.toBlob((b) => (b ? resolve(b) : reject(new Error(t("Couldn't prepare the picture. Try again.")))), "image/jpeg", 0.86));
      onDone(blob, color);
    } catch (e) {
      setPhase({ name: "error", message: e instanceof Error && e.message ? e.message : t("Couldn't prepare the picture. Try again.") });
    }
  }

  const editing = phase.name === "edit" || phase.name === "saving";
  const colorPicker = (onPick: (c: HoldColor) => void) => (
    <div className="highlighter__colors" role="radiogroup" aria-label={t("Hold colour")}>
      {HOLD_COLORS.map((c) => (
        <button key={c.value} type="button" role="radio" aria-checked={color === c.value} className="highlighter__color"
          onClick={() => onPick(c.value)} title={holdColorName(c.value)} aria-label={holdColorName(c.value)}>
          <span className="highlighter__swatch" style={{ background: c.hex }} aria-hidden />
        </button>
      ))}
    </div>
  );

  return (
    <div className="cropper highlighter" role="dialog" aria-modal="true" aria-labelledby="highlighter-title">
      <div className="cropper__top">
        <button type="button" className="viewer__close" onClick={onCancel} aria-label={t("Cancel")}><X aria-hidden /></button>
        <h2 id="highlighter-title" className="cropper__title">{t("Highlight the holds")}</h2>
        {editing
          ? <button type="button" className="viewer__close cropper__done" onClick={confirm} disabled={phase.name === "saving"} aria-label={t("Use this photo")}><Check aria-hidden /></button>
          : <span className="highlighter__spacer" aria-hidden />}
      </div>

      {(phase.name === "setup" || phase.name === "working" || phase.name === "error") && (
        <div className="highlighter__setup">
          <label className="highlighter__field">
            <span>{t("Photo of the wall")}</span>
            <select value={sourceId} onChange={(e) => setSourceId(e.target.value)} disabled={phase.name === "working"}>
              {sources.map((s) => <option key={s.id} value={s.id}>{s.label}</option>)}
            </select>
          </label>
          <div className="highlighter__field">
            <span>{t("Colour of this boulder's holds")}</span>
            {colorPicker(setColor)}
            {color && !canDetect(color) && <p className="cropper__hint">{t("Mixed holds can't be found by colour: you'll paint them in by hand.")}</p>}
          </div>
          {phase.name === "error" && <p className="highlighter__error" role="alert">{phase.message}</p>}
          <Button type="button" block icon={<Wand2 aria-hidden />} loading={phase.name === "working"} disabled={!color || !sourceId} onClick={generate}>
            {phase.name === "error" ? t("Try again") : t("Show the preview")}
          </Button>
          <p className="cropper__hint">{t("The holds of this colour stay in colour and the rest turns black and white. Then correct it with the brush: other boulders' holds of the same colour may be found too.")}</p>
        </div>
      )}

      {editing && (
        <>
          <div ref={box} className={`highlighter__stage is-${showOriginal ? "move" : tool}`}
            aria-label={t("Paint over the photo to keep colour or turn it grey. Two fingers zoom and move it.")}
            onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerCancel={onPointerUp}
            onPointerLeave={(e) => { if (e.pointerType === "mouse" && !pointers.current.size) setRing(null); }} onWheel={onWheel}>
            <canvas ref={canvas} className="highlighter__canvas"
              style={{ width: base.w || undefined, height: base.h || undefined, transform: `translate(${view.x}px, ${view.y}px) scale(${view.scale})` }} />
            {ring && <span className="highlighter__ring" aria-hidden style={{ left: ring.x, top: ring.y, width: brush, height: brush }} />}
            {/* Kept out of the painting: a tap on these must not leave a dab under them. */}
            <div className="highlighter__zoom" role="group" aria-label={t("Zoom")} onPointerDown={(e) => e.stopPropagation()}>
              <button type="button" onClick={() => zoomBy(1.6)} disabled={view.scale >= MAX_ZOOM} aria-label={t("Zoom in")}><ZoomIn aria-hidden /></button>
              <button type="button" onClick={() => zoomBy(1 / 1.6)} disabled={view.scale <= 1} aria-label={t("Zoom out")}><ZoomOut aria-hidden /></button>
              <button type="button" onClick={fitAll} disabled={view.scale <= 1} aria-label={t("Whole photo")}><Maximize aria-hidden /></button>
            </div>
            {phase.name === "saving" && <div className="highlighter__busy" role="status">{t("Preparing photo…")}</div>}
          </div>
          <div className="cropper__controls highlighter__controls">
            <p className="cropper__hint">{t("One finger paints, two fingers zoom and move the photo.")}</p>
            <div className="highlighter__row">
              <div className="highlighter__modes" role="radiogroup" aria-label={t("Brush")}>
                <button type="button" role="radio" aria-checked={tool === "add"} className="highlighter__mode" onClick={() => setTool("add")}>
                  <Paintbrush aria-hidden /> {t("Keep colour")}
                </button>
                <button type="button" role="radio" aria-checked={tool === "remove"} className="highlighter__mode" onClick={() => setTool("remove")}>
                  <Eraser aria-hidden /> {t("Turn grey")}
                </button>
                <button type="button" role="radio" aria-checked={tool === "move"} className="highlighter__mode" onClick={() => setTool("move")}
                  aria-label={t("Move")} title={t("Move")}>
                  <Hand aria-hidden />
                </button>
              </div>
              <button type="button" className={`highlighter__tool ${showOriginal ? "is-on" : ""}`} aria-pressed={showOriginal} onClick={() => setShowOriginal((v) => !v)}>
                <Eye aria-hidden /> {t("Original")}
              </button>
            </div>
            <label className="cropper__zoom highlighter__size">
              <span>{t("Brush size")}</span>
              <input type="range" min={BRUSH_MIN} max={BRUSH_MAX} step={1} value={brush} onChange={(e) => setBrush(Number(e.target.value))} />
              <span className="highlighter__dot" aria-hidden style={{ width: brush, height: brush }} />
            </label>
            <div className="highlighter__row">
              <button type="button" className="highlighter__tool" onClick={undo} disabled={undoCount === 0}><Undo2 aria-hidden /> {t("Undo")}</button>
              <button type="button" className="highlighter__tool" onClick={backToAutomatic}><RotateCcw aria-hidden /> {t("Automatic result")}</button>
            </div>
            <details className="highlighter__recolor">
              <summary>{t("Another colour")}</summary>
              {colorPicker(detectAgain)}
            </details>
            <button type="button" className="btn btn--primary btn--block" onClick={confirm} disabled={phase.name === "saving"}>
              <Check aria-hidden /><span>{t("Use this photo")}</span>
            </button>
          </div>
        </>
      )}
    </div>
  );
}
