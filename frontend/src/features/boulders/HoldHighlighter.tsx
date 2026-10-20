import { useEffect, useRef, useState, type PointerEvent } from "react";
import { Check, Eraser, Eye, Paintbrush, RotateCcw, Undo2, Wand2, X } from "lucide-react";
import { HOLD_COLORS, holdColorName, type HoldColor } from "@/features/boulders/holdColors";
import { canDetect, compose, detectHolds, featherMask, paintStroke } from "@/features/boulders/holdHighlight";
import { decodeImage } from "@/features/boulders/imageResize";
import { Button } from "@/components/Button";
import { t } from "@/i18n/i18n";

/** A photo the holds can be highlighted on: a sector's wall, or the photo just picked for the boulder. */
export interface HighlightSource { id: string; label: string; load: () => Promise<Blob> }

/** Painting happens on a reduced copy (quick to redraw under a finger); the result is made at full size. */
const PREVIEW_SIDE = 1024;
const RESULT_SIDE = 1600;
const UNDO_LIMIT = 20;

type Phase = { name: "setup" } | { name: "working" } | { name: "edit" } | { name: "saving" } | { name: "error"; message: string };

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
 * original. The result becomes the boulder's photo only when the form is saved; cancelling leaves the form as it was.
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
  const [mode, setMode] = useState<"add" | "remove">("add");
  const [brush, setBrush] = useState(3);
  const [showOriginal, setShowOriginal] = useState(false);
  const [undoCount, setUndoCount] = useState(0);

  const canvas = useRef<HTMLCanvasElement>(null);
  const work = useRef<{ blob: Blob; src: ImageData; mask: Uint8Array; auto: Uint8Array; undo: Uint8Array[]; color: HoldColor } | null>(null);
  const stroke = useRef<{ x: number; y: number } | null>(null);
  const frame = useRef(0);
  const showOriginalRef = useRef(false);
  // Photos already fetched, so changing the colour or trying again doesn't download the same wall twice.
  const fetched = useRef(new Map<string, Blob>());

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onCancel(); };
    window.addEventListener("keydown", onKey);
    return () => { window.removeEventListener("keydown", onKey); window.cancelAnimationFrame(frame.current); };
  }, [onCancel]);

  /** Redraws the preview from the untouched photo and the mask (edges softened once a stroke is finished). */
  function draw(soft = true) {
    const w = work.current, el = canvas.current;
    const ctx = el?.getContext("2d");
    if (!w || !el || !ctx) return;
    if (el.width !== w.src.width) { el.width = w.src.width; el.height = w.src.height; }
    if (showOriginalRef.current) { ctx.putImageData(w.src, 0, 0); return; }
    const out = ctx.createImageData(w.src.width, w.src.height);
    compose(w.src.data, w.src.width, w.src.height, soft ? featherMask(w.mask, w.src.width, w.src.height, 1) : w.mask, w.src.width, w.src.height, out.data);
    ctx.putImageData(out, 0, 0);
  }
  // The canvas appears with the editing phase, and the original/result switch redraws it.
  useEffect(() => { showOriginalRef.current = showOriginal; draw(); }, [showOriginal, phase.name]);

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
      const { data } = pixelsOf(picture.image, PREVIEW_SIDE);
      picture.release();
      const auto = detectHolds(data.data, data.width, data.height, color);
      work.current = { blob, src: data, mask: auto.slice(), auto, undo: [], color };
      setUndoCount(0);
      setShowOriginal(false);
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

  function toImage(e: PointerEvent<HTMLCanvasElement>) {
    const rect = e.currentTarget.getBoundingClientRect();
    const w = work.current!;
    return { x: ((e.clientX - rect.left) / rect.width) * w.src.width, y: ((e.clientY - rect.top) / rect.height) * w.src.height };
  }
  const radius = () => (Math.max(work.current!.src.width, work.current!.src.height) * brush) / 100 / 2;

  function onPointerDown(e: PointerEvent<HTMLCanvasElement>) {
    if (!work.current || showOriginal) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    remember();
    const p = toImage(e);
    stroke.current = p;
    const w = work.current;
    paintStroke(w.mask, w.src.width, w.src.height, p, p, radius(), mode === "add" ? 255 : 0);
    draw(false);
  }
  function onPointerMove(e: PointerEvent<HTMLCanvasElement>) {
    const w = work.current, from = stroke.current;
    if (!w || !from) return;
    const p = toImage(e);
    paintStroke(w.mask, w.src.width, w.src.height, from, p, radius(), mode === "add" ? 255 : 0);
    stroke.current = p;
    window.cancelAnimationFrame(frame.current);
    frame.current = window.requestAnimationFrame(() => draw(false));
  }
  function onPointerUp() {
    if (!stroke.current) return;
    stroke.current = null;
    window.cancelAnimationFrame(frame.current);
    draw();
  }

  function undo() {
    const w = work.current;
    const previous = w?.undo.pop();
    if (!w || !previous) return;
    w.mask = previous;
    setUndoCount(w.undo.length);
    draw();
  }
  function backToAutomatic() {
    const w = work.current;
    if (!w) return;
    remember();
    w.mask = w.auto.slice();
    draw();
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
    draw();
  }

  async function confirm() {
    const w = work.current;
    if (!w || !color) return;
    setPhase({ name: "saving" });
    await new Promise((r) => window.setTimeout(r, 30));
    try {
      const picture = await decodeImage(w.blob);
      const { canvas: full, ctx, data } = pixelsOf(picture.image, RESULT_SIDE);
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
          <div className="cropper__stage highlighter__stage">
            <canvas ref={canvas} className={`highlighter__canvas ${showOriginal ? "" : `is-${mode}`}`}
              aria-label={t("Paint over the photo to keep colour or turn it grey")}
              onPointerDown={onPointerDown} onPointerMove={onPointerMove} onPointerUp={onPointerUp} onPointerCancel={onPointerUp} />
            {phase.name === "saving" && <div className="highlighter__busy" role="status">{t("Preparing photo…")}</div>}
          </div>
          <div className="cropper__controls highlighter__controls">
            <div className="highlighter__row">
              <div className="highlighter__modes" role="radiogroup" aria-label={t("Brush")}>
                <button type="button" role="radio" aria-checked={mode === "add"} className="highlighter__mode" onClick={() => setMode("add")}>
                  <Paintbrush aria-hidden /> {t("Keep colour")}
                </button>
                <button type="button" role="radio" aria-checked={mode === "remove"} className="highlighter__mode" onClick={() => setMode("remove")}>
                  <Eraser aria-hidden /> {t("Turn grey")}
                </button>
              </div>
              <button type="button" className={`highlighter__tool ${showOriginal ? "is-on" : ""}`} aria-pressed={showOriginal} onClick={() => setShowOriginal((v) => !v)}>
                <Eye aria-hidden /> {t("Original")}
              </button>
            </div>
            <label className="cropper__zoom highlighter__size">
              <span>{t("Brush size")}</span>
              <input type="range" min={1} max={10} step={0.5} value={brush} onChange={(e) => setBrush(Number(e.target.value))} />
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
