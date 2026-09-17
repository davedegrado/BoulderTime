import { Route } from "react-router-dom";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

// ---- Mocks: signed-in session and an in-memory API keyed by "METHOD path" ----
vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string; body?: unknown }[] = [];
function reply(method: string, path: string, value: unknown | Handler) {
  routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
}
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
import { ExplorePage } from "@/pages/ExplorePage";
import { HomePage } from "@/pages/HomePage";
import { ManageLayout } from "@/pages/manage/ManageLayout";
import { ManageStaff } from "@/pages/manage/ManageStaff";
import { AdminLayout } from "@/pages/admin/AdminLayout";
import { AdminCandidates } from "@/pages/admin/AdminCandidates";

const gym = {
  id: "g1", slug: "crimp-factory", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE",
  description: "Two floors of bouldering.", address: "Via Tortona 31", website: "https://crimp.example", email: "info@crimp.example",
  phone: "+39 02 1234", createdAt: "2026-01-01T00:00:00Z", viewerRole: null,
};
const me = (over: object = {}) => ({
  id: "u1", email: "me@example.com", displayName: "Dave Climber", avatarUrl: null, isPlatformAdmin: false,
  createdAt: "2026-01-01T00:00:00Z", staffGyms: [], pendingInvitations: 0, ...over,
});

beforeEach(() => { routes.clear(); calls.length = 0; });

describe("Gym page", () => {
  it("shows active sectors and contact info to a climber, without a Manage button", async () => {
    reply("GET", "/api/gyms/crimp-factory", gym);
    reply("GET", "/api/gyms/g1/sectors", [
      { id: "s1", gymId: "g1", name: "Cave", description: null, imageUrl: null, sortOrder: 0, isActive: true },
      { id: "s2", gymId: "g1", name: "Slab", description: "Technical", imageUrl: null, sortOrder: 1, isActive: true },
    ]);
    reply("GET", "/api/gyms/g1/grade-systems", []);
    reply("GET", "/api/gyms/g1/boulders", { items: [], page: 1, pageSize: 24, total: 0, hasMore: false });
    renderAt("/gyms/crimp-factory", "/gyms/:slug", <GymPage />);

    expect(await screen.findByRole("heading", { name: "Crimp Factory" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("tab", { name: "Sectors" }));
    expect(await screen.findByText("Technical")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /manage/i })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("tab", { name: "Info" }));
    expect(screen.getByRole("link", { name: "info@crimp.example" })).toHaveAttribute("href", "mailto:info@crimp.example");
  });

  it("shows Manage and a draft notice to staff", async () => {
    reply("GET", "/api/gyms/crimp-factory", { ...gym, status: "DRAFT", viewerRole: "STAFF" });
    reply("GET", "/api/gyms/g1/sectors", []);
    reply("GET", "/api/gyms/g1/grade-systems", []);
    reply("GET", "/api/gyms/g1/boulders", { items: [], page: 1, pageSize: 24, total: 0, hasMore: false });
    renderAt("/gyms/crimp-factory", "/gyms/:slug", <GymPage />);

    expect(await screen.findByRole("link", { name: /manage/i })).toHaveAttribute("href", "/manage/crimp-factory");
    expect(screen.getByText(/only staff can see this/i)).toBeInTheDocument();
    expect(await screen.findByText("No boulders on the wall yet")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("tab", { name: "Sectors" }));
    expect(await screen.findByText("No sectors yet")).toBeInTheDocument();
  });
});

describe("Explore", () => {
  it("lists gyms and offers to suggest one when nothing matches", async () => {
    reply("GET", "/api/gyms", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
    renderAt("/explore?q=atlantis", "/explore", <ExplorePage />);
    expect(await screen.findByText(/No gyms match "atlantis"/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /suggest a gym/i })).toHaveAttribute("href", "/gyms/suggest");
  });
});

describe("Home", () => {
  it("shows a pending invitation and accepts it", async () => {
    reply("GET", "/api/users/me", me({ pendingInvitations: 1 }));
    reply("GET", "/api/users/me/home", { stats: { completed: 0, projects: 0, totalAttempts: 0, completedThisMonth: 0 }, gyms: [], projects: [], freshToTry: [], recentCompletions: [], updates: [] });
    reply("GET", "/api/users/me/invitations", [
      { id: "i1", gymId: "g1", gymSlug: "crimp-factory", gymName: "Crimp Factory", gymCity: "Milano", role: "ADMIN", invitedBy: "Marco", expiresAt: new Date(Date.now() + 3 * 86400000).toISOString() },
    ]);
    reply("POST", "/api/invitations/i1/accept", { gymId: "g1", slug: "crimp-factory", name: "Crimp Factory", city: "Milano", logoUrl: null, role: "ADMIN" });
    renderAt("/", "/", <HomePage />);

    const card = await screen.findByRole("region", { name: /you've been invited/i });
    expect(within(card).getByText(/as/)).toHaveTextContent("Marco invited you to join Crimp Factory (Milano) as Admin.");
    await userEvent.click(within(card).getByRole("button", { name: "Accept" }));
    expect(calls.some((c) => c.method === "POST" && c.path === "/api/invitations/i1/accept")).toBe(true);
  });
});

describe("Staff management", () => {
  const staff = [
    { userId: "o1", displayName: "Olga Owner", email: "olga@x.com", avatarUrl: null, role: "OWNER", since: "2026-01-01T00:00:00Z" },
    { userId: "u1", displayName: "Dave Climber", email: "me@example.com", avatarUrl: null, role: "ADMIN", since: "2026-01-01T00:00:00Z" },
    { userId: "s1", displayName: "Sam Setter", email: "sam@x.com", avatarUrl: null, role: "STAFF", since: "2026-01-01T00:00:00Z" },
  ];

  function setup(viewerRole: string) {
    reply("GET", "/api/gyms/crimp-factory", { ...gym, viewerRole });
    reply("GET", "/api/users/me", me());
    reply("GET", "/api/gyms/g1/staff", staff);
    reply("GET", "/api/gyms/g1/invitations", []);
    renderAt("/manage/crimp-factory/staff", "/manage/:slug", <ManageLayout />, <Route path="staff" element={<ManageStaff />} />);
  }

  it("lets an admin invite staff/admins but not owners, and not touch the owner", async () => {
    setup("ADMIN");
    const roleSelect = await screen.findByRole("combobox", { name: "Role" });
    expect(within(roleSelect).getAllByRole("option").map((o) => o.textContent)).toEqual(["Staff", "Admin"]);

    const ownerRow = (await screen.findByText("Olga Owner")).closest("li")!;
    expect(within(ownerRow).queryByRole("button", { name: /remove/i })).not.toBeInTheDocument();
    expect(within(ownerRow).getByText("Owner")).toBeInTheDocument();

    const samRow = screen.getByText("Sam Setter").closest("li")!;
    expect(within(samRow).getByRole("button", { name: /remove/i })).toBeInTheDocument();

    const myRow = screen.getByText("Dave Climber").closest("li")!;
    expect(within(myRow).getByRole("button", { name: /leave/i })).toBeInTheDocument();
  });

  it("requires two taps to remove someone", async () => {
    setup("OWNER");
    reply("DELETE", "/api/gyms/g1/staff/s1", undefined);
    const samRow = (await screen.findByText("Sam Setter")).closest("li")!;
    await userEvent.click(within(samRow).getByRole("button", { name: /remove/i }));
    expect(calls.some((c) => c.method === "DELETE")).toBe(false);
    await userEvent.click(within(samRow).getByRole("button", { name: /tap to remove/i }));
    expect(calls.some((c) => c.method === "DELETE" && c.path === "/api/gyms/g1/staff/s1")).toBe(true);
  });

  it("hides the staff area from people who aren't staff", async () => {
    reply("GET", "/api/gyms/crimp-factory", { ...gym, viewerRole: null });
    renderAt("/manage/crimp-factory", "/manage/:slug", <ManageLayout />);
    expect(await screen.findByText("This area is for gym staff")).toBeInTheDocument();
  });
});

describe("Admin", () => {
  it("is closed to non-admins", async () => {
    reply("GET", "/api/users/me", me());
    renderAt("/admin", "/admin", <AdminLayout />);
    expect(await screen.findByText("BoulderTime administrators only")).toBeInTheDocument();
  });

  it("turns a suggestion into a prefilled gym form", async () => {
    reply("GET", "/api/users/me", me({ isPlatformAdmin: true }));
    reply("GET", "/api/admin/gym-candidates", {
      items: [{ id: "c1", gymName: "Boulder Café", city: "Genova", officialEmail: "info@cafe.example", website: "https://cafe.example", notes: "Great gym", status: "PENDING", createdAt: "2026-09-01T00:00:00Z", handledAt: null, gymId: null, submittedBy: "Anna", submittedByEmail: "anna@x.com" }],
      page: 1, pageSize: 50, total: 1, hasMore: false,
    });
    reply("POST", "/api/admin/gyms", (body: unknown) => ({ ...gym, id: "g9", slug: "boulder-cafe", name: (body as { name: string }).name, status: "DRAFT" }));
    renderAt("/admin/candidates", "/admin", <AdminLayout />, <Route path="candidates" element={<AdminCandidates />} />);

    await userEvent.click(await screen.findByRole("button", { name: "Create gym" }));
    expect(screen.getByRole("textbox", { name: "Name" })).toHaveValue("Boulder Café");
    expect(screen.getByRole("textbox", { name: "City" })).toHaveValue("Genova");
    await userEvent.click(screen.getByRole("button", { name: /create draft gym/i }));
    const post = calls.find((c) => c.method === "POST" && c.path === "/api/admin/gyms");
    expect(post?.body).toMatchObject({ name: "Boulder Café", city: "Genova", candidateId: "c1" });
  });
});
