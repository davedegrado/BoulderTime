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
    const body = method === "GET" || method === "DELETE" ? undefined : bodyOrOpts;
    calls.push({ method, path, body });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler(body);
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { BoulderPage } from "@/pages/BoulderPage";
import { HomePage } from "@/pages/HomePage";
import { ActivityPage } from "@/pages/ActivityPage";

const grades = [{ gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", gradeValueId: "v", label: "6A", rank: 5, colorHex: null }];
const summary = (id: string, over: object = {}) => ({
  id, gymId: "g1", gymName: "Crimp Factory", sectorId: "s1", sectorName: "Cave", photoUrl: "/x.jpg", holdColor: "BLUE", grades,
  status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null, rating: { average: 4.4, count: 12 }, viewer: null, ...over,
});
const detail = (over: object = {}) => ({ ...summary("b1"), gymSlug: "crimp", photoPath: "p", setter: null, viewerRole: null, isFollowing: false, ...over });
const stats = { completed: 3, projects: 1, totalAttempts: 11, completedThisMonth: 2 };

beforeEach(() => { routes.clear(); calls.length = 0; });

describe("Progress tracker", () => {
  it("batches quick attempt taps into one save with the final count", async () => {
    reply("GET", "/api/boulders/b1", detail());
    reply("PUT", "/api/boulders/b1/attempt", (body: unknown) => ({ ...(body as object), completedAt: null, rating: null }));
    renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);

    const plus = await screen.findByRole("button", { name: "One more attempt" });
    await userEvent.click(plus);
    await userEvent.click(plus);
    await userEvent.click(plus);
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(calls.filter((c) => c.method === "PUT")).toHaveLength(0);

    await waitFor(() => expect(calls.filter((c) => c.method === "PUT")).toHaveLength(1), { timeout: 2000 });
    expect(calls.find((c) => c.method === "PUT")?.body).toEqual({ attempts: 3, completed: false });
  });

  it("only allows rating after an attempt, and marking completed counts as an attempt", async () => {
    reply("GET", "/api/boulders/b1", detail());
    reply("PUT", "/api/boulders/b1/attempt", (body: unknown) => ({ ...(body as object), completedAt: "2026-09-17T10:00:00Z", rating: null }));
    renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);

    const stars = await screen.findByRole("radiogroup", { name: "Your rating" });
    expect(within(stars).getByRole("radio", { name: "5 stars" })).toBeDisabled();
    expect(screen.getAllByText("4.4").length).toBeGreaterThan(0);

    await userEvent.click(screen.getByRole("button", { name: "Mark as completed" }));
    expect(screen.getByText("Completed")).toBeInTheDocument();
    await waitFor(() => expect(calls.find((c) => c.method === "PUT")?.body).toEqual({ attempts: 1, completed: true }), { timeout: 2000 });
  });
});

describe("Home", () => {
  it("shows followed gyms, projects to keep trying and recent sends", async () => {
    reply("GET", "/api/users/me", { id: "u1", email: "me@x.com", displayName: "Dave Climber", avatarUrl: null, isPlatformAdmin: false, createdAt: "2026-01-01T00:00:00Z", staffGyms: [], pendingInvitations: 0 });
    reply("GET", "/api/users/me/home", {
      stats,
      gyms: [{ gym: { id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE" }, isFavorite: true, activeBoulders: 42, newThisWeek: 6 }],
      projects: [summary("p1", { viewer: { attempts: 7, completed: false, completedAt: null, rating: null } })],
      freshToTry: [],
      recentCompletions: [{ boulder: summary("r1", { status: "REMOVED" }), attempts: 3, completed: true, completedAt: "2026-09-12T00:00:00Z", updatedAt: "2026-09-12T00:00:00Z", rating: 4 }],
      updates: [],
    });
    renderAt("/", "/", <HomePage />);

    expect(await screen.findByText("2 sent this month. Keep it going.")).toBeInTheDocument();
    expect(screen.getByText("42 boulders · 6 new this week")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Keep trying" })).toBeInTheDocument();
    expect(screen.getByText("7 tries")).toBeInTheDocument();
    const recent = screen.getByRole("heading", { name: "Recent sends" }).closest("section")!;
    expect(within(recent).getByText(/3 attempts/)).toBeInTheDocument();
    expect(within(recent).getByText("Boulder removed")).toBeInTheDocument();
  });
});

describe("Activity", () => {
  it("shows stats, highest grade per system and removed boulders in history", async () => {
    reply("GET", "/api/users/me", { id: "u1", email: "me@x.com", displayName: "Dave", avatarUrl: null, isPlatformAdmin: false, createdAt: "2026-01-01T00:00:00Z", staffGyms: [], pendingInvitations: 0 });
    reply("GET", "/api/users/u1/profile", {
      id: "u1", displayName: "Dave", avatarUrl: null, memberSince: "2026-01-01T00:00:00Z", isMe: true, stats,
      highestGrades: [
        { gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", gymName: "Crimp Factory", label: "7A", rank: 11, colorHex: null },
        { gradeSystemId: "col", systemName: "Colour", systemType: "COLOR", gymName: "Crimp Factory", label: "Black", rank: 5, colorHex: "#1A1A1A" },
      ],
      weekly: Array.from({ length: 12 }, (_, i) => ({ weekStart: `2026-06-${String(i + 1).padStart(2, "0")}`, completed: i })),
      followedGyms: [], recentCompletions: [],
    });
    reply("GET", "/api/users/u1/history", { items: [{ boulder: summary("h1", { status: "REMOVED" }), attempts: 3, completed: true, completedAt: "2026-09-12T00:00:00Z", updatedAt: "2026-09-12T00:00:00Z", rating: null }], page: 1, pageSize: 20, total: 1, hasMore: false });
    renderAt("/activity", "/activity", <ActivityPage />);

    expect(await screen.findByText("Completed", { selector: ".stat__label" })).toBeInTheDocument();
    expect(screen.getByText("7A")).toBeInTheDocument();
    expect(screen.getByText("Black")).toBeInTheDocument();
    expect(screen.getByText("Colour · Crimp Factory")).toBeInTheDocument();
    expect(await screen.findByText("Boulder removed")).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /completions in the last 12 weeks/ })).toBeInTheDocument();
  });
});
