import { describe, expect, it } from "vitest";
import { canDetect, compose, detectHolds, featherMask, paintStroke, rgbToHsv } from "@/features/boulders/holdHighlight";

type RGB = [number, number, number];
const W = 120, H = 100;

/** A grey wall with squares of colour on it, as an RGBA pixel array. */
function wall(squares: { x: number; y: number; size: number; rgb: RGB }[], wallRgb: RGB = [190, 190, 185]) {
  const px = new Uint8ClampedArray(W * H * 4);
  for (let p = 0; p < W * H; p++) px.set([...wallRgb, 255], p * 4);
  for (const s of squares)
    for (let y = s.y; y < s.y + s.size; y++)
      for (let x = s.x; x < s.x + s.size; x++) px.set([...s.rgb, 255], (y * W + x) * 4);
  return px;
}
const on = (mask: Uint8Array, x: number, y: number) => mask[y * W + x] === 255;

describe("Finding the holds of a colour", () => {
  const photo = wall([
    { x: 10, y: 10, size: 16, rgb: [245, 200, 0] },   // a yellow hold in the light
    { x: 60, y: 10, size: 16, rgb: [120, 98, 8] },    // a yellow hold in shadow
    { x: 10, y: 60, size: 16, rgb: [215, 50, 40] },   // a red hold
    { x: 90, y: 80, size: 1, rgb: [245, 200, 0] },    // a yellow speck in the wall's texture
  ]);

  it("finds the holds of that colour in light and in shadow, and nothing else", () => {
    const mask = detectHolds(photo, W, H, "YELLOW");
    expect(on(mask, 18, 18)).toBe(true);
    expect(on(mask, 68, 18)).toBe(true);
    expect(on(mask, 18, 68)).toBe(false); // red
    expect(on(mask, 40, 40)).toBe(false); // the wall
    expect(on(mask, 90, 80)).toBe(false); // a speck is not a hold
  });

  it("finds the red one when asked for red", () => {
    const mask = detectHolds(photo, W, H, "RED");
    expect(on(mask, 18, 68)).toBe(true);
    expect(on(mask, 18, 18)).toBe(false);
  });

  it("keeps a hold cut by the edge of the photo whole up to the edge", () => {
    const mask = detectHolds(wall([{ x: 0, y: 0, size: 16, rgb: [245, 200, 0] }]), W, H, "YELLOW");
    expect(on(mask, 0, 0)).toBe(true);
    expect(on(mask, 15, 0)).toBe(true);
  });

  it("finds nothing by itself for mixed holds: those are painted by hand", () => {
    expect(canDetect("MIXED")).toBe(false);
    expect(detectHolds(photo, W, H, "MIXED").some((v) => v > 0)).toBe(false);
  });

  it("reads colours as hue, saturation and brightness", () => {
    const [h, s, v] = rgbToHsv(255, 0, 0);
    expect([Math.round(h), s, v]).toEqual([0, 1, 1]);
    expect(Math.round(rgbToHsv(0, 0, 255)[0])).toBe(240);
    expect(rgbToHsv(128, 128, 128)[1]).toBe(0);
  });
});

describe("Correcting by hand and making the picture", () => {
  it("adds what was missed and takes out another boulder's hold of the same colour", () => {
    const photo = wall([{ x: 10, y: 10, size: 16, rgb: [245, 200, 0] }, { x: 60, y: 10, size: 16, rgb: [245, 200, 0] }]);
    const mask = detectHolds(photo, W, H, "YELLOW");
    paintStroke(mask, W, H, { x: 58, y: 8 }, { x: 78, y: 28 }, 12, 0);   // the other boulder's hold, painted out
    paintStroke(mask, W, H, { x: 40, y: 60 }, { x: 60, y: 60 }, 4, 255); // a hold the camera saw as grey, painted in
    expect(on(mask, 18, 18)).toBe(true);
    expect(on(mask, 68, 18)).toBe(false);
    expect(on(mask, 50, 60)).toBe(true);
    expect(on(mask, 50, 70)).toBe(false);
  });

  it("keeps the colour where the mask says and turns the rest grey, from the untouched photo", () => {
    const photo = wall([{ x: 10, y: 10, size: 16, rgb: [245, 200, 0] }, { x: 10, y: 60, size: 16, rgb: [215, 50, 40] }]);
    const mask = detectHolds(photo, W, H, "YELLOW");
    const out = new Uint8ClampedArray(photo.length);
    compose(photo, W, H, mask, W, H, out);
    const px = (x: number, y: number) => Array.from(out.slice((y * W + x) * 4, (y * W + x) * 4 + 3));
    expect(px(18, 18)).toEqual([245, 200, 0]);
    const red = px(18, 68);
    expect(red[0]).toBe(red[1]);
    expect(red[1]).toBe(red[2]);
    // The photo itself is never changed: the next result starts from it again.
    expect(Array.from(photo.slice((68 * W + 18) * 4, (68 * W + 18) * 4 + 3))).toEqual([215, 50, 40]);
  });

  it("reads a mask painted on a smaller preview at the right place of the full photo", () => {
    const photo = wall([{ x: 10, y: 10, size: 40, rgb: [245, 200, 0] }]);
    const small = new Uint8Array((W / 2) * (H / 2));
    paintStroke(small, W / 2, H / 2, { x: 15, y: 15 }, { x: 15, y: 15 }, 8, 255);
    const out = new Uint8ClampedArray(photo.length);
    compose(photo, W, H, featherMask(small, W / 2, H / 2, 1), W / 2, H / 2, out);
    expect(Array.from(out.slice((30 * W + 30) * 4, (30 * W + 30) * 4 + 3))).toEqual([245, 200, 0]);
    const corner = Array.from(out.slice((48 * W + 48) * 4, (48 * W + 48) * 4 + 3));
    expect(corner[0]).toBe(corner[2]);
  });
});
