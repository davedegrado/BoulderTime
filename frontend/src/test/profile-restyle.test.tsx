import { Link, RouterProvider, createMemoryRouter } from "react-router-dom";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { ToastProvider } from "@/components/Toast";

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

import { UserProfilePage } from "@/pages/UserProfilePage";
import { GymPage } from "@/pages/GymPage";
import { PrivacySettings } from "@/features/profile/PrivacySettings";

const DEFAULT = ["stats", "favoriteSector", "gyms", "weekly", "highestGrades", "history"].map((key) => ({ key, visible: key !== "stats" }));
const profile = (over: object = {}) => ({
  id: "u2", displayName: "Anna Verdi", avatarUrl: null, memberSince: "2026-09-01T00:00:00Z", isMe: false, isBlocked: false,
  stats: { completed: 3, projects: 1, completedThisMonth: 2, totalAttempts: 9 }, highestGrades: [], weekly: [],
  followedGyms: [{ id: "g1", slug: "crimp", name: "Crimp Factory", logoUrl: null }],
  recentCompletions: [], staffDistinctions: [], favoriteSector: null,
  isPrivate: false, contentHidden: false, sections: DEFAULT, ...over,
});
const me = (over: object = {}) => ({ id: "u2", email: "anna@example.com", displayName: "Anna Verdi", leaderboardOptOut: false, profilePrivate: false, ...over });

function show(element: ReactElement, path: string, at = path, extra: { path: string; element: ReactElement }[] = []) {
  const router = createMemoryRouter([{ path, element }, { path: "/", element: <h1>Home</h1> }, ...extra], { initialEntries: [at] });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);
  return router;
}
const renderProfile = () => {
  const router = createMemoryRouter([{ path: "/users/:id", element: <UserProfilePage /> }, { path: "/", element: <h1>Home</h1> }], { initialEntries: ["/users/u2"] });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);
};
const openMenu = async () => { await userEvent.click(await screen.findByRole("button", { name: "More actions" })); return screen.getByRole("menu"); };
const menuItems = (menu: HTMLElement) => within(menu).getAllByRole("menuitem").map((i) => i.textContent);

beforeEach(() => {
  routes.clear();
  calls.length = 0;
  reply("GET", "/api/users/u2/history", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
  reply("GET", "/api/users/me", me());
});

describe("A person's profile", () => {
  it("that is private shows a visitor only the name and photo, and says why", async () => {
    reply("GET", "/api/users/u2/profile", profile({ isPrivate: true, contentHidden: true, stats: { completed: 0, projects: 0, completedThisMonth: 0, totalAttempts: 0 }, followedGyms: [] }));
    show(<UserProfilePage />, "/users/:id", "/users/u2");
    expect(await screen.findByRole("heading", { name: "Anna Verdi" })).toBeInTheDocument();
    expect(screen.getByText("This profile is private")).toBeInTheDocument();
    expect(screen.queryByText("Sends")).not.toBeInTheDocument();
    expect(screen.queryByRole("tablist")).not.toBeInTheDocument();
  });

  it("offers a visitor share, copy, block and report, and nothing of the owner's", async () => {
    reply("GET", "/api/users/u2/profile", profile());
    show(<UserProfilePage />, "/users/:id", "/users/u2");
    expect(menuItems(await openMenu())).toEqual(["Share profile", "Copy link", "Block", "Report Anna Verdi"]);
  });

  it("copies the profile's address", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    reply("GET", "/api/users/u2/profile", profile());
    show(<UserProfilePage />, "/users/:id", "/users/u2");
    await userEvent.click(within(await openMenu()).getByRole("menuitem", { name: "Copy link" }));
    await waitFor(() => expect(writeText).toHaveBeenCalledWith(expect.stringMatching(/\/users\/u2$/)));
    expect(await screen.findByText("Link copied")).toBeInTheDocument();
  });

  it("offers its owner edit, customise, privacy and share", async () => {
    reply("GET", "/api/users/u2/profile", profile({ isMe: true }));
    show(<UserProfilePage />, "/users/:id", "/users/u2");
    expect(menuItems(await openMenu())).toEqual(["Edit profile", "Customise profile", "Privacy and leaderboards", "Share profile"]);
  });

  it("leaves out the sections its owner hid, in the order they chose", async () => {
    reply("GET", "/api/users/u2/profile", profile({ sections: [{ key: "gyms", visible: false }, { key: "stats", visible: false }, ...DEFAULT.filter((s) => s.key !== "gyms" && s.key !== "stats")] }));
    show(<UserProfilePage />, "/users/:id", "/users/u2");
    // The gyms are always a tab of their own (even saved as hidden by an earlier build), never a block of the overview.
    expect(await screen.findByRole("tab", { name: "Gyms" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Gyms" })).not.toBeInTheDocument();
    expect(screen.queryByText("Sends")).not.toBeInTheDocument();
  });

  it("shows the statistics only to those who chose them, inside the overview and not above the tabs", async () => {
    reply("GET", "/api/users/u2/profile", profile());
    const { unmount } = renderProfile();
    expect(await screen.findByRole("tab", { name: "Overview" })).toBeInTheDocument();
    expect(screen.queryByText("Sends")).not.toBeInTheDocument();
    unmount();

    reply("GET", "/api/users/u2/profile", profile({ sections: DEFAULT.map((s) => ({ ...s, visible: true })) }));
    renderProfile();
    const stats = await screen.findByRole("region", { name: "Statistics" });
    expect(within(stats).getByText("Sends")).toBeInTheDocument();
    // After the tabs, in the overview's own flow.
    expect(screen.getByRole("tablist").compareDocumentPosition(stats) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("lists the recent activity on short rows, with what identifies each send", async () => {
    reply("GET", "/api/users/u2/profile", profile({ recentCompletions: [{
      boulder: { id: "b1", name: "La Fessura", gymId: "g1", gymName: "Crimp Factory", sectorId: "s1", sectorName: "Cave", photoUrl: "/b1.jpg", holdColor: "BLUE", points: 64, status: "ACTIVE",
        grades: [{ gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", gradeValueId: "v6a", label: "6A", rank: 5, colorHex: null }] },
      completed: true, completedAt: "2026-10-20T10:00:00Z", updatedAt: "2026-10-20T10:00:00Z", attempts: 3,
    }] }));
    renderProfile();
    const recent = await screen.findByRole("region", { name: "Recent activity" });
    const row = within(recent).getByRole("link");
    expect(row).toHaveAttribute("href", "/boulders/b1");
    expect(row).toHaveTextContent("La Fessura");
    expect(row).toHaveTextContent("+64 pt");
    expect(row).toHaveTextContent("Cave · Crimp Factory");
    expect(row).toHaveTextContent("3 attempts");
    expect(row).toHaveTextContent(/blue/i);
    await userEvent.click(within(recent).getByRole("button", { name: "See all" }));
    expect(await screen.findByRole("tab", { name: "Activity", selected: true })).toBeInTheDocument();
  });

  it("is customised by its owner: hide, move, save; and back to the default", async () => {
    reply("GET", "/api/users/u2/profile", profile({ isMe: true }));
    reply("PUT", "/api/users/me/profile-sections", (body: unknown) => (body as { sections: unknown }).sections);
    reply("DELETE", "/api/users/me/profile-sections", DEFAULT);
    show(<UserProfilePage />, "/users/:id", "/users/u2");
    await userEvent.click(within(await openMenu()).getByRole("menuitem", { name: "Customise profile" }));

    await userEvent.click(screen.getByRole("switch", { name: "Statistics" }));
    // The gyms tab can't be switched off: no switch for it.
    expect(screen.queryByRole("switch", { name: /Gyms/ })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Move Recent activity up" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));
    await waitFor(() => expect(calls.some((c) => c.method === "PUT")).toBe(true));
    const sent = (calls.find((c) => c.method === "PUT")!.body as { sections: { key: string; visible: boolean }[] }).sections;
    // The gyms aren't in the order (they have their own tab, always there); their entry is kept as it was.
    expect(screen.queryByRole("button", { name: "Move Gyms up" })).not.toBeInTheDocument();
    expect(sent.map((s) => s.key)).toEqual(["stats", "favoriteSector", "weekly", "history", "highestGrades", "gyms"]);
    // Off by default, so the switch turned them on.
    expect(sent.find((s) => s.key === "stats")!.visible).toBe(true);

    await userEvent.click(within(await openMenu()).getByRole("menuitem", { name: "Customise profile" }));
    await userEvent.click(screen.getByRole("button", { name: "Default" }));
    await waitFor(() => expect(calls.some((c) => c.method === "DELETE" && c.path === "/api/users/me/profile-sections")).toBe(true));
  });

  it("from an older server keeps the usual layout and offers nothing to customise", async () => {
    const { sections: _s, isPrivate: _p, contentHidden: _c, ...old } = profile({ isMe: true });
    reply("GET", "/api/users/u2/profile", old);
    show(<UserProfilePage />, "/users/:id", "/users/u2");
    // The default arrangement: the gyms in their tab, the statistics only for those who choose them.
    expect(await screen.findByRole("tab", { name: "Gyms" })).toBeInTheDocument();
    expect(screen.queryByText("Sends")).not.toBeInTheDocument();
    expect(menuItems(await openMenu())).toEqual(["Edit profile", "Share profile"]);
  });
});

describe("Privacy and leaderboards", () => {
  it("are two separate switches, each with its own request", async () => {
    reply("PUT", "/api/users/me/profile-privacy", undefined);
    reply("PUT", "/api/users/me/leaderboard-visibility", undefined);
    show(<PrivacySettings />, "/profile");

    await userEvent.click(await screen.findByRole("radio", { name: /Private profile/ }));
    await waitFor(() => expect(calls.filter((c) => c.method === "PUT").map((c) => [c.path, c.body]))
      .toEqual([["/api/users/me/profile-privacy", { private: true }]]));

    await userEvent.click(screen.getByRole("switch", { name: "Show me in leaderboards and lists" }));
    await waitFor(() => expect(calls.filter((c) => c.method === "PUT").map((c) => [c.path, c.body]))
      .toEqual([["/api/users/me/profile-privacy", { private: true }], ["/api/users/me/leaderboard-visibility", { hidden: true }]]));
  });
});

describe("The gym's page header", () => {
  const gym = { id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: null };
  beforeEach(() => {
    reply("GET", "/api/gyms/crimp", gym);
    reply("GET", "/api/gyms/g1/sectors", []);
    reply("GET", "/api/gyms/g1/grade-systems", []);
    reply("GET", "/api/gyms/g1/boulders", { items: [], page: 1, pageSize: 24, total: 0, hasMore: false });
  });

  it("goes back where the gym was opened from", async () => {
    const router = show(<div><h1>Explore</h1><Link to="/gyms/crimp">Crimp</Link></div>, "/explore", "/explore", [{ path: "/gyms/:slug", element: <GymPage /> }]);
    await userEvent.click(await screen.findByRole("link", { name: "Crimp" }));
    expect(await screen.findByRole("heading", { name: "Crimp Factory" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "Explore" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/explore");
  });

  it("goes Home when the gym was opened directly", async () => {
    show(<GymPage />, "/gyms/:slug", "/gyms/crimp");
    await userEvent.click(await screen.findByRole("button", { name: "Back" }));
    expect(await screen.findByRole("heading", { name: "Home" })).toBeInTheDocument();
  });

  it("shares the gym, and offers no management to a climber", async () => {
    const share = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "share", { value: share, configurable: true });
    show(<GymPage />, "/gyms/:slug", "/gyms/crimp");
    const menu = await openMenu();
    expect(menuItems(menu)).toEqual(["Share gym", "Copy link"]);
    await userEvent.click(within(menu).getByRole("menuitem", { name: "Share gym" }));
    await waitFor(() => expect(share).toHaveBeenCalledWith({ title: "Crimp Factory", url: expect.stringMatching(/\/gyms\/crimp$/) }));
    Reflect.deleteProperty(navigator, "share");
  });
});
