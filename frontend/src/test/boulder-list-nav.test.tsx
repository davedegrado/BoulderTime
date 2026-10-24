import { Link, RouterProvider, createMemoryRouter } from "react-router-dom";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { renderAt } from "@/test/renderApp";
import { ToastProvider } from "@/components/Toast";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string }[] = [];
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string, bodyOrOpts?: unknown) => {
    calls.push({ method, path });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler(method === "GET" ? undefined : bodyOrOpts);
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { GymPage } from "@/pages/GymPage";
import { BoulderPage } from "@/pages/BoulderPage";
import { UserProfilePage } from "@/pages/UserProfilePage";

const gym = { id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: null };
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
});

describe("The gym's boulder list", () => {
  function setup() {
    reply("GET", "/api/gyms/crimp", gym);
    reply("GET", "/api/gyms/g1/sectors", sectors);
    reply("GET", "/api/gyms/g1/grade-systems", []);
    reply("GET", "/api/gyms/g1/boulders", page([
      boulder("b1", { name: "La Fessura", points: 64 }),
      boulder("b2", { sectorName: "A sector with a very long name that must not stretch its row", photoUrl: null, holdColor: "YELLOW", grades: [], points: null }),
    ]));
  }

  it("is sorted by sector unless the person chooses otherwise, and can be reversed", async () => {
    setup();
    renderAt("/gyms/crimp", "/gyms/:slug", <GymPage />);
    await screen.findAllByRole("link", { name: /grade 6A/i });
    expect(boulderCalls()[0]).toContain("sort=SECTOR");

    // The order is one compact button on the toolbar row; it opens the same choice as before.
    await userEvent.click(screen.getByRole("button", { name: "Sort: Sector" }));
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Sort by" }), "POINTS");
    await waitFor(() => expect(boulderCalls().at(-1)).toContain("sort=POINTS"));
    expect(boulderCalls().at(-1)).not.toContain("order=");
    await userEvent.click(screen.getByRole("button", { name: "Reverse the order" }));
    await waitFor(() => expect(boulderCalls().at(-1)).toContain("order=ASC"));
    // There is no sorting by hold colour: it says nothing useful about a boulder.
    const options = within(screen.getByRole("combobox", { name: "Sort by" })).getAllByRole("option").map((o) => o.textContent);
    expect(options).toEqual(["Sector", "Points", "Grade", "Date set"]);
  });

  it("shows a compact list with the same five places in every row, and keeps the sort when switching", async () => {
    setup();
    const { container } = renderAt("/gyms/crimp", "/gyms/:slug", <GymPage />);
    await screen.findAllByRole("link", { name: /grade 6A/i });
    await userEvent.click(screen.getByRole("button", { name: "Sort: Sector" }));
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Sort by" }), "GRADE");
    await userEvent.click(screen.getByRole("radio", { name: "List" }));

    const rows = container.querySelectorAll(".boulder-row");
    expect(rows).toHaveLength(2);
    for (const row of rows) {
      expect([...row.children].map((c) => c.className)).toEqual(["boulder-row__thumb", "boulder-row__grade", "boulder-row__sector", "boulder-row__holds", "boulder-row__points"]);
      expect(row).toHaveAttribute("href", expect.stringMatching(/^\/boulders\/b[12]$/));
    }
    // No name in the compact list; the holds labelled, with a dot of their colour.
    expect(screen.queryByText("La Fessura")).not.toBeInTheDocument();
    expect(within(rows[0] as HTMLElement).getByText("Holds")).toBeInTheDocument();
    expect((rows[0]!.querySelector(".boulder-row__dot") as HTMLElement).style.background).toBe("rgb(47, 111, 219)");
    expect((rows[1]!.querySelector(".boulder-row__dot") as HTMLElement).style.background).toBe("rgb(245, 196, 0)");
    // What a boulder lacks leaves its place empty instead of moving the others.
    expect(rows[0]!.querySelector(".boulder-row__points")).toHaveTextContent("+64 pt");
    expect(rows[1]!.querySelector(".boulder-row__points")).toHaveTextContent("–");
    expect(rows[1]!.querySelector(".boulder-row__grade")).toHaveTextContent("–");
    expect(rows[1]!.querySelector(".boulder-row__thumb img")).toBeNull();
    expect(rows[1]!.querySelector(".boulder-row__thumb svg")).not.toBeNull();

    await userEvent.click(screen.getByRole("radio", { name: "Cards" }));
    expect(screen.getByRole("combobox", { name: "Sort by" })).toHaveValue("GRADE");
    expect(localStorage.getItem("bt:boulder-view")).toBe('"grid"');
  });
});

describe("Back from a boulder", () => {
  const detail = { ...boulder("b1"), gymSlug: "crimp", photoPath: "x.jpg", setter: null, viewerRole: null, isFollowing: false, communityVideosEnabled: false, canAddOfficialBeta: false };

  function renderRouter(initialEntries: string[]) {
    reply("GET", "/api/boulders/b1", detail);
    const router = createMemoryRouter([
      { path: "/users/:id", element: <div><h1>Anna's profile</h1><Link to="/boulders/b1">A boulder she climbed</Link></div> },
      { path: "/gyms/:slug", element: <h1>The gym's page</h1> },
      { path: "/boulders/:id", element: <BoulderPage /> },
    ], { initialEntries });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);
    return router;
  }

  it("returns to the profile it was opened from, not to the gym", async () => {
    const router = renderRouter(["/users/u2"]);
    await userEvent.click(await screen.findByRole("link", { name: "A boulder she climbed" }));
    await userEvent.click(await screen.findByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "Anna's profile" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/users/u2");
  });

  it("goes to the gym only when the boulder was opened directly, without adding a page to go back to", async () => {
    const router = renderRouter(["/boulders/b1"]);
    await screen.findByRole("list", { name: "About this boulder" });
    await userEvent.click(screen.getByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "The gym's page" })).toBeInTheDocument();
    expect(router.state.historyAction).toBe("REPLACE");
  });
});

describe("Back from a person's profile", () => {
  beforeEach(() => {
    reply("GET", "/api/users/u2/profile", {
      id: "u2", displayName: "Anna Verdi", avatarUrl: null, memberSince: "2026-09-01T00:00:00Z", isMe: false, isBlocked: false,
      stats: { completed: 3, projects: 1, completedThisMonth: 2, totalAttempts: 9 }, highestGrades: [], weekly: [], followedGyms: [],
      recentCompletions: [], staffDistinctions: [], favoriteSector: null,
    });
    reply("GET", "/api/users/u2/history", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
  });

  it("returns to the boulder whose climbers it was opened from", async () => {
    const router = createMemoryRouter([
      { path: "/boulders/:id", element: <div><h1>The boulder</h1><Link to="/users/u2">Anna</Link></div> },
      { path: "/users/:id", element: <UserProfilePage /> },
      { path: "/", element: <h1>Home</h1> },
    ], { initialEntries: ["/boulders/b1"] });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);

    await userEvent.click(await screen.findByRole("link", { name: "Anna" }));
    expect(await screen.findByRole("heading", { name: "Anna Verdi" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "The boulder" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/boulders/b1");
  });

  it("is there even when the profile can't be loaded", async () => {
    reply("GET", "/api/users/u2/profile", () => { throw new Error("offline"); });
    const router = createMemoryRouter([{ path: "/users/:id", element: <UserProfilePage /> }, { path: "/", element: <h1>Home</h1> }], { initialEntries: ["/users/u2"] });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);
    await userEvent.click(await screen.findByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "Home" })).toBeInTheDocument();
  });

  it("works while the profile is still loading", async () => {
    reply("GET", "/api/users/u2/profile", () => new Promise(() => {}));
    const router = createMemoryRouter([{ path: "/users/:id", element: <UserProfilePage /> }, { path: "/", element: <h1>Home</h1> }], { initialEntries: ["/users/u2"] });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);
    await userEvent.click(await screen.findByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "Home" })).toBeInTheDocument();
  });

  it("goes Home when the profile was opened directly", async () => {
    const router = createMemoryRouter([
      { path: "/users/:id", element: <UserProfilePage /> },
      { path: "/", element: <h1>Home</h1> },
    ], { initialEntries: ["/users/u2"] });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);

    expect(await screen.findByRole("heading", { name: "Anna Verdi" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "Home" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/");
  });
});

