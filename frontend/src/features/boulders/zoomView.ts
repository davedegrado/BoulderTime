/**
 * Zoom and pan for a picture shown inside a box (the hold highlighter's photo). Plain geometry, so it can be tested:
 * the picture is drawn at `base` size (fitted inside the box) and then moved by `x`, `y` and enlarged by `scale`
 * (CSS `translate(x, y) scale(scale)` from the top-left corner).
 */
export interface View { scale: number; x: number; y: number }
export interface Size { w: number; h: number }
export interface Point { x: number; y: number }

export const MAX_ZOOM = 8;

/** The picture's size when it just fits the box, keeping its proportions. */
export function fitted(picture: Size, box: Size): Size {
  const s = Math.min(box.w / picture.w, box.h / picture.h);
  return { w: picture.w * s, h: picture.h * s };
}

/** Whole picture, centred. */
export function fitView(base: Size, box: Size): View {
  return { scale: 1, x: (box.w - base.w) / 2, y: (box.h - base.h) / 2 };
}

/**
 * Keeps the picture where it can be seen: never smaller than fitted, never further in than MAX_ZOOM, and never moved so
 * far that an edge leaves an empty band inside the box (smaller than the box, it stays centred).
 */
export function clampView(v: View, base: Size, box: Size): View {
  const scale = Math.min(MAX_ZOOM, Math.max(1, v.scale));
  const along = (pos: number, size: number, room: number) => (size <= room ? (room - size) / 2 : Math.min(0, Math.max(room - size, pos)));
  return { scale, x: along(v.x, base.w * scale, box.w), y: along(v.y, base.h * scale, box.h) };
}

/** Zooms to `scale` keeping the point of the picture under `at` (in box coordinates) where it is. */
export function zoomAt(v: View, scale: number, at: Point, base: Size, box: Size): View {
  const next = Math.min(MAX_ZOOM, Math.max(1, scale));
  const ix = (at.x - v.x) / v.scale, iy = (at.y - v.y) / v.scale;
  return clampView({ scale: next, x: at.x - ix * next, y: at.y - iy * next }, base, box);
}

/**
 * Two fingers: the picture follows them. `start` is the view and the fingers' midpoint and spread when they touched;
 * the point of the picture that was under the midpoint stays under it, and the spread scales the zoom.
 */
export function pinch(start: { view: View; mid: Point; spread: number }, mid: Point, spread: number, base: Size, box: Size): View {
  const scale = Math.min(MAX_ZOOM, Math.max(1, start.view.scale * (spread / Math.max(1, start.spread))));
  const ix = (start.mid.x - start.view.x) / start.view.scale, iy = (start.mid.y - start.view.y) / start.view.scale;
  return clampView({ scale, x: mid.x - ix * scale, y: mid.y - iy * scale }, base, box);
}

/** One finger in "move" mode: the picture follows it. */
export const pan = (v: View, dx: number, dy: number, base: Size, box: Size): View => clampView({ ...v, x: v.x + dx, y: v.y + dy }, base, box);
