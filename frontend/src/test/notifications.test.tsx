import { Route } from "react-router-dom";
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

import { NotificationsPage } from "@/pages/NotificationsPage";
import { NotificationSettingsPage } from "@/pages/NotificationSettingsPage";
import { ManageLayout } from "@/pages/manage/ManageLayout";
import { ManageAnnouncements } from "@/pages/manage/ManageAnnouncements";

const note = (id: string, over: object = {}) => ({
  id, type: "SECTOR_RETRACED", category: "SECTOR_UPDATES", title: "Sector Cave has been retraced", body: "Crimp Factory removed 25 boulders.",
  link: "/gyms/crimp", relatedEntityType: "SECTOR", relatedEntityId: "s1", count: 1, isRead: false, createdAt: new Date().toISOString(), ...over,
});

beforeEach(() => { routes.clear(); calls.length = 0; });

describe("Notifications inbox", () => {
  it("opens a notification, marking it read and navigating to its link", async () => {
    reply("GET", "/api/notifications", { items: [note("n1"), note("n2", { isRead: true, type: "GYM_ANNOUNCEMENT", category: "GYM_UPDATES", title: "Old news", createdAt: "2026-01-01T00:00:00Z" })], page: 1, pageSize: 30, total: 2, hasMore: false });
    reply("POST", "/api/notifications/n1/read", {});
    renderAt("/notifications", "/notifications", <><NotificationsPage /></>);

    expect(await screen.findByRole("region", { name: "Today" })).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Earlier" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Mark all as read" })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /sector cave has been retraced/i }));
    expect(calls.some((c) => c.method === "POST" && c.path === "/api/notifications/n1/read")).toBe(true);
  });

  it("shows an empty state when there is nothing", async () => {
    reply("GET", "/api/notifications", { items: [], page: 1, pageSize: 30, total: 0, hasMore: false });
    renderAt("/notifications", "/notifications", <NotificationsPage />);
    expect(await screen.findByText("You're all caught up")).toBeInTheDocument();
  });
});

describe("Notification settings", () => {
  it("switches a category off and mutes a single follow without unfollowing", async () => {
    reply("GET", "/api/users/me/notification-settings", { gymUpdates: true, sectorUpdates: true, boulderUpdates: true, myContent: true });
    reply("PUT", "/api/users/me/notification-settings", { gymUpdates: false, sectorUpdates: true, boulderUpdates: true, myContent: true });
    reply("GET", "/api/users/me/follows", { gyms: [{ gym: { id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null }, isFavorite: true, notificationsEnabled: true }], sectors: [], boulders: [] });
    reply("PUT", "/api/gyms/g1/follow", {});
    renderAt("/notifications/settings", "/notifications/settings", <NotificationSettingsPage />);

    await userEvent.click(await screen.findByRole("switch", { name: "Gym updates" }));
    await waitFor(() => expect(calls.find((c) => c.path === "/api/users/me/notification-settings" && c.method === "PUT")?.body).toEqual({ gymUpdates: false }));

    await userEvent.click(await screen.findByRole("switch", { name: "Notifications for Crimp Factory" }));
    await waitFor(() => expect(calls.find((c) => c.path === "/api/gyms/g1/follow")?.body).toEqual({ notificationsEnabled: false }));
    expect(calls.some((c) => c.method === "DELETE")).toBe(false);
  });
});

describe("Staff updates", () => {
  it("requires a date for events and publishes with follower notification by default", async () => {
    reply("GET", "/api/gyms/crimp", { id: "g1", slug: "crimp", name: "Crimp", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: "STAFF", follow: null, followerCount: 0 });
    reply("GET", "/api/gyms/g1/announcements", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
    reply("GET", "/api/gyms/g1/sectors", [{ id: "s1", gymId: "g1", name: "Cave", description: null, imageUrl: null, sortOrder: 0, isActive: true, isFollowing: false }]);
    reply("POST", "/api/gyms/g1/announcements", (body: unknown) => ({ id: "a1", notifiedFollowers: (body as { notifyFollowers: boolean }).notifyFollowers }));
    renderAt("/manage/crimp/announcements?new=1&sectorId=s1&title=Cave%20has%20been%20retraced", "/manage/:slug", <ManageLayout />, <Route path="announcements" element={<ManageAnnouncements />} />);

    const form = await screen.findByRole("heading", { name: "New update" });
    const card = form.closest("form")!;
    expect(within(card).getByRole("textbox", { name: "Title" })).toHaveValue("Cave has been retraced");
    await waitFor(() => expect(within(card).getByRole("combobox", { name: "Sector" })).toHaveValue("s1"));
    expect(within(card).queryByLabelText("Date and time")).not.toBeInTheDocument();

    await userEvent.selectOptions(within(card).getByRole("combobox", { name: "Type" }), "EVENT");
    expect(within(card).getByLabelText("Date and time")).toBeInTheDocument();
    expect(within(card).getByRole("checkbox", { name: /notify followers/i })).toBeChecked();

    await userEvent.selectOptions(within(card).getByRole("combobox", { name: "Type" }), "ANNOUNCEMENT");
    await userEvent.type(within(card).getByRole("textbox", { name: "Message" }), "Fresh problems on Friday.");
    await userEvent.click(within(card).getByRole("button", { name: "Publish" }));
    await waitFor(() => expect(calls.find((c) => c.method === "POST")?.body).toMatchObject({ type: "ANNOUNCEMENT", sectorId: "s1", notifyFollowers: true, content: "Fresh problems on Friday." }));
  });
});

describe("New boulder notifications", () => {
  it("render with their own icon and open the gym when collapsed", async () => {
    reply("GET", "/api/notifications", { items: [
      note("n9", { type: "NEW_BOULDERS_IN_SECTOR", category: "SECTOR_UPDATES", title: "5 new boulders in Cave", body: "Crimp Factory · latest: 6A · Blue holds", link: "/gyms/crimp", count: 5 }),
    ], page: 1, pageSize: 30, total: 1, hasMore: false });
    reply("POST", "/api/notifications/n9/read", {});
    renderAt("/notifications", "/notifications", <NotificationsPage />);

    const item = await screen.findByRole("button", { name: /5 new boulders in cave/i });
    expect(within(item).getByText("Crimp Factory · latest: 6A · Blue holds")).toBeInTheDocument();
    await userEvent.click(item);
    expect(calls.some((c) => c.path === "/api/notifications/n9/read")).toBe(true);
  });
});
