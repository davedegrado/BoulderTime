import { describe, expect, it } from "vitest";
import { removeAreaAt, selectHoldAt } from "@/features/boulders/holdHighlight";

type RGB = [number, number, number];
const W = 200, H = 160;

/** A photo as an RGBA pixel array: a wall, then shapes painted over it in order. */
function photo(wallRgb: RGB, shapes: { inside: (x: number, y: number) => boolean; rgb: RGB }[], w = W, h = H) {
  const px = new Uint8ClampedArray(w * h * 4);
  for (let y = 0; y < h; y++)
    for (let x = 0; x < w; x++) {
      let rgb = wallRgb;
      for (const s of shapes) if (s.inside(x, y)) rgb = s.rgb;
      px.set([...rgb, 255], (y * w + x) * 4);
    }
  return px;
}
const disc = (cx: number, cy: number, r: number) => (x: number, y: number) => (x - cx) ** 2 + (y - cy) ** 2 <= r * r;
const on = (mask: Uint8Array, x: number, y: number) => mask[y * W + x] === 255;
const count = (mask: Uint8Array) => mask.reduce((a, v) => a + (v ? 1 : 0), 0);

describe("The wand: a tap selects one hold, whole", () => {
  // A red hold with a patch of chalk and a bolt hole in it, on a light wall; another red hold further on.
  const wall = photo([214, 208, 198], [
    { inside: disc(60, 60, 18), rgb: [205, 45, 40] },
    { inside: disc(66, 54, 6), rgb: [232, 226, 222] }, // chalk
    { inside: disc(55, 66, 2), rgb: [40, 36, 34] },    // the bolt hole
    { inside: disc(150, 110, 15), rgb: [205, 45, 40] },
  ]);

  it("takes the hold it is tapped on, chalk and bolt hole included, and nothing of the wall or the other hold", () => {
    const mask = new Uint8Array(W * H);
    const area = selectHoldAt(wall, W, H, mask, 50, 60);
    expect(area).not.toBeNull();
    expect(on(mask, 50, 60)).toBe(true);
    expect(on(mask, 66, 54)).toBe(true); // the chalk, enclosed by the hold
    expect(on(mask, 55, 66)).toBe(true); // the bolt hole
    expect(on(mask, 60, 90)).toBe(false); // the wall
    expect(on(mask, 150, 110)).toBe(false); // the other hold, not tapped
    expect(count(mask)).toBeLessThan(Math.PI * 19 * 19);
  });

  it("finds the hold's colour when the finger lands on its chalk", () => {
    // The look around the tap scales with the photo, so this one is as big as the editor's: a fingertip of chalk on a
    // big red hold, and around it the hold's colour.
    const w = 1600, h = 1200;
    const chalked = photo([214, 208, 198], [
      { inside: disc(400, 400, 120), rgb: [205, 45, 40] },
      { inside: disc(430, 380, 7), rgb: [232, 226, 222] },
    ], w, h);
    const mask = new Uint8Array(w * h);
    expect(selectHoldAt(chalked, w, h, mask, 430, 380)).not.toBeNull();
    expect(mask[400 * w + 320]).toBe(255); // the hold, far from the tap
    expect(mask[380 * w + 430]).toBe(255); // the chalk tapped
    expect(mask[600 * w + 400]).toBe(0); // the wall
  });

  it("stops where a bright hold meets a darker wall of the same hue", () => {
    // A lime hold on a dark green panel: the same hue, apart in lightness.
    const green = photo([40, 95, 55], [{ inside: disc(100, 80, 20), rgb: [110, 235, 60] }]);
    const mask = new Uint8Array(W * H);
    expect(selectHoldAt(green, W, H, mask, 100, 80)).not.toBeNull();
    expect(on(mask, 100, 80)).toBe(true);
    expect(on(mask, 100, 110)).toBe(false);
    expect(count(mask)).toBeLessThan(Math.PI * 21 * 21);
  });

  it("adds nothing where the colour runs on as far as a wall: that is no hold", () => {
    const panel = photo([214, 208, 198], [{ inside: (x) => x < 120, rgb: [40, 95, 55] }]);
    const mask = new Uint8Array(W * H);
    expect(selectHoldAt(panel, W, H, mask, 40, 80)).toBeNull();
    expect(count(mask)).toBe(0);
  });

  it("selects a white hold when the boulder's holds are white, without looking for a colour around it", () => {
    const white = photo([150, 146, 140], [
      { inside: disc(70, 70, 14), rgb: [246, 245, 240] },
      { inside: disc(92, 70, 6), rgb: [205, 45, 40] }, // a red hold right beside it
    ]);
    const mask = new Uint8Array(W * H);
    expect(selectHoldAt(white, W, H, mask, 70, 70, false)).not.toBeNull();
    expect(on(mask, 70, 70)).toBe(true);
    expect(on(mask, 92, 70)).toBe(false);
    expect(on(mask, 30, 30)).toBe(false);
  });

  it("does nothing outside the photo", () => {
    expect(selectHoldAt(wall, W, H, new Uint8Array(W * H), -5, 10)).toBeNull();
  });
});

describe("The wand: a tap on a part in colour takes it away", () => {
  const selection = () => {
    const mask = new Uint8Array(W * H);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) if (disc(50, 50, 20)(x, y) || disc(150, 110, 15)(x, y)) mask[y * W + x] = 255;
    return mask;
  };

  it("takes the whole area tapped away and leaves the others", () => {
    const mask = selection();
    const area = removeAreaAt(mask, W, H, 50, 50);
    expect(area).toEqual({ x0: 30, y0: 30, x1: 71, y1: 71 });
    expect(on(mask, 40, 50)).toBe(false);
    expect(on(mask, 150, 110)).toBe(true);
  });

  it("reaches an area a fingertip away, and finds nothing to take on bare wall", () => {
    const mask = selection();
    expect(removeAreaAt(mask, W, H, 50, 72)).not.toBeNull(); // just below the edge
    expect(on(mask, 50, 50)).toBe(false);
    expect(removeAreaAt(mask, W, H, 100, 10)).toBeNull();
    expect(on(mask, 150, 110)).toBe(true);
  });
});
