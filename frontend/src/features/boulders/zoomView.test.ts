import { describe, expect, it } from "vitest";
import { clampView, fitted, fitView, MAX_ZOOM, pan, pinch, zoomAt } from "@/features/boulders/zoomView";

const box = { w: 400, h: 600 };
const base = fitted({ w: 1600, h: 1200 }, box); // a landscape photo in a portrait box: 400 × 300

describe("Zooming the photo", () => {
  it("fits the whole photo, centred, to start with", () => {
    expect(base).toEqual({ w: 400, h: 300 });
    expect(fitView(base, box)).toEqual({ scale: 1, x: 0, y: 150 });
  });

  it("keeps the point under the fingers where it is while zooming", () => {
    const start = fitView(base, box);
    const at = { x: 100, y: 225 }; // a quarter across, half way down the photo
    const v = zoomAt(start, 4, at, base, box);
    expect(v.scale).toBe(4);
    // The same point of the photo is still under (100, 225).
    expect((at.x - v.x) / v.scale).toBeCloseTo(100);
    expect((at.y - v.y) / v.scale).toBeCloseTo(75);
  });

  it("follows two fingers: spreading them zooms, moving them moves the photo", () => {
    const start = { view: fitView(base, box), mid: { x: 200, y: 300 }, spread: 100 };
    const v = pinch(start, { x: 220, y: 300 }, 300, base, box);
    expect(v.scale).toBe(3);
    // The photo point that was between the fingers (200, 150 in fitted pixels) is between them again.
    expect((220 - v.x) / v.scale).toBeCloseTo(200);
  });

  it("never zooms out past the whole photo or in past the limit, and never leaves an empty edge", () => {
    expect(zoomAt(fitView(base, box), 0.2, { x: 0, y: 0 }, base, box)).toEqual(fitView(base, box));
    expect(zoomAt(fitView(base, box), 50, { x: 0, y: 0 }, base, box).scale).toBe(MAX_ZOOM);
    const zoomed = clampView({ scale: 2, x: 0, y: 0 }, base, box); // 800 × 600: as tall as the box
    expect(pan(zoomed, 500, 500, base, box)).toEqual({ scale: 2, x: 0, y: 0 });
    expect(pan(zoomed, -1000, 0, base, box).x).toBe(400 - 800);
  });
});
