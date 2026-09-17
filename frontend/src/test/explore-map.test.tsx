import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: null, initializing: false }) }));

const calls: string[] = [];
vi.mock("@/lib/api", () => ({
  api: {
    get: async (path: string) => {
      calls.push(path);
      if (path.startsWith("/api/gyms/map")) return [{ id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, latitude: 45.45, longitude: 9.16 }];
      return {
        items: [
          { id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", latitude: 45.45, longitude: 9.16, distanceKm: 1.24 },
          { id: "g2", slug: "nopin", name: "No Pin Gym", city: "Pisa", logoUrl: null, coverImageUrl: null, status: "ACTIVE", latitude: null, longitude: null, distanceKm: null },
        ],
        page: 1, pageSize: 20, total: 2, hasMore: false,
      };
    },
  },
}));

// Leaflet can't run in jsdom: a stub map reports its viewport and exposes what it was asked to show.
const mapProps: { pins: unknown[]; userPosition: unknown; focus: unknown }[] = [];
vi.mock("@/features/map/GymMap", () => ({
  GymMap: (props: { pins: unknown[]; userPosition: unknown; focus: unknown; onBounds: (b: object) => void }) => {
    mapProps.push(props);
    if (mapProps.length === 1) queueMicrotask(() => props.onBounds({ south: 44, west: 8, north: 46, east: 10 }));
    return <div data-testid="map">{props.pins.length} pins</div>;
  },
}));

import { ExplorePage } from "@/pages/ExplorePage";

function mockGeolocation(result: "granted" | "denied") {
  Object.defineProperty(navigator, "geolocation", {
    configurable: true,
    value: {
      getCurrentPosition: (ok: PositionCallback, fail: PositionErrorCallback) =>
        result === "granted"
          ? ok({ coords: { latitude: 45.4642, longitude: 9.19, accuracy: 20 } } as GeolocationPosition)
          : fail({ code: 1, PERMISSION_DENIED: 1 } as GeolocationPositionError),
    },
  });
}

beforeEach(() => { calls.length = 0; mapProps.length = 0; localStorage.clear(); });

describe("Explore map", () => {
  it("centres on the user's position, loads pins for the viewport and lists nearest gyms first", async () => {
    mockGeolocation("granted");
    renderAt("/explore", "/explore", <ExplorePage />);

    expect(await screen.findByText("1 pins")).toBeInTheDocument();
    await waitFor(() => expect(calls).toContain("/api/gyms/map?south=44&west=8&north=46&east=10"));
    await waitFor(() => expect(calls.some((c) => c.startsWith("/api/gyms?q=&page=1&pageSize=20&lat=45.4642&lng=9.19"))).toBe(true));
    expect(mapProps.at(-1)!.userPosition).toEqual({ lat: 45.4642, lng: 9.19 });

    expect(await screen.findByText(/nearest first/)).toBeInTheDocument();
    const crimp = screen.getByText("Crimp Factory").closest(".gym-grid__item")! as HTMLElement;
    const showOnMap = within(crimp).getByRole("button", { name: /show on map · 1\.2 km/i });
    const noPin = screen.getByText("No Pin Gym").closest(".gym-grid__item")! as HTMLElement;
    expect(within(noPin).queryByRole("button", { name: /show on map/i })).not.toBeInTheDocument();

    window.scrollTo = vi.fn();
    await userEvent.click(showOnMap);
    expect(mapProps.at(-1)!.focus).toEqual({ lat: 45.45, lng: 9.16, zoom: 15 });
  });

  it("falls back to Italy and explains how to enable location when it's denied", async () => {
    mockGeolocation("denied");
    renderAt("/explore", "/explore", <ExplorePage />);
    expect(await screen.findByText(/location is off, so the map shows italy/i)).toBeInTheDocument();
    expect(mapProps[0]!.userPosition).toBeNull();
  });
});
