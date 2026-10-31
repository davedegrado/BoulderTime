import { act, screen } from "@testing-library/react";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = () => unknown;
const routes = new Map<string, Handler>();
const reply = (path: string, value: unknown) => routes.set(path, () => value);
vi.mock("@/lib/api", () => {
  const get = async (path: string) => {
    const handler = routes.get(path.split("?")[0]!);
    if (!handler) throw new Error(`No mock for GET ${path}`);
    return handler();
  };
  return { api: { get, post: get, put: get, patch: get, delete: get, blob: get } };
});

import { BoulderPage } from "@/pages/BoulderPage";
import { setClimberView } from "@/lib/climberView";

const font = { gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", gradeValueId: "v6a", label: "6A", rank: 5, colorHex: null };
const boulder = { id: "b1", gymId: "g1", gymName: "Crimp Factory", gymSlug: "crimp", rating: { average: null, count: 0 }, viewer: null, sectorId: "s1", sectorName: "Cave",
  photoUrl: "/b1.jpg", photoPath: "p", holdColor: "BLUE", grades: [font], status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null, setter: null,
  viewerRole: "OWNER", isFollowing: false };
const me = (isPlatformAdmin: boolean) => ({ id: "u1", displayName: "Dave", email: "d@x.it", avatarUrl: null, isPlatformAdmin, staffGyms: [], language: "en" });

beforeEach(() => {
  routes.clear();
  localStorage.clear();
  reply("/api/boulders/b1", boulder);
  reply("/api/boulders/b1/beta", undefined);
  reply("/api/boulders/b1/grade-consensus", { viewerCanSuggest: false, systems: [] });
  reply("/api/boulders/b1/comments", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
});

describe("Seeing gyms as a climber", () => {
  it("leaves the staff's edit button out for a BoulderTime admin who turned it on, and brings it back when off", async () => {
    reply("/api/users/me", me(true));
    renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);
    expect(await screen.findByRole("link", { name: "Edit" })).toBeInTheDocument();
    act(() => setClimberView(true));
    expect(screen.queryByRole("link", { name: "Edit" })).not.toBeInTheDocument();
    act(() => setClimberView(false));
    expect(screen.getByRole("link", { name: "Edit" })).toBeInTheDocument();
  });

  it("changes nothing for staff who are not BoulderTime admins, even with the switch left on", async () => {
    setClimberView(true);
    reply("/api/users/me", me(false));
    renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);
    expect(await screen.findByRole("link", { name: "Edit" })).toBeInTheDocument();
  });
});
