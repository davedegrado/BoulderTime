/**
 * The position only ever leaves the device coarsened (store declarations: "approximate location").
 * A 0.03° grid is about 3.3 km north–south and 2.4 km east–west in Italy (≈ 8 km²): above the 3 km² Google Play calls
 * approximate, and with two decimals at most, which Apple counts as coarse. Plenty to sort gyms by distance.
 */
const STEP = 0.03;

const toGrid = (n: number) => Number((n / STEP).toFixed(6));
const fromGrid = (cells: number) => Number((cells * STEP).toFixed(2));

/** The nearest grid point. */
export function coarsen(n: number): number {
  return fromGrid(Math.round(toGrid(n)));
}

/** The grid-aligned box that contains the given one, so its centre doesn't give away where the map was opened. */
export function coarsenBounds<T extends { south: number; west: number; north: number; east: number }>(b: T): T {
  const clamp = (n: number, max: number) => Math.min(max, Math.max(-max, n));
  return {
    ...b,
    south: clamp(fromGrid(Math.floor(toGrid(b.south))), 90),
    west: clamp(fromGrid(Math.floor(toGrid(b.west))), 180),
    north: clamp(fromGrid(Math.ceil(toGrid(b.north))), 90),
    east: clamp(fromGrid(Math.ceil(toGrid(b.east))), 180),
  };
}
