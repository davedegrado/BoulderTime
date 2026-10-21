import type { HoldColor } from "@/features/boulders/holdColors";

/**
 * "Highlight the holds": a photo of the wall where the holds of one colour keep their colour and everything else turns
 * grey. All of it runs on the device, on plain pixel arrays, so it needs no service and can be tested without a
 * browser.
 *
 * The automatic part is a starting point, not a recognition: it finds what has the colour of the holds (the hold
 * colours BoulderTime knows, read as hue/saturation/brightness so shade and light don't throw it), cleans up specks,
 * and leaves staff to paint in what it missed and paint out what isn't this boulder (other boulders' holds of the same
 * colour, a jacket, a volume painted alike). The mask is a separate layer: the result is always made again from the
 * untouched photo, never by filtering a picture already filtered.
 */

/** Hue in degrees (0–360), saturation and brightness (0–1). */
export function rgbToHsv(r: number, g: number, b: number): [number, number, number] {
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min;
  let h = 0;
  if (d > 0) {
    if (max === r) h = 60 * (((g - b) / d) % 6);
    else if (max === g) h = 60 * ((b - r) / d + 2);
    else h = 60 * ((r - g) / d + 4);
  }
  if (h < 0) h += 360;
  return [h, max === 0 ? 0 : d / max, max / 255];
}

const between = (h: number, from: number, to: number) => (from <= to ? h >= from && h <= to : h >= from || h <= to);

/**
 * What counts as "this colour" for each hold colour. Wide on purpose for hue and brightness (a red hold in shadow is
 * dark red, a yellow one under a spotlight is almost white-yellow); saturation keeps the grey wall out.
 * White, grey and black have no hue, so they are told apart by brightness alone — on a white wall, white holds will
 * need painting by hand. Mixed holds have no single colour: nothing is found automatically.
 */
const MATCHERS: Record<HoldColor, ((h: number, s: number, v: number) => boolean) | null> = {
  RED: (h, s, v) => between(h, 342, 12) && s >= 0.35 && v >= 0.18,
  ORANGE: (h, s, v) => between(h, 13, 38) && s >= 0.5 && v >= 0.4,
  YELLOW: (h, s, v) => between(h, 40, 68) && s >= 0.35 && v >= 0.4,
  GREEN: (h, s, v) => between(h, 72, 165) && s >= 0.25 && v >= 0.15,
  BLUE: (h, s, v) => between(h, 186, 250) && s >= 0.3 && v >= 0.15,
  PURPLE: (h, s, v) => between(h, 251, 298) && s >= 0.2 && v >= 0.12,
  PINK: (h, s, v) => between(h, 299, 341) && s >= 0.22 && v >= 0.4,
  BROWN: (h, s, v) => between(h, 10, 40) && s >= 0.3 && v >= 0.12 && v <= 0.55,
  WHITE: (_h, s, v) => s <= 0.14 && v >= 0.82,
  GREY: (_h, s, v) => s <= 0.12 && v >= 0.3 && v <= 0.72,
  BLACK: (_h, _s, v) => v <= 0.17,
  MIXED: null,
};

/** Whether this colour can be found automatically (mixed holds can't: they are painted by hand). */
export const canDetect = (color: HoldColor) => MATCHERS[color] !== null;

/** 0 or 255 per pixel: what has the colour, before any cleaning. */
function rawMatch(rgba: Uint8ClampedArray, width: number, height: number, color: HoldColor): Uint8Array {
  const mask = new Uint8Array(width * height);
  const matches = MATCHERS[color];
  if (!matches) return mask;
  for (let i = 0, p = 0; p < mask.length; i += 4, p++) {
    const [h, s, v] = rgbToHsv(rgba[i]!, rgba[i + 1]!, rgba[i + 2]!);
    if (matches(h, s, v)) mask[p] = 255;
  }
  return mask;
}

/** Grows (dilate) or shrinks (erode) the mask by a square of `radius`, in two passes so it stays fast. */
function morph(mask: Uint8Array, width: number, height: number, radius: number, grow: boolean): Uint8Array {
  const pass = (src: Uint8Array, horizontal: boolean) => {
    const out = new Uint8Array(src.length);
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        let hit = !grow;
        for (let k = -radius; k <= radius; k++) {
          const xx = horizontal ? x + k : x, yy = horizontal ? y : y + k;
          // Beyond the photo's edge counts for nothing either way: a hold cut by the frame keeps its edge.
          if (xx < 0 || xx >= width || yy < 0 || yy >= height) continue;
          const on = src[yy * width + xx] === 255;
          if (grow && on) { hit = true; break; }
          if (!grow && !on) { hit = false; break; }
        }
        out[y * width + x] = hit ? 255 : 0;
      }
    }
    return out;
  };
  return pass(pass(mask, true), false);
}

/** Drops the areas smaller than `minArea` pixels: specks of colour in the wall's texture, not holds. */
function dropSpecks(mask: Uint8Array, width: number, height: number, minArea: number): Uint8Array {
  const out = new Uint8Array(mask.length);
  const seen = new Uint8Array(mask.length);
  const stack: number[] = [];
  const area: number[] = [];
  for (let start = 0; start < mask.length; start++) {
    if (mask[start] !== 255 || seen[start]) continue;
    area.length = 0;
    stack.push(start);
    seen[start] = 1;
    while (stack.length) {
      const p = stack.pop()!;
      area.push(p);
      const x = p % width, y = (p - x) / width;
      const next = [x > 0 ? p - 1 : -1, x < width - 1 ? p + 1 : -1, y > 0 ? p - width : -1, y < height - 1 ? p + width : -1];
      for (const q of next) if (q >= 0 && mask[q] === 255 && !seen[q]) { seen[q] = 1; stack.push(q); }
    }
    if (area.length >= minArea) for (const p of area) out[p] = 255;
  }
  return out;
}

/**
 * The automatic mask: the pixels of the hold colour, opened (specks gone), small leftovers dropped, then closed (the
 * bolt hole and the chalk in the middle of a hold filled in). Sizes follow the picture, so a phone photo and a
 * reduced preview of it come out alike.
 */
export function detectHolds(rgba: Uint8ClampedArray, width: number, height: number, color: HoldColor): Uint8Array {
  const raw = rawMatch(rgba, width, height, color);
  const side = Math.max(width, height);
  const open = Math.max(1, Math.round(side / 600));
  const close = Math.max(2, Math.round(side / 250));
  const opened = morph(morph(raw, width, height, open, false), width, height, open, true);
  const kept = dropSpecks(opened, width, height, Math.max(12, Math.round(width * height * 0.00012)));
  return morph(morph(kept, width, height, close, true), width, height, close, false);
}

/** Paints a round dab of the brush into the mask: 255 keeps colour there, 0 turns it grey. */
export function paintDab(mask: Uint8Array, width: number, height: number, cx: number, cy: number, radius: number, value: 0 | 255) {
  const r2 = radius * radius;
  const x0 = Math.max(0, Math.floor(cx - radius)), x1 = Math.min(width - 1, Math.ceil(cx + radius));
  const y0 = Math.max(0, Math.floor(cy - radius)), y1 = Math.min(height - 1, Math.ceil(cy + radius));
  for (let y = y0; y <= y1; y++)
    for (let x = x0; x <= x1; x++)
      if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2) mask[y * width + x] = value;
}

/** A brush stroke between two points, as dabs close enough to leave no gaps however fast the finger moves. */
export function paintStroke(mask: Uint8Array, width: number, height: number, from: { x: number; y: number }, to: { x: number; y: number }, radius: number, value: 0 | 255) {
  const steps = Math.max(1, Math.ceil(Math.hypot(to.x - from.x, to.y - from.y) / Math.max(1, radius / 2)));
  for (let i = 0; i <= steps; i++)
    paintDab(mask, width, height, from.x + ((to.x - from.x) * i) / steps, from.y + ((to.y - from.y) * i) / steps, radius, value);
}

/** Softens the mask's edge (a box blur), so the colour fades into the grey instead of stopping on a jagged line. */
export function featherMask(mask: Uint8Array, width: number, height: number, radius: number): Uint8Array {
  if (radius < 1) return mask;
  const blur = (src: Uint8Array, horizontal: boolean) => {
    const out = new Uint8Array(src.length);
    const n = radius * 2 + 1;
    for (let line = 0; line < (horizontal ? height : width); line++) {
      const len = horizontal ? width : height;
      const at = (i: number) => src[horizontal ? line * width + Math.min(len - 1, Math.max(0, i)) : Math.min(len - 1, Math.max(0, i)) * width + line]!;
      let sum = 0;
      for (let k = -radius; k <= radius; k++) sum += at(k);
      for (let i = 0; i < len; i++) {
        out[horizontal ? line * width + i : i * width + line] = Math.round(sum / n);
        sum += at(i + radius + 1) - at(i - radius);
      }
    }
    return out;
  };
  return blur(blur(mask, true), false);
}

/**
 * The result: each pixel between its grey (perceived brightness) and its own colour, as far as the mask says. `mask`
 * may be smaller than the photo (painted on a preview): it is read at the matching place, smoothly.
 */
export function compose(src: Uint8ClampedArray, width: number, height: number, mask: Uint8Array, maskWidth: number, maskHeight: number, out: Uint8ClampedArray) {
  const sx = maskWidth / width, sy = maskHeight / height;
  const same = maskWidth === width && maskHeight === height;
  for (let y = 0, i = 0; y < height; y++) {
    const my = Math.min(maskHeight - 1, Math.max(0, (y + 0.5) * sy - 0.5));
    const y0 = Math.floor(my), y1 = Math.min(maskHeight - 1, y0 + 1), fy = my - y0;
    for (let x = 0; x < width; x++, i += 4) {
      let a: number;
      if (same) a = mask[y * width + x]! / 255;
      else {
        const mx = Math.min(maskWidth - 1, Math.max(0, (x + 0.5) * sx - 0.5));
        const x0 = Math.floor(mx), x1 = Math.min(maskWidth - 1, x0 + 1), fx = mx - x0;
        const top = mask[y0 * maskWidth + x0]! * (1 - fx) + mask[y0 * maskWidth + x1]! * fx;
        const bottom = mask[y1 * maskWidth + x0]! * (1 - fx) + mask[y1 * maskWidth + x1]! * fx;
        a = (top * (1 - fy) + bottom * fy) / 255;
      }
      const r = src[i]!, g = src[i + 1]!, b = src[i + 2]!;
      const grey = 0.299 * r + 0.587 * g + 0.114 * b;
      out[i] = grey + (r - grey) * a;
      out[i + 1] = grey + (g - grey) * a;
      out[i + 2] = grey + (b - grey) * a;
      out[i + 3] = src[i + 3]!;
    }
  }
}

/**
 * The result over one rectangle only (mask and photo the same size): what a brush stroke changed, redrawn without
 * going over the whole photo, so painting stays smooth on a phone at full resolution.
 */
export function composeArea(src: Uint8ClampedArray, width: number, mask: Uint8Array, out: Uint8ClampedArray, x0: number, y0: number, x1: number, y1: number) {
  for (let y = y0; y < y1; y++) {
    for (let x = x0, i = (y * width + x0) * 4; x < x1; x++, i += 4) {
      const a = mask[y * width + x]! / 255;
      const r = src[i]!, g = src[i + 1]!, b = src[i + 2]!;
      const grey = 0.299 * r + 0.587 * g + 0.114 * b;
      out[i] = grey + (r - grey) * a;
      out[i + 1] = grey + (g - grey) * a;
      out[i + 2] = grey + (b - grey) * a;
      out[i + 3] = src[i + 3]!;
    }
  }
}

// ---- Outline: a line around the selected holds, derived from the mask every time it is drawn ----

/** Colour of the outline and its width in photo pixels. */
export interface OutlineStyle { rgb: [number, number, number]; width: number }

/**
 * How far the outline reaches outside the selection: the line, the thin contrasting edge beyond it, and the pixel
 * where the edge fades out.
 */
export const outlineReach = (width: number) => width + haloOf(width) + 1;
const haloOf = (width: number) => Math.max(1, Math.round(width / 3));

/** The thin edge drawn just outside the line: light around a dark line, dark around a light one, so it shows on any wall. */
const haloFor = ([r, g, b]: [number, number, number]): [number, number, number] =>
  (0.299 * r + 0.587 * g + 0.114 * b) / 255 > 0.6 ? [17, 17, 17] : [255, 255, 255];

/** Squared Euclidean distance transform along one line (Felzenszwalb & Huttenlocher), in place on `f`. */
function edt1d(f: Float64Array, n: number, v: Int32Array, z: Float64Array, d: Float64Array) {
  let k = 0;
  v[0] = 0;
  z[0] = -Infinity;
  z[1] = Infinity;
  for (let q = 1; q < n; q++) {
    let s = (f[q]! + q * q - (f[v[k]!]! + v[k]! * v[k]!)) / (2 * q - 2 * v[k]!);
    while (s <= z[k]!) {
      k--;
      s = (f[q]! + q * q - (f[v[k]!]! + v[k]! * v[k]!)) / (2 * q - 2 * v[k]!);
    }
    k++;
    v[k] = q;
    z[k] = s;
    z[k + 1] = Infinity;
  }
  k = 0;
  for (let q = 0; q < n; q++) {
    while (z[k + 1]! < q) k++;
    d[q] = (q - v[k]!) * (q - v[k]!) + f[v[k]!]!;
  }
  for (let q = 0; q < n; q++) f[q] = d[q]!;
}

/**
 * Draws the outline over `out` in one rectangle, after `out` has just been made again from the photo and the mask
 * (never over a picture already outlined, so it can't thicken or double up). The line lies outside the selection,
 * hugging it at the given width, its outer side softened over a pixel; beyond it a thin contrasting edge. The distance
 * to the selection is measured on a region larger than the rectangle by the outline's reach, so a rectangle drawn
 * after a brush stroke comes out exactly as the whole picture would.
 */
export function outlineArea(out: Uint8ClampedArray, width: number, height: number, mask: Uint8Array,
  x0: number, y0: number, x1: number, y1: number, style: OutlineStyle) {
  const w = Math.max(1, style.width), halo = haloOf(w), reach = outlineReach(w);
  // The outline only exists within its reach of the selection: work out where the selection is first, and measure
  // distances only around it. Same picture, a fraction of the work when the holds fill part of the photo.
  let bx0 = width, by0 = height, bx1 = -1, by1 = -1;
  for (let y = Math.max(0, y0 - reach); y < Math.min(height, y1 + reach); y++)
    for (let x = Math.max(0, x0 - reach); x < Math.min(width, x1 + reach); x++)
      if (mask[y * width + x]! >= 128) { if (x < bx0) bx0 = x; if (x > bx1) bx1 = x; if (y < by0) by0 = y; if (y > by1) by1 = y; }
  if (bx1 < 0) return;
  x0 = Math.max(x0, bx0 - reach); y0 = Math.max(y0, by0 - reach); x1 = Math.min(x1, bx1 + reach + 1); y1 = Math.min(y1, by1 + reach + 1);
  if (x1 <= x0 || y1 <= y0) return;
  const rx0 = Math.max(0, x0 - reach), ry0 = Math.max(0, y0 - reach), rx1 = Math.min(width, x1 + reach), ry1 = Math.min(height, y1 + reach);
  const rw = rx1 - rx0, rh = ry1 - ry0;
  const BIG = 1e12;
  const f = new Float32Array(rw * rh);
  for (let y = 0; y < rh; y++)
    for (let x = 0; x < rw; x++) f[y * rw + x] = mask[(ry0 + y) * width + rx0 + x]! >= 128 ? 0 : BIG;
  const n = Math.max(rw, rh);
  const line = new Float64Array(n), v = new Int32Array(n), z = new Float64Array(n + 1), d = new Float64Array(n);
  for (let x = 0; x < rw; x++) {
    for (let y = 0; y < rh; y++) line[y] = f[y * rw + x]!;
    edt1d(line, rh, v, z, d);
    for (let y = 0; y < rh; y++) f[y * rw + x] = line[y]!;
  }
  for (let y = 0; y < rh; y++) {
    for (let x = 0; x < rw; x++) line[x] = f[y * rw + x]!;
    edt1d(line, rw, v, z, d);
    for (let x = 0; x < rw; x++) f[y * rw + x] = line[x]!;
  }
  const [lr, lg, lb] = style.rgb, [hr, hg, hb] = haloFor(style.rgb);
  for (let y = y0; y < y1; y++) {
    for (let x = x0; x < x1; x++) {
      const dist2 = f[(y - ry0) * rw + (x - rx0)]!;
      if (dist2 === 0 || dist2 >= (reach + 1) * (reach + 1)) continue;
      const dist = Math.sqrt(dist2);
      const onLine = Math.min(1, Math.max(0, w + 0.5 - dist));
      const onEdge = Math.min(1, Math.max(0, w + halo + 0.5 - dist)) - onLine;
      if (onLine <= 0 && onEdge <= 0) continue;
      const i = (y * width + x) * 4, keep = 1 - onLine - onEdge;
      out[i] = out[i]! * keep + lr * onLine + hr * onEdge;
      out[i + 1] = out[i + 1]! * keep + lg * onLine + hg * onEdge;
      out[i + 2] = out[i + 2]! * keep + lb * onLine + hb * onEdge;
    }
  }
}

/** Outline width in photo pixels for a level (thin, medium, thick), relative to the photo so the saved picture looks the same at any size. */
export const outlineWidth = (level: 1 | 2 | 3, width: number, height: number) =>
  Math.max(2, Math.round(Math.max(width, height) * ({ 1: 0.0025, 2: 0.004, 3: 0.0065 } as const)[level]));
