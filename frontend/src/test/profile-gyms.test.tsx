import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "@/components/Toast";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

const routes = new Map<string, () => unknown>();
vi.mock("@/lib/api", () => {
  const call = () => async (path: string) => {
    const handler = routes.get(path.split("?")[0]!);
    if (!handler) throw new Error(`No mock for ${path}`);
    return handler();
  };
  return { api: { get: call(), post: call(), put: call(), patch: call(), delete: call() } };
});

import { UserProfilePage } from "@/pages/UserProfilePage";

const DEFAULT = ["stats", "favoriteSector", "gyms", "weekly", "highestGrades", "history"].map((key) => ({ key, visible: key !== "stats" }));
const gym = (id: string, name: string, over: object = {}) =>
  ({ id, slug: id, name, city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", isFoundingGym: false, isEarlyPartner: false, ...over });
const font = (label: string) => ({ gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", gymName: "x", label, rank: 10, colorHex: null });
const colour = (label: string, hex: string) => ({ gradeSystemId: "col", systemName: "Colori", systemType: "COLOR", gymName: "x", label, rank: 3, colorHex: hex });
const followed = gym("volume", "Volume Lab");
const profile = (over: object = {}) => ({
  id: "u2", displayName: "Anna Verdi", avatarUrl: null, memberSince: "2026-09-01T00:00:00Z", isMe: false, isBlocked: false,
  stats: { completed: 3, projects: 1, completedThisMonth: 2, totalAttempts: 9 }, highestGrades: [], weekly: [],
  followedGyms: [followed], recentCompletions: [], staffDistinctions: [], favoriteSector: null,
  isPrivate: false, contentHidden: false, sections: DEFAULT, friendship: "NONE", climbedGyms: [], ...over,
});

function show(p: object, tab = "gyms") {
  routes.set("/api/users/u2/profile", () => p);
  routes.set("/api/users/u2/friends", () => ({ items: [], page: 1, pageSize: 30, total: 0, hasMore: false }));
  const router = createMemoryRouter([{ path: "/users/:id", element: <UserProfilePage /> }], { initialEntries: [`/users/u2${tab ? `?tab=${tab}` : ""}`] });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><ToastProvider><RouterProvider router={router} /></ToastProvider></QueryClientProvider>);
}

beforeEach(() => routes.clear());

describe("A person's gyms", () => {
  it("show where they completed boulders: town, distinct boulders, latest send, highest grade per scale, and open the gym", async () => {
    show(profile({ climbedGyms: [{ gym: gym("crimp", "Crimp Factory"), completed: 2, lastCompletedAt: "2026-10-20T10:00:00Z", highest: [font("6B"), colour("Blu", "#1e63d6")] }] }));
    const card = await screen.findByRole("link", { name: /Crimp Factory/ });
    expect(card).toHaveAttribute("href", "/gyms/crimp");
    expect(card).toHaveTextContent("Milano");
    expect(card).toHaveTextContent("2 boulders completed");
    expect(card).toHaveTextContent(/Last send .*2026/);
    // One grade per scale, side by side, each read with its scale's name: no equivalence between them.
    expect(card).toHaveTextContent("Highest grade: 6B (Fontainebleau), Blu (Colori)");
    // No logo: the gym's initials, as everywhere else.
    expect(card.querySelector(".gym-avatar--initials")).toHaveTextContent("CF");
  });

  it("come in the order the server gives, latest send first, one card each", async () => {
    show(profile({ climbedGyms: [
      { gym: gym("granite", "Granite House"), completed: 5, lastCompletedAt: "2026-10-22T10:00:00Z", highest: [] },
      { gym: gym("crimp", "Crimp Factory"), completed: 1, lastCompletedAt: "2026-10-01T10:00:00Z", highest: [font("6A")] },
    ] }));
    await screen.findByRole("link", { name: /Granite House/ });
    const names = [...document.querySelectorAll(".climbed-gym__name")].map((n) => n.textContent);
    expect(names).toEqual(["Granite House", "Crimp Factory"]);
    expect(screen.getByRole("link", { name: /Granite House/ })).toHaveTextContent("5 boulders completed");
    expect(screen.getByRole("link", { name: /Crimp Factory/ })).toHaveTextContent("1 boulder completed");
  });

  it("without a date for the latest send, says nothing rather than inventing one", async () => {
    show(profile({ climbedGyms: [{ gym: gym("crimp", "Crimp Factory"), completed: 1, lastCompletedAt: null, highest: [] }] }));
    const card = await screen.findByRole("link", { name: /Crimp Factory/ });
    expect(card).not.toHaveTextContent("Last send");
  });

  it("never fall back to the gyms only followed: an empty list says where they will appear", async () => {
    show(profile({ climbedGyms: [] }));
    expect(await screen.findByText("No gyms to show yet")).toBeInTheDocument();
    expect(screen.getByText("The gyms where Anna Verdi completes at least one boulder appear here.")).toBeInTheDocument();
    expect(screen.queryByText("Volume Lab")).not.toBeInTheDocument();
  });

  it("are never on the overview: only in their own tab", async () => {
    show(profile({ climbedGyms: [{ gym: gym("crimp", "Crimp Factory"), completed: 1, lastCompletedAt: null, highest: [] }] }), "");
    expect(await screen.findByRole("tab", { name: "Gyms" })).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Gyms" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Crimp Factory/ })).not.toBeInTheDocument();
  });

  it("from an older server, which doesn't say where they climbed, are the gyms followed, as before", async () => {
    const { climbedGyms: _c, ...old } = profile();
    show(old);
    expect(await screen.findByRole("link", { name: /Volume Lab/ })).toHaveAttribute("href", "/gyms/volume");
  });
});
