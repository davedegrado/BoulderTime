import { cardPlacement } from "@/features/boulders/imageResize";

/** Card pictures are 480×384 (5:4). */
describe("card picture placement", () => {
  it("fills the card with a tall photo, keeping its top, centre or bottom", () => {
    // 760×1020: scaled to the card's width, taller than the card.
    const centre = cardPlacement(760, 1020, 480, 384, "FILL_CENTER");
    expect(centre.width).toBeCloseTo(480);
    expect(centre.height).toBeGreaterThan(384);
    expect(centre.y).toBeCloseTo((384 - centre.height) / 2);
    expect(cardPlacement(760, 1020, 480, 384, "FILL_TOP").y).toBe(0);
    const bottom = cardPlacement(760, 1020, 480, 384, "FILL_BOTTOM");
    expect(bottom.y + bottom.height).toBeCloseTo(384);
  });

  it("fills with a wide photo by keeping its middle sideways, whatever the vertical choice", () => {
    for (const mode of ["FILL_TOP", "FILL_CENTER", "FILL_BOTTOM"] as const) {
      const p = cardPlacement(1600, 900, 480, 384, mode);
      expect(p.height).toBeCloseTo(384);
      expect(p.x).toBeCloseTo((480 - p.width) / 2);
      expect(p.y).toBeCloseTo(0);
    }
  });

  it("fits the whole photo inside the card, centred, whatever its shape", () => {
    const tall = cardPlacement(760, 1020, 480, 384, "FIT");
    expect(tall.height).toBeCloseTo(384);
    expect(tall.width).toBeLessThan(480);
    expect(tall.x).toBeCloseTo((480 - tall.width) / 2);
    const wide = cardPlacement(1600, 900, 480, 384, "FIT");
    expect(wide.width).toBeCloseTo(480);
    expect(wide.y).toBeCloseTo((384 - wide.height) / 2);
    const square = cardPlacement(800, 800, 480, 384, "FIT");
    expect(square.width).toBeCloseTo(384);
    expect(square.height).toBeCloseTo(384);
  });
});
