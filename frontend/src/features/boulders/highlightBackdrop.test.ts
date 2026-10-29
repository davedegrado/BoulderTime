import { describe, expect, it } from "vitest";
import { backdropGrey, compose } from "@/features/boulders/holdHighlight";
import { highlightSettings, readHighlightSettings } from "@/features/boulders/HoldHighlighter";

describe("The black and white part, darker or lighter", () => {
  it("leaves the grey as it is at 0, and never reaches black or white", () => {
    expect(backdropGrey(128, 0)).toBe(128);
    expect(backdropGrey(128, -0.5)).toBeLessThan(128);
    expect(backdropGrey(128, 0.5)).toBeGreaterThan(128);
    expect(backdropGrey(200, -1)).toBeGreaterThan(0);
    expect(backdropGrey(50, 1)).toBeLessThan(255);
  });

  it("changes the grey only: the holds in colour stay exactly as in the photo", () => {
    // Two pixels: one kept in colour by the mask, one turned grey.
    const src = new Uint8ClampedArray([200, 40, 30, 255, 200, 40, 30, 255]);
    const mask = new Uint8Array([255, 0]);
    const plain = new Uint8ClampedArray(8), dark = new Uint8ClampedArray(8), light = new Uint8ClampedArray(8);
    compose(src, 2, 1, mask, 2, 1, plain);
    compose(src, 2, 1, mask, 2, 1, dark, -0.6);
    compose(src, 2, 1, mask, 2, 1, light, 0.6);
    expect([...dark.slice(0, 4)]).toEqual([200, 40, 30, 255]);
    expect([...light.slice(0, 4)]).toEqual([200, 40, 30, 255]);
    expect(dark[4]!).toBeLessThan(plain[4]!);
    expect(light[4]!).toBeGreaterThan(plain[4]!);
    expect(dark[4]).toBe(dark[5]); // still grey
  });

  it("is saved with the highlight, and a highlight saved before it opens as it always looked", () => {
    const outline = { on: true, color: "red", level: 2 } as const;
    expect(readHighlightSettings(highlightSettings("WHITE", outline, -60))?.backdrop).toBe(-60);
    expect(readHighlightSettings(JSON.stringify({ color: "WHITE", outline }))?.backdrop).toBe(0);
    expect(readHighlightSettings(JSON.stringify({ color: "WHITE", outline, backdrop: 900 }))?.backdrop).toBe(100);
    expect(readHighlightSettings(JSON.stringify({ color: "WHITE", outline, backdrop: "dark" }))?.backdrop).toBe(0);
  });
});
