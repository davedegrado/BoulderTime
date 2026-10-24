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

import { FriendsPage } from "@/pages/FriendsPage";

const person = (userId: string, displayName: string) => ({ userId, displayName, avatarUrl: null });
const page = (items: unknown[]) => ({ items, page: 1, pageSize: 30, total: items.length, hasMore: false });

beforeEach(() => {
  routes.clear();
  calls.length = 0;
  reply("GET", "/api/users/me", { id: "me", email: "me@example.com", displayName: "Marco" });
  reply("GET", "/api/users/me/friend-requests", [{ person: person("u3", "Luca Rossi"), requestedAt: "2026-10-22T10:00:00Z" }]);
  reply("GET", "/api/users/me/friend-requests/sent", [{ person: person("u4", "Sara Conti"), requestedAt: "2026-10-22T11:00:00Z" }]);
  reply("GET", "/api/users/me/friends", page([{ person: person("u5", "Paolo Neri"), since: "2026-10-01T00:00:00Z" }]));
});

const show = () => renderAt("/friends", "/friends", <FriendsPage />);

describe("Friends page", () => {
  it("puts the requests received and sent on top, then the friends", async () => {
    show();
    const received = await screen.findByRole("region", { name: "Requests received" });
    expect(within(received).getByText("Luca Rossi")).toBeInTheDocument();
    expect(within(received).getByRole("button", { name: "Accept" })).toBeInTheDocument();
    const sent = await screen.findByRole("region", { name: "Requests sent" });
    expect(within(sent).getByText("Sara Conti")).toBeInTheDocument();
    const friends = await screen.findByRole("region", { name: "Friends" });
    expect(within(friends).getByRole("link", { name: /Paolo Neri/ })).toHaveAttribute("href", "/users/u5");
    // In this order.
    expect(received.compareDocumentPosition(sent) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(sent.compareDocumentPosition(friends) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("cancels a request sent", async () => {
    reply("DELETE", "/api/users/u4/friendship", { state: "NONE" });
    show();
    const sent = await screen.findByRole("region", { name: "Requests sent" });
    await userEvent.click(within(sent).getByRole("button", { name: "Cancel the request to Sara Conti" }));
    await waitFor(() => expect(calls.some((c) => c.method === "DELETE" && c.path === "/api/users/u4/friendship")).toBe(true));
  });

  it("finds climbers by name, with the right button for each, and asks only from two letters on", async () => {
    reply("GET", "/api/users/search", [
      { person: person("u6", "Anna Bianchi"), friendship: "NONE" },
      { person: person("u4", "Sara Conti"), friendship: "OUTGOING" },
      { person: person("u5", "Paolo Neri"), friendship: "FRIENDS" },
    ]);
    reply("POST", "/api/users/u6/friend-request", { state: "OUTGOING" });
    show();
    const search = await screen.findByRole("searchbox", { name: "Search climbers" });
    await userEvent.type(search, "a");
    expect(screen.getByText("Type at least two letters of a name.")).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Requests received" })).not.toBeInTheDocument();

    await userEvent.type(search, "n");
    const results = await screen.findByRole("region", { name: "Search results" });
    expect(calls.filter((c) => c.path.startsWith("/api/users/search"))).toEqual([{ method: "GET", path: "/api/users/search?q=an" }]);
    const rows = within(results).getAllByRole("listitem");
    expect(within(rows[0]!).getByRole("button", { name: "Add" })).toBeInTheDocument();
    expect(within(rows[1]!).getByRole("button", { name: "Cancel request" })).toBeInTheDocument();
    expect(within(rows[2]!).getByText("Friends")).toBeInTheDocument();
    await userEvent.click(within(rows[0]!).getByRole("button", { name: "Add" }));
    await waitFor(() => expect(calls.some((c) => c.method === "POST" && c.path === "/api/users/u6/friend-request")).toBe(true));

    // Clearing the search brings the requests back.
    await userEvent.click(screen.getByRole("button", { name: "Clear search" }));
    expect(await screen.findByRole("region", { name: "Requests received" })).toBeInTheDocument();
  });

  it("says so when nobody has that name", async () => {
    reply("GET", "/api/users/search", []);
    show();
    await userEvent.type(await screen.findByRole("searchbox", { name: "Search climbers" }), "zzz");
    expect(await screen.findByText("Nobody called “zzz”")).toBeInTheDocument();
  });
});
