import { Route } from "react-router-dom";
import { render, renderHook, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactNode } from "react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
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

import { BoulderCard } from "@/features/boulders/BoulderCard";
import { CompletionsLine } from "@/features/climbing/Completions";
import { FiltersPanel, activeFilters } from "@/features/boulders/BoulderFilters";
import { FavoriteSectorCard } from "@/features/climbing/FavoriteSector";
import { ManageLayout } from "@/pages/manage/ManageLayout";
import { ManageTags } from "@/pages/manage/ManageTags";
import { useBoulders, type BoulderFilters, type BoulderSummary } from "@/features/boulders/api";

beforeEach(() => { routes.clear(); calls.length = 0; });

const boulder = (over: Partial<BoulderSummary> = {}): BoulderSummary => ({
  id: "b1", gymId: "g1", gymName: "Crimp", sectorId: "s1", sectorName: "Cave", photoUrl: "/p.jpg", holdColor: "BLUE",
  grades: [], status: "ACTIVE", createdAt: "2026-10-01T10:00:00Z", removedAt: null, rating: { average: 4.2, count: 5 }, viewer: null,
  intensity: "HIGH", tags: [{ id: "t1", name: "Placca" }, { id: "t2", name: "Tecnico" }, { id: "t3", name: "Dinamico" }], completions: 18, ...over,
});
const tags = [{ id: "t1", name: "Placca", boulders: 4 }, { id: "t2", name: "Tecnico", boulders: 2 }];

describe("A boulder's intensity, tags and sends", () => {
  it("shows intensity and sends on the card, and leaves the tags to the boulder's page", () => {
    render(<MemoryRouter><BoulderCard boulder={boulder()} /></MemoryRouter>);
    expect(screen.getByTitle("High intensity")).toBeInTheDocument();
    expect(screen.queryByText(/Placca/)).not.toBeInTheDocument();
    expect(screen.getByText("Completed by 18 people")).toBeInTheDocument();
  });

  it("puts the intensity next to the grade, on the photo, with the points opposite", () => {
    const { container } = render(<MemoryRouter><BoulderCard boulder={boulder({ points: 64 })} /></MemoryRouter>);
    const overlay = container.querySelector(".boulder-card__overlay")!;
    expect(overlay.querySelector(".boulder-card__grades")!.textContent).toContain("High intensity");
    expect(overlay.querySelector(".boulder-card__points")!.textContent).toContain("+64 pt");
    expect(screen.getByText("Worth 64 points")).toBeInTheDocument();
  });

  it("has a name line only for a boulder with a name, and never puts the sector there instead", () => {
    const { container, rerender } = render(<MemoryRouter><BoulderCard boulder={boulder({ name: "La Fessura" })} /></MemoryRouter>);
    expect(container.querySelector(".boulder-card__name")!.textContent).toBe("La Fessura");
    rerender(<MemoryRouter><BoulderCard boulder={boulder({ name: null })} /></MemoryRouter>);
    expect(container.querySelector(".boulder-card__name")).toBeNull();
    expect(container.querySelector(".boulder-card__sector")).not.toBeNull();
  });

  it("counts tries with an icon and a number, read out in words", () => {
    render(<MemoryRouter><BoulderCard boulder={boulder({ viewer: { attempts: 1, completed: false, completedAt: null, rating: null } })} /></MemoryRouter>);
    expect(screen.getByText("1 try")).toHaveClass("sr-only");
  });

  it("leaves out what a boulder doesn't have", () => {
    render(<MemoryRouter><BoulderCard boulder={boulder({ intensity: null, tags: [], completions: 0 })} /></MemoryRouter>);
    expect(screen.queryByTitle(/intensity/)).not.toBeInTheDocument();
    expect(screen.queryByText(/Completed by/)).not.toBeInTheDocument();
  });

  it("lists who completed it, and says how many prefer not to appear", async () => {
    reply("GET", "/api/boulders/b1/completions", {
      total: 3,
      people: { items: [{ person: { userId: "u1", displayName: "Giulia", avatarUrl: null }, completedAt: "2026-10-05T10:00:00Z" }], page: 1, pageSize: 30, total: 1, hasMore: false },
    });
    renderAt("/b", "/b", <CompletionsLine boulderId="b1" count={3} />);
    await userEvent.click(screen.getByRole("button", { name: /Completed by 3 people/ }));
    const sheet = await screen.findByRole("dialog");
    expect(await within(sheet).findByRole("link", { name: /Giulia/ })).toHaveAttribute("href", "/users/u1");
    expect(within(sheet).getByText("And 2 people who prefer not to appear.")).toBeInTheDocument();
  });

  it("says nobody has completed it yet instead of a zero", () => {
    renderAt("/b", "/b", <CompletionsLine boulderId="b1" count={0} />);
    expect(screen.getByText("Nobody has completed it yet")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});

describe("Filtering by tags and intensity", () => {
  it("adds every tag chosen and removes them one at a time", async () => {
    let filters: BoulderFilters = {};
    const onChange = vi.fn((f: BoulderFilters) => { filters = f; });
    const { rerender } = render(<FiltersPanel open filters={filters} onChange={onChange} sectors={[]} systems={[]} tags={tags} />);
    await userEvent.click(screen.getByRole("button", { name: "Placca" }));
    rerender(<FiltersPanel open filters={filters} onChange={onChange} sectors={[]} systems={[]} tags={tags} />);
    await userEvent.click(screen.getByRole("button", { name: "Tecnico" }));
    rerender(<FiltersPanel open filters={filters} onChange={onChange} sectors={[]} systems={[]} tags={tags} />);
    await userEvent.click(screen.getByRole("button", { name: /High intensity/ }));
    expect(filters).toEqual({ tagIds: ["t1", "t2"], intensity: ["HIGH"] });

    const active = activeFilters(filters, [], [], tags);
    expect(active.map((a) => a.label)).toEqual(["High intensity", "Placca", "Tecnico"]);
    // Taking one tag away keeps the other; the last one gone leaves no filter behind.
    expect(active[1]!.clear(filters)).toEqual({ tagIds: ["t2"], intensity: ["HIGH"] });
    expect(active[0]!.clear({ intensity: ["HIGH"] })).toEqual({ intensity: undefined });
  });
});

describe("Asking the server", () => {
  it("sends one tagId per tag and one intensity per level", async () => {
    reply("GET", "/api/gyms/g1/boulders", { items: [], page: 1, pageSize: 24, total: 0, hasMore: false });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
    renderHook(() => useBoulders("g1", { tagIds: ["t1", "t2"], intensity: ["LOW", "HIGH"] }), { wrapper });
    await waitFor(() => expect(calls.length).toBe(1));
    const query = new URLSearchParams(calls[0]!.path.split("?")[1]);
    expect(query.getAll("tagId")).toEqual(["t1", "t2"]);
    expect(query.getAll("intensity")).toEqual(["LOW", "HIGH"]);
  });
});

describe("The gym's own tags, for staff", () => {
  const gym = {
    id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null,
    address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: "STAFF", followerCount: 0,
  };

  it("adds a tag and deletes one with a second tap that says how many boulders lose it", async () => {
    reply("GET", "/api/gyms/crimp", gym);
    reply("GET", "/api/gyms/g1/tags", tags);
    reply("POST", "/api/gyms/g1/tags", { id: "t3", name: "Dinamico", boulders: 0 });
    reply("DELETE", "/api/tags/t1", undefined);
    renderAt("/manage/crimp/tags", "/manage/:slug", <ManageLayout />, <Route path="tags" element={<ManageTags />} />);

    expect(await screen.findByText("On 4 boulders on the wall")).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText("New tag"), "Dinamico");
    await userEvent.click(screen.getByRole("button", { name: "Add" }));
    await waitFor(() => expect(calls.find((c) => c.method === "POST")?.body).toEqual({ name: "Dinamico" }));

    await userEvent.click(screen.getByRole("button", { name: "Delete Placca" }));
    await userEvent.click(screen.getByRole("button", { name: "Remove from 4 boulders?" }));
    await waitFor(() => expect(calls.some((c) => c.method === "DELETE" && c.path === "/api/tags/t1")).toBe(true));
  });
});

describe("The favourite sector", () => {
  it("names the sector, the gym and the count, and links to its boulders", () => {
    render(<MemoryRouter><FavoriteSectorCard isMe sector={{
      sectorId: "s1", sectorName: "Grotta", gymId: "g1", gymName: "Crimp Factory", gymSlug: "crimp", completed: 23,
      floorPlanUrl: null, floorPlanWidth: null, floorPlanHeight: null, zone: null,
    }} /></MemoryRouter>);
    expect(screen.getByRole("heading", { name: "Your sector" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Grotta/ })).toHaveAttribute("href", "/gyms/crimp?sector=s1");
    expect(screen.getByText(/23 boulders completed here/)).toBeInTheDocument();
  });
});
