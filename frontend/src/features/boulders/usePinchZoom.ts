import { useCallback, useEffect, useLayoutEffect, useRef, useState, type CSSProperties, type MouseEvent, type PointerEvent } from "react";
import { fitted, fitView, pan, pinch, zoomAt, type Point, type Size, type View } from "@/features/boulders/zoomView";

/** How far in a tap or a zoom button goes. */
export const TAP_ZOOM = 2.5;
/** Movement under this many pixels is a tap, not a drag. */
const TAP_SLOP = 8;
const DOUBLE_TAP_MS = 300;

type Gesture =
  | { kind: "pan"; last: Point; from: Point }
  | { kind: "pinch"; start: { view: View; mid: Point; spread: number } };

/**
 * Zoom for a picture shown full screen: two fingers pinch (the page itself can't be pinched, so the app does it), one
 * finger moves the picture once it is enlarged, a double tap goes in on that spot and back out, the wheel zooms on a
 * computer, and `zoomTo` serves the buttons. The geometry is the hold highlighter's (zoomView): never smaller than the
 * whole picture, never an empty band at an edge.
 *
 * `picture` is the picture's own size (its proportions are what matter); until it's known nothing is drawn. With
 * `tapToZoom` a single tap goes in and out (a photo); without it a tap stays a tap for what's underneath (the sectors
 * of a plan) — and a drag never counts as one.
 */
export function usePinchZoom(picture: Size | null, { tapToZoom = false }: { tapToZoom?: boolean } = {}) {
  const box = useRef<HTMLDivElement>(null);
  const [boxSize, setBoxSize] = useState<Size | null>(null);
  const [view, setViewState] = useState<View | null>(null);
  const viewRef = useRef<View | null>(null);
  const setView = (v: View) => { viewRef.current = v; setViewState(v); };
  const pointers = useRef(new Map<number, Point>());
  const gesture = useRef<Gesture | null>(null);
  const dragged = useRef(false);
  const lastTap = useRef<{ at: number; point: Point } | null>(null);

  useLayoutEffect(() => {
    const el = box.current;
    if (!el) return;
    const measure = () => {
      const r = el.getBoundingClientRect();
      setBoxSize((s) => (s && s.w === r.width && s.h === r.height ? s : { w: r.width, h: r.height }));
    };
    measure();
    if (typeof ResizeObserver === "undefined") {
      window.addEventListener("resize", measure);
      return () => window.removeEventListener("resize", measure);
    }
    const observer = new ResizeObserver(measure);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const base = picture && boxSize && boxSize.w > 0 && boxSize.h > 0 ? fitted(picture, boxSize) : null;
  // A new picture or a new box (a rotation) starts from the whole picture again.
  useEffect(() => {
    if (base && boxSize) setView(fitView(base, boxSize));
  }, [base?.w, base?.h, boxSize?.w, boxSize?.h]);

  const local = (e: { clientX: number; clientY: number }): Point => {
    const r = box.current!.getBoundingClientRect();
    return { x: e.clientX - r.left, y: e.clientY - r.top };
  };
  const two = () => {
    const [a, b] = [...pointers.current.values()];
    return { mid: { x: (a!.x + b!.x) / 2, y: (a!.y + b!.y) / 2 }, spread: Math.hypot(a!.x - b!.x, a!.y - b!.y) };
  };

  /** Zooms to `scale` around a point of the box (its centre by default); 1 is the whole picture. */
  const zoomTo = useCallback((scale: number, at?: Point) => {
    const v = viewRef.current;
    if (!v || !base || !boxSize) return;
    setView(scale <= 1 ? fitView(base, boxSize) : zoomAt(v, scale, at ?? { x: boxSize.w / 2, y: boxSize.h / 2 }, base, boxSize));
  }, [base, boxSize]);

  function onPointerDown(e: PointerEvent<HTMLDivElement>) {
    pointers.current.set(e.pointerId, local(e));
    if (pointers.current.size === 1) {
      dragged.current = false;
      gesture.current = { kind: "pan", last: local(e), from: local(e) };
    } else if (pointers.current.size === 2 && viewRef.current) {
      dragged.current = true;
      const { mid, spread } = two();
      gesture.current = { kind: "pinch", start: { view: viewRef.current, mid, spread } };
    }
  }

  function onPointerMove(e: PointerEvent<HTMLDivElement>) {
    if (!pointers.current.has(e.pointerId)) return;
    const p = local(e);
    pointers.current.set(e.pointerId, p);
    const g = gesture.current, v = viewRef.current;
    if (!g || !v || !base || !boxSize) return;
    if (g.kind === "pinch" && pointers.current.size >= 2) {
      const { mid, spread } = two();
      setView(pinch(g.start, mid, spread, base, boxSize));
    } else if (g.kind === "pan") {
      if (Math.hypot(p.x - g.from.x, p.y - g.from.y) > TAP_SLOP) dragged.current = true;
      if (dragged.current && v.scale > 1) setView(pan(v, p.x - g.last.x, p.y - g.last.y, base, boxSize));
      g.last = p;
    }
  }

  function onPointerUp(e: PointerEvent<HTMLDivElement>) {
    const wasPinch = gesture.current?.kind === "pinch";
    pointers.current.delete(e.pointerId);
    if (wasPinch && pointers.current.size === 1) {
      // One finger lifted from a pinch: the other carries on moving, from where it is.
      const [rest] = [...pointers.current.values()];
      gesture.current = { kind: "pan", last: rest!, from: rest! };
      return;
    }
    if (pointers.current.size > 0) return;
    gesture.current = null;
    if (dragged.current || e.type === "pointercancel") return;

    const point = local(e);
    const zoomed = (viewRef.current?.scale ?? 1) > 1.01;
    const toggle = () => zoomTo(zoomed ? 1 : TAP_ZOOM, point);
    if (tapToZoom) { toggle(); return; }
    const before = lastTap.current;
    if (before && e.timeStamp - before.at < DOUBLE_TAP_MS && Math.hypot(point.x - before.point.x, point.y - before.point.y) < TAP_SLOP * 3) {
      lastTap.current = null;
      toggle();
    } else {
      lastTap.current = { at: e.timeStamp, point };
    }
  }

  // The wheel (or a trackpad's pinch) zooms on the spot under the pointer. Not passive, so the page doesn't scroll.
  useEffect(() => {
    const el = box.current;
    if (!el) return;
    const onWheel = (e: WheelEvent) => {
      const v = viewRef.current;
      if (!v || !base || !boxSize) return;
      e.preventDefault();
      setView(zoomAt(v, v.scale * Math.exp(-e.deltaY * 0.002), local(e), base, boxSize));
    };
    el.addEventListener("wheel", onWheel, { passive: false });
    return () => el.removeEventListener("wheel", onWheel);
  }, [base, boxSize]);

  /** A drag or a pinch is not a click on what's underneath (a sector of the plan). */
  function onClickCapture(e: MouseEvent<HTMLDivElement>) {
    if (dragged.current) { e.stopPropagation(); e.preventDefault(); dragged.current = false; }
  }

  const contentStyle: CSSProperties | undefined = base && view
    ? { position: "absolute", left: 0, top: 0, width: base.w, height: base.h, transform: `translate(${view.x}px, ${view.y}px) scale(${view.scale})`, transformOrigin: "0 0" }
    : undefined;

  return {
    box,
    /** On the box: it takes every finger, so the page never moves underneath. */
    boxProps: { onPointerDown, onPointerMove, onPointerUp, onPointerCancel: onPointerUp, onClickCapture, style: { touchAction: "none" } as CSSProperties },
    contentStyle,
    scale: view?.scale ?? 1,
    zoomed: (view?.scale ?? 1) > 1.01,
    zoomTo,
  };
}
