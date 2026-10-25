import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string }[] = [];
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string) => {
    calls.push({ method, path });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler();
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { BoulderPage } from "@/pages/BoulderPage";
import { HomePage } from "@/pages/HomePage";
import { BoulderFiltersBar } from "@/features/boulders/BoulderFilters";

const grades = [{ gradeSystemId: "col", systemName: "Colour", systemType: "COLOR", gradeValueId: "vg", label: "Green", rank: 1, colorHex: "#2E9E4F" }];
const card = (id: string, over: object = {}) => ({
  id, gymId: "g1", gymName: "Crimp Factory", sectorId: "s1", sectorName: "Onda", photoUrl: `/${id}.jpg`, holdColor: "BLUE", grades,
  status: "ACTIVE", createdAt: "2026-10-13T00:00:00Z", removedAt: null, rating: { average: null, count: 0 }, viewer: null, ...over,
});
const detail = (over: object = {}) => ({
  ...card("b1"), gymSlug: "crimp", photoPath: "p", name: "Ex Moon", points: 46, completions: 0, tags: [], setter: null, intensity: null,
  viewerRole: null, isFollowing: false, isProject: false, ...over,
});

function showBoulder(boulder: object) {
  reply("GET", "/api/boulders/b1", boulder);
  reply("GET", "/api/boulders/b1/beta", undefined);
  reply("GET", "/api/boulders/b1/grade-consensus", { viewerCanSuggest: false, systems: [] });
  reply("GET", "/api/boulders/b1/comments", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
  reply("GET", "/api/gyms/crimp", { id: "g1", slug: "crimp", name: "Crimp Factory" });
  reply("GET", "/api/gyms/g1/sectors", []);
  return renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);
}

beforeEach(() => { routes.clear(); calls.length = 0; localStorage.clear(); });

describe("Project on a boulder's page", () => {
  it("is a switch in Your progress, by hand only, next to Mark as completed", async () => {
    reply("PUT", "/api/boulders/b1/project", { isProject: true });
    showBoulder(detail({ viewer: { attempts: 4, completed: false, completedAt: null, rating: null } }));
    const progress = await screen.findByRole("region", { name: "Your progress" });
    const toggle = within(progress).getByRole("switch", { name: "Project" });
    expect(toggle).toHaveAttribute("aria-checked", "false");
    expect(within(progress).getByRole("button", { name: "Mark as completed" })).toBeInTheDocument();
    await userEvent.click(toggle);
    await waitFor(() => expect(calls).toContainEqual({ method: "PUT", path: "/api/boulders/b1/project" }));
  });

  it("can be turned off", async () => {
    reply("DELETE", "/api/boulders/b1/project", { isProject: false });
    showBoulder(detail({ isProject: true }));
    const toggle = await screen.findByRole("switch", { name: "Project" });
    expect(toggle).toHaveAttribute("aria-checked", "true");
    await userEvent.click(toggle);
    await waitFor(() => expect(calls).toContainEqual({ method: "DELETE", path: "/api/boulders/b1/project" }));
  });

  it("is gone once completed: the project is among the sends", async () => {
    showBoulder(detail({ isProject: true, viewer: { attempts: 3, completed: true, completedAt: "2026-10-20T00:00:00Z", rating: null } }));
    await screen.findByRole("region", { name: "Your progress" });
    expect(screen.queryByRole("switch", { name: "Project" })).not.toBeInTheDocument();
  });

  it("can't be started on a boulder off the wall, but one that was can be unmarked", async () => {
    const { unmount } = showBoulder(detail({ status: "REMOVED", removedAt: "2026-10-20T00:00:00Z" }));
    await screen.findByRole("region", { name: "Your progress" });
    expect(screen.queryByRole("switch", { name: "Project" })).not.toBeInTheDocument();
    unmount();
    showBoulder(detail({ status: "REMOVED", removedAt: "2026-10-20T00:00:00Z", isProject: true }));
    expect(await screen.findByRole("switch", { name: "Project" })).toHaveAttribute("aria-checked", "true");
  });

  it("is not offered by a server that doesn't know projects", async () => {
    const { isProject: _p, ...old } = detail();
    showBoulder(old);
    await screen.findByRole("region", { name: "Your progress" });
    expect(screen.queryByRole("switch", { name: "Project" })).not.toBeInTheDocument();
  });
});

describe("Projects on Home", () => {
  const home = (over: object = {}) => ({
    stats: { completed: 0, projects: 3, totalAttempts: 5, completedThisMonth: 0, markedProjects: 1 }, gyms: [], freshToTry: [], recentCompletions: [], updates: [],
    projects: [card("p1", { isProject: true }), card("t1"), card("t2")], markedProjects: [card("p1", { isProject: true, name: "Chosen one" })], unmarkedTries: 2, ...over,
  });

  it("have their own row; Keep trying keeps the rest; the invitation shows once and stays closed", async () => {
    reply("GET", "/api/users/me", { id: "u1", email: "a@x.it", displayName: "Anna", staffGyms: [], pendingInvitations: 0 });
    reply("GET", "/api/users/me/home", home());
    const { unmount } = renderAt("/", "/", <HomePage />);
    const marked = await screen.findByRole("region", { name: "Your projects" });
    expect(within(marked).getAllByRole("link")).toHaveLength(1);
    expect(within(marked).getByTitle("Your project")).toBeInTheDocument();
    const trying = screen.getByRole("region", { name: "Keep trying" });
    expect(within(trying).getAllByRole("link")).toHaveLength(2);

    const invite = screen.getByRole("region", { name: "Choose your projects" });
    expect(invite).toHaveTextContent("You have 2 boulders tried and not completed");
    expect(within(invite).getByRole("link", { name: "See what you tried" })).toHaveAttribute("href", "/activity?history=PROJECTS");
    await userEvent.click(within(invite).getByRole("button", { name: "Not now" }));
    expect(screen.queryByRole("region", { name: "Choose your projects" })).not.toBeInTheDocument();
    unmount();

    renderAt("/", "/", <HomePage />);
    await screen.findByRole("region", { name: "Your projects" });
    expect(screen.queryByRole("region", { name: "Choose your projects" })).not.toBeInTheDocument();
  });

  it("from an older server: no projects row, no invitation, Keep trying as before", async () => {
    reply("GET", "/api/users/me", { id: "u1", email: "a@x.it", displayName: "Anna", staffGyms: [], pendingInvitations: 0 });
    const { markedProjects: _m, unmarkedTries: _u, ...old } = home({ projects: [card("t1"), card("t2")] });
    reply("GET", "/api/users/me/home", old);
    renderAt("/", "/", <HomePage />);
    const trying = await screen.findByRole("region", { name: "Keep trying" });
    expect(within(trying).getAllByRole("link")).toHaveLength(2);
    expect(screen.queryByRole("region", { name: "Your projects" })).not.toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Choose your projects" })).not.toBeInTheDocument();
  });
});

describe("Your progress filter", () => {
  it("offers the chosen projects as Projects and what was tried as Tried", async () => {
    const onChange = vi.fn();
    renderAt("/", "/", <BoulderFiltersBar filters={{}} onChange={onChange} sectors={[]} systems={[]} />);
    const select = screen.getByRole("combobox", { name: "Your progress" });
    expect(within(select).getAllByRole("option").map((o) => [o.getAttribute("value"), o.textContent]))
      .toEqual([["", "All"], ["UNTRIED", "Not tried"], ["MARKED", "Projects"], ["PROJECTS", "Tried"], ["COMPLETED", "Completed"]]);
  });
});
