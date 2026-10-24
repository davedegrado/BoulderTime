import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { I18nProvider } from "@/i18n/i18n";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t", user: { app_metadata: { providers: ["email"] } } }, initializing: false, signOut: vi.fn() }) }));

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

import { ProfilePage } from "@/pages/ProfilePage";

const me = (over: object = {}) => ({
  id: "u1", email: "anna@example.com", displayName: "Anna", avatarUrl: null, isPlatformAdmin: false, createdAt: "2026-09-01T00:00:00Z",
  staffGyms: [], pendingInvitations: 0, language: "en", leaderboardOptOut: false, leaderboardExcluded: false,
  acceptedLegalVersion: "1", legalAcceptanceNeeded: false, ...over,
});

function show() {
  renderAt("/profile", "/profile", <I18nProvider initial="en"><ProfilePage /></I18nProvider>);
}

beforeEach(() => {
  routes.clear();
  calls.length = 0;
  reply("GET", "/api/users/u1/profile", { id: "u1", displayName: "Anna", staffDistinctions: [] });
  reply("GET", "/api/users/me/blocks", []);
  reply("GET", "/api/users/me/deletion", { requestedAt: null });
});

describe("Edit profile", () => {
  it("writes a bio, saved with the name", async () => {
    reply("GET", "/api/users/me", me({ bio: null }));
    reply("PATCH", "/api/users/me", (body?: unknown) => me(body as object));
    show();
    await userEvent.click(await screen.findByText("Edit profile"));
    const bio = screen.getByRole("textbox", { name: "Bio" });
    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
    await userEvent.type(bio, "Crimper. ");
    expect(screen.getByText("8 of 160 characters. Shown under your name on your profile.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(calls.find((c) => c.method === "PATCH")?.body).toEqual({ displayName: "Anna", bio: "Crimper." }));
  });

  it("offers no bio against an older server, and sends none", async () => {
    reply("GET", "/api/users/me", me());
    reply("PATCH", "/api/users/me", (body?: unknown) => me(body as object));
    show();
    await userEvent.click(await screen.findByText("Edit profile"));
    expect(screen.queryByRole("textbox", { name: "Bio" })).not.toBeInTheDocument();
    const name = screen.getByRole("textbox", { name: "Display name" });
    await userEvent.type(name, " B");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(calls.find((c) => c.method === "PATCH")?.body).toEqual({ displayName: "Anna B" }));
  });
});
