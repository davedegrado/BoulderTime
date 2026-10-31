import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string; body?: unknown }[] = [];
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string, bodyOrOpts?: unknown) => {
    calls.push({ method, path, body: method === "GET" ? undefined : bodyOrOpts });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler(method === "GET" ? undefined : bodyOrOpts);
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { GymPage } from "@/pages/GymPage";

const gym = { id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: null, followerCount: 0, follow: { isFollowing: false, isFavorite: false } };
const sectors = [{ id: "s1", gymId: "g1", name: "Cave", description: null, imageUrl: null, sortOrder: 0, isActive: true }];
const font = { gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", gradeValueId: "v6a", label: "6A", rank: 5, colorHex: null };
const boulder = (id: string, over: object = {}) => ({ id, gymId: "g1", gymName: "Crimp Factory", rating: { average: null, count: 0 }, viewer: null, sectorId: "s1", sectorName: "Cave", photoUrl: `/img/${id}.jpg`, holdColor: "BLUE", grades: [font], status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null, ...over });
const page = (items: unknown[]) => ({ items, page: 1, pageSize: 24, total: items.length, hasMore: false });
const boulderCalls = () => calls.filter((c) => c.path.startsWith("/api/gyms/g1/boulders")).map((c) => c.path);

beforeEach(() => {
  routes.clear();
  calls.length = 0;
  sessionStorage.clear();
  localStorage.clear();
  reply("GET", "/api/gyms/crimp", gym);
  reply("GET", "/api/gyms/g1/sectors", sectors);
  reply("GET", "/api/gyms/g1/grade-systems", []);
  reply("GET", "/api/gyms/g1/tags", []);
  reply("GET", "/api/gyms/g1/boulders", page([boulder("b1", { isNew: true }), boulder("b2")]));
});

describe("New boulders", () => {
  it("carry a New tag, and the filters can show only them", async () => {
    renderAt("/gyms/crimp", "/gyms/:slug", <GymPage />);
    expect(await screen.findByText("New")).toBeInTheDocument();
    expect(screen.getAllByText("New")).toHaveLength(1);
    expect(boulderCalls()[0]).not.toContain("isNew");

    await userEvent.click(screen.getByRole("button", { name: "Filters" }));
    await userEvent.click(within(screen.getByRole("dialog", { name: "Filters" })).getByRole("switch", { name: "New boulders only" }));
    await userEvent.click(screen.getByRole("button", { name: /^Show / }));
    await waitFor(() => expect(boulderCalls().at(-1)).toContain("isNew=true"));
    const inUse = screen.getByRole("list", { name: "Filters in use" });
    await userEvent.click(within(inUse).getByRole("button", { name: "Remove filter New boulders" }));
    await waitFor(() => expect(boulderCalls().at(-1)).not.toContain("isNew"));
  });

  it("are what a notification about several of them opens, whatever was filtered before", async () => {
    sessionStorage.setItem("bt:boulder-filters:g1", JSON.stringify({ gradeValueId: "v6a" }));
    renderAt("/gyms/crimp?sector=s1&new=1", "/gyms/:slug", <GymPage />);
    await screen.findAllByText("Cave");
    const first = boulderCalls()[0]!;
    expect(first).toContain("isNew=true");
    expect(first).toContain("sectorId=s1");
    expect(first).not.toContain("gradeValueId");
  });
});

describe("Following a gym", () => {
  it("is a button that says so", async () => {
    reply("PUT", "/api/gyms/g1/follow", { isFollowing: true, isFavorite: false });
    renderAt("/gyms/crimp", "/gyms/:slug", <GymPage />);
    await userEvent.click(await screen.findByRole("button", { name: "Follow" }));
    await waitFor(() => expect(calls.some((c) => c.method === "PUT" && c.path === "/api/gyms/g1/follow")).toBe(true));
  });

  it("reads Following once followed, with the favourite beside it", async () => {
    reply("GET", "/api/gyms/crimp", { ...gym, follow: { isFollowing: true, isFavorite: false } });
    renderAt("/gyms/crimp", "/gyms/:slug", <GymPage />);
    const following = await screen.findByRole("button", { name: "Following" });
    expect(following).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "Add to favourites" })).toBeInTheDocument();
  });
});
