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
  it("puts the requests in one box on top, the ones sent folded into a line, then the friends", async () => {
    show();
    const requests = await screen.findByRole("region", { name: "Requests received" });
    expect(within(requests).getByText("Luca Rossi")).toBeInTheDocument();
    expect(within(requests).getByRole("button", { name: "Accept" })).toBeInTheDocument();
    // The ones sent: a line that opens them.
    expect(within(requests).queryByText("Sara Conti")).not.toBeInTheDocument();
    const fold = within(requests).getByRole("button", { name: "1 request sent" });
    expect(fold).toHaveAttribute("aria-expanded", "false");
    await userEvent.click(fold);
    expect(within(requests).getByText("Sara Conti")).toBeInTheDocument();
    const friends = await screen.findByRole("region", { name: /^Your friends/ });
    expect(within(friends).getByRole("link", { name: /Paolo Neri/ })).toHaveAttribute("href", "/users/u5");
    expect(requests.compareDocumentPosition(friends) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("cancels a request sent", async () => {
    reply("DELETE", "/api/users/u4/friendship", { state: "NONE" });
    show();
    const requests = await screen.findByRole("region", { name: "Requests received" });
    await userEvent.click(within(requests).getByRole("button", { name: "1 request sent" }));
    await userEvent.click(within(requests).getByRole("button", { name: "Cancel the request to Sara Conti" }));
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

  it("shows what each friend climbed lately, the most active first, and how many friends a request has in common", async () => {
    localStorage.clear();
    const green = { gradeSystemId: "c", systemName: "Colori", systemType: "COLOR", gradeValueId: "g", label: "Green", rank: 3, colorHex: "#2E9E4F" };
    const today = new Date().toISOString();
    reply("GET", "/api/users/me/friend-requests", [{ person: person("u3", "Luca Rossi"), requestedAt: "2026-10-22T10:00:00Z", mutualFriends: 3 }]);
    reply("GET", "/api/users/me/friends", page([
      { person: person("u7", "Marta Rossi"), since: "2026-10-01T00:00:00Z",
        activity: { lastSendAt: today, boulderId: "b1", gymName: "Rock’n Fire", grade: green, holdColor: "BLUE", sentThisMonth: 3 } },
      { person: person("u5", "Paolo Neri"), since: "2026-10-01T00:00:00Z", activity: null },
      { person: person("u8", "Gino Verdi"), since: "2026-10-01T00:00:00Z" }, // an older server: the name alone
    ]));
    show();

    expect(within(await screen.findByRole("region", { name: "Requests received" })).getByText("3 friends in common")).toBeInTheDocument();
    const friends = await screen.findByRole("region", { name: /^Your friends/ });
    // Asked by activity unless the person chose A–Z.
    await waitFor(() => expect(calls.some((c) => c.path.startsWith("/api/users/me/friends") && c.path.includes("sort=activity"))).toBe(true));
    expect(within(friends).getByRole("radio", { name: "Activity" })).toHaveAttribute("aria-checked", "true");
    const marta = within(friends).getByRole("link", { name: /Marta Rossi/ });
    expect(marta).toHaveTextContent("Green");
    expect(marta).toHaveTextContent("at Rock’n Fire · today");
    expect(marta).toHaveTextContent("3 boulders this month");
    expect(within(friends).getByRole("link", { name: /Paolo Neri/ })).toHaveTextContent("No recent activity to show");
    expect(within(friends).getByRole("link", { name: /Gino Verdi/ }).querySelector(".friend-row__last, .friend-row__month")).toBeNull();

    await userEvent.click(within(friends).getByRole("radio", { name: "A–Z" }));
    await waitFor(() => expect(calls.at(-1)!.path).toBe("/api/users/me/friends?page=1"));
    expect(localStorage.getItem("bt:friends-sort")).toBe("name");
  });
});
