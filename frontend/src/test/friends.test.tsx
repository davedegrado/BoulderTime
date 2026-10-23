import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
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

import { UserProfilePage } from "@/pages/UserProfilePage";
import { NotificationsPage } from "@/pages/NotificationsPage";

const DEFAULT = ["stats", "favoriteSector", "gyms", "weekly", "highestGrades", "history"].map((key) => ({ key, visible: key !== "stats" }));
/** The server's side of the friendship: what the profile says, changed by the calls as the real one would. */
let friendship: string | null = "NONE";
const profile = (over: object = {}) => ({
  id: "u2", displayName: "Anna Verdi", avatarUrl: null, memberSince: "2026-09-01T00:00:00Z", isMe: false, isBlocked: false,
  stats: { completed: 3, projects: 1, completedThisMonth: 2, totalAttempts: 9 }, highestGrades: [], weekly: [], followedGyms: [],
  recentCompletions: [], staffDistinctions: [], favoriteSector: null, isPrivate: false, contentHidden: false, sections: DEFAULT,
  friendship, ...over,
});
const answer = (state: string) => () => { friendship = state; return { state }; };

function show(element = <UserProfilePage />, at = "/users/u2") {
  const router = createMemoryRouter([{ path: "/users/:id", element }, { path: "/notifications", element }, { path: "/", element: <h1>Home</h1> }], { initialEntries: [at] });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);
  return router;
}
/** The friendship as the profile shows it (the toasts are statuses too). */
const friendState = () => waitFor(() => { const el = document.querySelector(".friend-state"); expect(el).not.toBeNull(); return el!; });
const posts = (path: string) => calls.filter((c) => c.method === "POST" && c.path === path).length;

beforeEach(() => {
  routes.clear();
  calls.length = 0;
  friendship = "NONE";
  reply("GET", "/api/users/u2/profile", () => profile());
  reply("GET", "/api/users/u2/history", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
  reply("GET", "/api/users/u2/friends", { items: [], page: 1, pageSize: 30, total: 0, hasMore: false });
  reply("GET", "/api/users/me/friend-requests", []);
});

describe("Add friend", () => {
  it("sends one request however fast it's tapped, then says it's sent and offers to cancel it", async () => {
    let release: () => void = () => {};
    reply("POST", "/api/users/u2/friend-request", () => new Promise((resolve) => { release = () => resolve(answer("OUTGOING")()); }));
    reply("DELETE", "/api/users/u2/friendship", answer("NONE"));
    show();
    const add = await screen.findByRole("button", { name: "Add friend" });
    await userEvent.click(add);
    await userEvent.click(add);
    expect(posts("/api/users/u2/friend-request")).toBe(1);
    expect(add).toBeDisabled();
    release();

    expect(await friendState()).toHaveTextContent("Request sent");
    await userEvent.click(screen.getByRole("button", { name: "Cancel request" }));
    expect(await screen.findByRole("button", { name: "Add friend" })).toBeInTheDocument();
    expect(calls.some((c) => c.method === "DELETE" && c.path === "/api/users/u2/friendship")).toBe(true);
  });

  it("offers to accept or decline a request received, and says when you're friends; ending it is in the menu", async () => {
    friendship = "INCOMING";
    reply("POST", "/api/users/u2/friend-request/accept", answer("FRIENDS"));
    reply("DELETE", "/api/users/u2/friendship", answer("NONE"));
    show();
    expect(await screen.findByRole("button", { name: "Decline" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Accept request" }));
    expect(await friendState()).toHaveTextContent("Friends");

    await userEvent.click(screen.getByRole("button", { name: "More actions" }));
    await userEvent.click(within(screen.getByRole("menu")).getByRole("menuitem", { name: "Remove from friends" }));
    expect(await screen.findByRole("button", { name: "Add friend" })).toBeInTheDocument();
  });

  it("says what went wrong and stays as it was when the request fails", async () => {
    reply("POST", "/api/users/u2/friend-request", () => { throw new Error("offline"); });
    show();
    await userEvent.click(await screen.findByRole("button", { name: "Add friend" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Add friend" })).toBeEnabled());
    expect(screen.queryByText("Request sent")).not.toBeInTheDocument();
  });

  it("is not on your own profile", async () => {
    reply("GET", "/api/users/u2/profile", () => profile({ isMe: true, friendship: null }));
    show();
    expect(await screen.findByRole("heading", { name: "Anna Verdi" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add friend" })).not.toBeInTheDocument();
  });

  it("is not there for someone you blocked", async () => {
    reply("GET", "/api/users/u2/profile", () => profile({ isBlocked: true }));
    show();
    expect(await screen.findByRole("heading", { name: "Anna Verdi" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add friend" })).not.toBeInTheDocument();
  });

  it("is not offered by an older server, which has no Friends tab either", async () => {
    const { friendship: _f, ...old } = profile();
    reply("GET", "/api/users/u2/profile", () => old);
    show();
    expect(await screen.findByRole("tab", { name: "Overview" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Add friend" })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Friends" })).not.toBeInTheDocument();
  });

  it("is there on a private profile too, which shows nothing else", async () => {
    reply("GET", "/api/users/u2/profile", () => profile({ isPrivate: true, contentHidden: true }));
    show();
    expect(await screen.findByText("This profile is private")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Add friend" })).toBeInTheDocument();
  });
});

describe("The Friends tab", () => {
  it("shows your requests first, to accept there, then your friends", async () => {
    friendship = null;
    reply("GET", "/api/users/u2/profile", () => profile({ isMe: true, friendship: null }));
    let requests = [{ person: { userId: "u3", displayName: "Luca Rossi", avatarUrl: null }, requestedAt: "2026-10-22T10:00:00Z" }];
    reply("GET", "/api/users/me/friend-requests", () => requests);
    let friends: unknown[] = [];
    reply("GET", "/api/users/u2/friends", () => ({ items: friends, page: 1, pageSize: 30, total: friends.length, hasMore: false }));
    reply("POST", "/api/users/u3/friend-request/accept", () => {
      requests = [];
      friends = [{ person: { userId: "u3", displayName: "Luca Rossi", avatarUrl: null }, since: "2026-10-23T10:00:00Z" }];
      return { state: "FRIENDS" };
    });
    show(<UserProfilePage />, "/users/u2?tab=friends");

    const asked = await screen.findByRole("region", { name: "Friend requests" });
    expect(within(asked).getByRole("link", { name: /Luca Rossi/ })).toHaveAttribute("href", "/users/u3");
    expect(screen.getByText("No friends yet")).toBeInTheDocument();
    await userEvent.click(within(asked).getByRole("button", { name: "Accept" }));

    const list = await screen.findByRole("region", { name: "Friends" });
    expect(within(list).getByRole("link", { name: /Luca Rossi/ })).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Friend requests" })).not.toBeInTheDocument();
  });
});

describe("Notifications", () => {
  it("lead to the friend requests when someone asked", async () => {
    reply("GET", "/api/notifications", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
    reply("GET", "/api/users/me", { id: "u2", email: "a@example.com", displayName: "Anna Verdi" });
    reply("GET", "/api/users/me/friend-requests", [
      { person: { userId: "u3", displayName: "Luca Rossi", avatarUrl: null }, requestedAt: "2026-10-22T10:00:00Z" },
      { person: { userId: "u4", displayName: "Sara Conti", avatarUrl: null }, requestedAt: "2026-10-22T11:00:00Z" },
    ]);
    show(<NotificationsPage />, "/notifications");
    expect(await screen.findByRole("link", { name: /2 friend requests/ })).toHaveAttribute("href", "/users/u2?tab=friends");
  });

  it("stay as they were against a server without friends", async () => {
    reply("GET", "/api/notifications", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
    reply("GET", "/api/users/me", { id: "u2", email: "a@example.com", displayName: "Anna Verdi" });
    reply("GET", "/api/users/me/friend-requests", () => { throw new Error("404"); });
    show(<NotificationsPage />, "/notifications");
    expect(await screen.findByText("You're all caught up")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /friend request/ })).not.toBeInTheDocument();
  });
});
