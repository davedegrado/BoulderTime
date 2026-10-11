import { describe, expect, it } from "vitest";
import { coarsen, coarsenBounds } from "./coarse";

describe("coarsen", () => {
  it("snaps to a 0.03° grid with at most two decimals", () => {
    expect(coarsen(45.464211)).toBe(45.45);
    expect(coarsen(9.18951)).toBe(9.18);
    expect(coarsen(-0.01) + 0).toBe(0);
    for (const n of [45.4642, 9.1895, 41.9028, 12.4964, -33.8688, 151.2093]) {
      const c = coarsen(n);
      expect(Math.abs(c - n)).toBeLessThanOrEqual(0.015 + 1e-9);
      expect(String(Math.abs(c)).split(".")[1]?.length ?? 0).toBeLessThanOrEqual(2);
    }
  });

  it("widens bounds outwards to the grid", () => {
    const b = coarsenBounds({ south: 45.4512, west: 9.1501, north: 45.4799, east: 9.2299 });
    expect(b).toEqual({ south: 45.45, west: 9.15, north: 45.48, east: 9.24 });
    expect(coarsenBounds({ south: -90, west: -180, north: 90, east: 180 })).toEqual({ south: -90, west: -180, north: 90, east: 180 });
  });
});
