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

import { AdminPartners } from "@/pages/admin/AdminPartners";

const adminGym = { id: "g1", slug: "rock-n-fire", name: "Rock n Fire", city: "Modena", status: "ACTIVE", staffCount: 1, ownerCount: 1, pendingInvitations: 0, createdAt: "2026-09-01T00:00:00Z" };

beforeEach(() => {
  routes.clear();
  calls.length = 0;
  reply("GET", "/api/admin/gyms", { items: [adminGym], page: 1, pageSize: 20, total: 1, hasMore: false });
});

describe("Admin partners", () => {
  it("picks the founding gym by searching for it — no identifiers to copy", async () => {
    reply("GET", "/api/admin/partners", []);
    reply("PUT", "/api/admin/gyms/g1/founding", {});
    renderAt("/admin/partners", "/admin/partners", <AdminPartners />);

    expect(await screen.findByText("No founding gym yet.")).toBeInTheDocument();
    expect(screen.queryByLabelText(/gym id/i)).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Choose the founding gym" }));
    const results = await screen.findAllByRole("listitem");
    const row = results.find((r) => within(r).queryByText("Rock n Fire"))!;
    await userEvent.click(within(row).getByRole("button", { name: "Set as founding gym" }));

    await waitFor(() => expect(calls.find((c) => c.method === "PUT")).toEqual({
      method: "PUT", path: "/api/admin/gyms/g1/founding", body: { isFoundingGym: true },
    }));
  });

  it("adds an early partner with an optional note, and ends a running partnership", async () => {
    reply("GET", "/api/admin/partners", [
      { gym: { ...adminGym, logoUrl: null, coverImageUrl: null, latitude: null, longitude: null, isFoundingGym: false, isEarlyPartner: true },
        earlyPartner: { id: "p1", gymId: "g1", startedAt: "2026-09-01T00:00:00Z", endedAt: null, note: "Launch partner", isActive: true } },
    ]);
    reply("POST", "/api/admin/gyms/g1/early-partner", {});
    reply("DELETE", "/api/admin/gyms/g1/early-partner", {});
    renderAt("/admin/partners", "/admin/partners", <AdminPartners />);

    expect(await screen.findByText(/Partner since/)).toBeInTheDocument();
    expect(screen.getByText(/Launch partner/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Add early partner" }));
    await userEvent.type(screen.getByRole("textbox", { name: "Note" }), "Pilota");
    const row = (await screen.findAllByRole("listitem")).find((r) => within(r).queryByRole("button", { name: "Add as early partner" }))!;
    await userEvent.click(within(row).getByRole("button", { name: "Add as early partner" }));
    await waitFor(() => expect(calls.find((c) => c.method === "POST")?.body).toEqual({ note: "Pilota" }));

    await userEvent.click(screen.getByRole("button", { name: "End partnership" }));
    await userEvent.click(screen.getByRole("button", { name: "End?" }));
    await waitFor(() => expect(calls.some((c) => c.method === "DELETE" && c.path === "/api/admin/gyms/g1/early-partner")).toBe(true));
  });
});
