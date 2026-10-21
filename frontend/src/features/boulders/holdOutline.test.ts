import { describe, expect, it } from "vitest";
import { composeArea, outlineArea, outlineReach, outlineWidth, paintStroke } from "@/features/boulders/holdHighlight";

type RGB = [number, number, number];
const W = 80, H = 60;
const RED: RGB = [224, 32, 27];

function photo(wall: RGB, holds: { x: number; y: number; size: number; rgb: RGB }[]) {
  const px = new Uint8ClampedArray(W * H * 4);
  for (let p = 0; p < W * H; p++) px.set([...wall, 255], p * 4);
  for (const s of holds)
    for (let y = s.y; y < s.y + s.size; y++) for (let x = s.x; x < s.x + s.size; x++) px.set([...s.rgb, 255], (y * W + x) * 4);
  return px;
}
function maskOf(squares: { x: number; y: number; size: number }[]) {
  const m = new Uint8Array(W * H);
  for (const s of squares) for (let y = s.y; y < s.y + s.size; y++) for (let x = s.x; x < s.x + s.size; x++) m[y * W + x] = 255;
  return m;
}
/** The whole picture made again from the photo and the mask, outline included: what the editor and the save do. */
function render(src: Uint8ClampedArray, mask: Uint8Array, width = 3, rgb: RGB = RED, outlined = true) {
  const out = new Uint8ClampedArray(src.length);
  composeArea(src, W, mask, out, 0, 0, W, H);
  if (outlined) outlineArea(out, W, H, mask, 0, 0, W, H, { rgb, width });
  return out;
}
const at = (px: Uint8ClampedArray, x: number, y: number) => Array.from(px.slice((y * W + x) * 4, (y * W + x) * 4 + 3));
const isRed = (c: number[]) => c[0]! > 180 && c[1]! < 80 && c[2]! < 80;
const isGrey = (c: number[]) => Math.abs(c[0]! - c[1]!) < 2 && Math.abs(c[1]! - c[2]!) < 2;

describe("The outline around the selected holds", () => {
  it("rings a white hold on a light wall, outside the hold, which keeps its own pixels", () => {
    const src = photo([225, 225, 220], [{ x: 20, y: 20, size: 12, rgb: [250, 250, 250] }]);
    const out = render(src, maskOf([{ x: 20, y: 20, size: 12 }]));
    expect(isRed(at(out, 18, 25))).toBe(true);         // just outside, on the left
    expect(isRed(at(out, 33, 25))).toBe(true);         // just outside, on the right
    expect(at(out, 25, 25)).toEqual([250, 250, 250]);  // the hold itself, untouched
    expect(isGrey(at(out, 5, 5))).toBe(true);          // the wall far away
  });

  it("rings a black hold on a dark wall, with a light edge that makes it stand out", () => {
    const src = photo([40, 40, 45], [{ x: 20, y: 20, size: 12, rgb: [15, 15, 15] }]);
    const out = render(src, maskOf([{ x: 20, y: 20, size: 12 }]), 3);
    expect(isRed(at(out, 18, 25))).toBe(true);
    const edge = at(out, 16, 25); // beyond the line: the light edge, brighter than the dark wall
    expect(edge[0]!).toBeGreaterThan(120);
  });

  it("follows the selection: a dab added gets its outline, a part removed loses it", () => {
    const src = photo([200, 200, 200], []);
    const mask = maskOf([{ x: 10, y: 10, size: 10 }]);
    paintStroke(mask, W, H, { x: 55, y: 40 }, { x: 55, y: 40 }, 4, 255);
    let out = render(src, mask);
    expect(isRed(at(out, 49, 40))).toBe(true);  // around the dab
    paintStroke(mask, W, H, { x: 15, y: 15 }, { x: 15, y: 15 }, 12, 0); // the first area painted out
    out = render(src, mask);
    expect(isRed(at(out, 8, 15))).toBe(false);
    expect(isGrey(at(out, 8, 15))).toBe(true);
    expect(isRed(at(out, 49, 40))).toBe(true);
  });

  it("joins areas brought together, with no line left between them", () => {
    const src = photo([200, 200, 200], []);
    const apart = maskOf([{ x: 10, y: 20, size: 10 }, { x: 30, y: 20, size: 10 }]);
    expect(isRed(at(render(src, apart), 21, 25))).toBe(true); // the gap between them is outlined
    const joined = maskOf([{ x: 10, y: 20, size: 30 }]);
    const out = render(src, joined);
    expect(isRed(at(out, 25, 25))).toBe(false);
    expect(isRed(at(out, 8, 25))).toBe(true);
  });

  it("is never drawn twice: making the picture again gives the same picture", () => {
    const src = photo([200, 200, 200], [{ x: 20, y: 20, size: 12, rgb: [40, 90, 220] }]);
    const mask = maskOf([{ x: 20, y: 20, size: 12 }]);
    expect(render(src, mask)).toEqual(render(src, mask));
  });

  it("drawn only where the brush went, it matches the whole picture drawn again", () => {
    const src = photo([200, 200, 200], [{ x: 20, y: 20, size: 12, rgb: [40, 90, 220] }]);
    const mask = maskOf([{ x: 20, y: 20, size: 12 }]);
    const live = render(src, mask, 4);
    paintStroke(mask, W, H, { x: 33, y: 25 }, { x: 45, y: 30 }, 3, 255);
    // The editor's update: the stroke's box, widened by the outline's reach, made again and outlined.
    const r = outlineReach(4);
    const [x0, y0, x1, y1] = [Math.max(0, 30 - r), Math.max(0, 22 - r), Math.min(W, 49 + r), Math.min(H, 34 + r)];
    composeArea(src, W, mask, live, x0, y0, x1, y1);
    outlineArea(live, W, H, mask, x0, y0, x1, y1, { rgb: RED, width: 4 });
    expect(live).toEqual(render(src, mask, 4));
  });

  it("changing colour, width or turning it off leaves the selection as it was", () => {
    const src = photo([200, 200, 200], [{ x: 20, y: 20, size: 12, rgb: [40, 90, 220] }]);
    const mask = maskOf([{ x: 20, y: 20, size: 12 }]);
    const before = mask.slice();
    const yellow = render(src, mask, 2, [255, 212, 0]);
    expect(at(yellow, 18, 25)[2]).toBeLessThan(60);
    const thick = render(src, mask, 6);
    expect(isRed(at(thick, 15, 25))).toBe(true);
    expect(isRed(at(render(src, mask, 2), 15, 25))).toBe(false);
    const off = render(src, mask, 3, RED, false);
    expect(isGrey(at(off, 18, 25))).toBe(true);
    expect(at(off, 25, 25)).toEqual([40, 90, 220]);
    expect(mask).toEqual(before);
  });

  it("scales its width with the photo, so the saved picture looks alike at any size", () => {
    expect(outlineWidth(2, 1600, 1200)).toBe(6);
    expect(outlineWidth(1, 760, 1021)).toBe(3);
    expect(outlineWidth(1, 200, 100)).toBe(2);
  });
});
