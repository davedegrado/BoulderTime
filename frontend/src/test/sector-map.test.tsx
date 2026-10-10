import { Route } from "react-router-dom";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string; body?: unknown }[] = [];
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string, bodyOrOpts?: unknown) => {
    const body = method === "GET" || method === "DELETE" ? undefined : bodyOrOpts;
    calls.push({ method, path, body });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler(body);
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { GymPage } from "@/pages/GymPage";
import { ManageLayout } from "@/pages/manage/ManageLayout";
import { ManageSectorMap } from "@/pages/manage/ManageSectorMap";

const square = (x: number, y: number, s: number) => ({
  points: [{ x, y }, { x: x + s, y }, { x: x + s, y: y + s }, { x, y: y + s }], label: { x: x + s / 2, y: y + s / 2 },
});
const gym = {
  id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null,
  website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: null, followerCount: 0,
  floorPlanUrl: "/plan.jpg", floorPlanWidth: 1000, floorPlanHeight: 1200,
};
const sectors = [
  { id: "s1", gymId: "g1", name: "Cave", description: "Steep and dark", imageUrl: null, sortOrder: 0, isActive: true, isFollowing: false, zone: square(0.1, 0.1, 0.3), activeBoulders: 12, newThisWeek: 3 },
  { id: "s2", gymId: "g1", name: "Slab", description: null, imageUrl: null, sortOrder: 1, isActive: true, isFollowing: true, zone: null, activeBoulders: 0, newThisWeek: 0 },
];
const page = { items: [], page: 1, pageSize: 24, total: 0, hasMore: false };

beforeEach(() => { routes.clear(); calls.length = 0; });

describe("The sector map, for climbers", () => {
  function setup() {
    reply("GET", "/api/gyms/crimp", gym);
    reply("GET", "/api/gyms/g1/sectors", sectors);
    reply("GET", "/api/gyms/g1/grade-systems", []);
    reply("GET", "/api/gyms/g1/boulders", page);
    renderAt("/gyms/crimp?tab=sectors", "/gyms/:slug", <GymPage />);
  }

  it("draws the sectors on the plan, and a tap opens the sector with its boulders", async () => {
    setup();
    const zone = await screen.findByRole("button", { name: "Sector Cave" });
    // The list stays under the map, with the counts and the bell.
    expect(screen.getByText("12 on the wall · 3 new")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Unfollow Slab" })).toBeInTheDocument();

    await userEvent.click(zone);
    const sheet = screen.getByRole("dialog", { name: "Cave" });
    expect(within(sheet).getByText("Steep and dark")).toBeInTheDocument();
    await userEvent.click(within(sheet).getByRole("button", { name: "See the boulders" }));

    // The boulders tab, already narrowed to the sector, with the filter there to remove.
    await waitFor(() => expect(calls.some((c) => c.path.startsWith("/api/gyms/g1/boulders") && c.path.includes("sectorId=s1"))).toBe(true));
    expect(await screen.findByRole("button", { name: "Remove filter Cave" })).toBeInTheDocument();
  });

  it("follows a sector from its sheet, the same as from the list", async () => {
    setup();
    reply("PUT", "/api/sectors/s1/follow", undefined);
    await userEvent.click(await screen.findByRole("button", { name: /^Cave/ }));
    await userEvent.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Follow" }));
    expect(calls.some((c) => c.method === "PUT" && c.path === "/api/sectors/s1/follow")).toBe(true);
  });
});

describe("Drawing the sectors, for staff", () => {
  beforeEach(() => {
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockReturnValue({ left: 0, top: 0, width: 1000, height: 1000, right: 1000, bottom: 1000, x: 0, y: 0, toJSON: () => ({}) } as DOMRect);
  });
  afterEach(() => vi.restoreAllMocks());

  it("draws a sector corner by corner, closes it on the first corner and saves it", async () => {
    reply("GET", "/api/gyms/crimp", { ...gym, viewerRole: "STAFF" });
    reply("GET", "/api/gyms/g1/sectors", sectors);
    reply("PUT", "/api/sectors/s2/zone", (body: unknown) => ({ ...sectors[1], zone: { ...(body as object), label: { x: 0.5, y: 0.5 } } }));
    renderAt("/manage/crimp/sectors/map", "/manage/:slug", <ManageLayout />, <Route path="sectors/map" element={<ManageSectorMap />} />);

    // It starts on the first sector not drawn yet.
    expect(await screen.findByRole("radio", { name: "Slab" })).toHaveAttribute("aria-checked", "true");
    const canvas = document.querySelector(".zone-editor__canvas") as HTMLElement;
    fireEvent.click(canvas, { clientX: 500, clientY: 100 });
    fireEvent.click(canvas, { clientX: 900, clientY: 100 });
    fireEvent.click(canvas, { clientX: 900, clientY: 500 });
    expect(screen.getByRole("button", { name: "Save the sector" })).toBeDisabled();

    const first = screen.getByRole("button", { name: "First corner: tap to close" });
    fireEvent.pointerDown(first, { pointerId: 1 });
    fireEvent.pointerUp(first, { pointerId: 1 });
    await userEvent.click(screen.getByRole("button", { name: "Save the sector" }));

    const put = calls.find((c) => c.method === "PUT" && c.path === "/api/sectors/s2/zone");
    expect(put?.body).toEqual({ points: [{ x: 0.5, y: 0.1 }, { x: 0.9, y: 0.1 }, { x: 0.9, y: 0.5 }], label: null });
  });

  it("asks for the floor plan first", async () => {
    reply("GET", "/api/gyms/crimp", { ...gym, viewerRole: "STAFF", floorPlanUrl: null, floorPlanWidth: null, floorPlanHeight: null });
    reply("GET", "/api/gyms/g1/sectors", sectors);
    renderAt("/manage/crimp/sectors/map", "/manage/:slug", <ManageLayout />, <Route path="sectors/map" element={<ManageSectorMap />} />);
    expect(await screen.findByText("Add your gym's floor plan")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Upload the floor plan" })).toBeInTheDocument();
  });
});
