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

import { GymPage } from "@/pages/GymPage";
import { BoulderPage } from "@/pages/BoulderPage";
import { ManageLayout } from "@/pages/manage/ManageLayout";
import { ManageBoulders } from "@/pages/manage/ManageBoulders";
import { BoulderEditorPage } from "@/pages/manage/BoulderEditorPage";

const gym = { id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: null };
const sectors = [{ id: "s1", gymId: "g1", name: "Cave", description: null, imageUrl: null, sortOrder: 0, isActive: true }];
const systems = [
  { id: "font", gymId: "g1", name: "Fontainebleau", type: "FONTAINEBLEAU", isActive: true, sortOrder: 1, values: [{ id: "v6a", label: "6A", rank: 5, colorHex: null, isActive: true }] },
  { id: "col", gymId: "g1", name: "Colour", type: "COLOR", isActive: true, sortOrder: 0, values: [{ id: "vy", label: "Yellow", rank: 1, colorHex: "#F5C400", isActive: true }] },
];
const grades = [
  { gradeSystemId: "col", systemName: "Colour", systemType: "COLOR", gradeValueId: "vy", label: "Yellow", rank: 1, colorHex: "#F5C400" },
  { gradeSystemId: "font", systemName: "Fontainebleau", systemType: "FONTAINEBLEAU", gradeValueId: "v6a", label: "6A", rank: 5, colorHex: null },
];
const boulder = (id: string, over: object = {}) => ({ id, gymId: "g1", gymName: "Crimp Factory", rating: { average: null, count: 0 }, viewer: null, sectorId: "s1", sectorName: "Cave", photoUrl: `/img/${id}.jpg`, holdColor: "BLUE", grades, status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null, ...over });
const page = (items: unknown[]) => ({ items, page: 1, pageSize: 24, total: items.length, hasMore: false });

beforeEach(() => { routes.clear(); calls.length = 0; });

describe("Boulder list (climber)", () => {
  it("shows the colour GRADE and the physical HOLDS as separate, labelled facts", async () => {
    reply("GET", "/api/gyms/crimp", gym);
    reply("GET", "/api/gyms/g1/sectors", sectors);
    reply("GET", "/api/gyms/g1/grade-systems", systems);
    reply("GET", "/api/gyms/g1/boulders", page([boulder("b1")]));
    renderAt("/gyms/crimp", "/gyms/:slug", <GymPage />);

    const card = await screen.findByRole("link", { name: /grade yellow/i });
    expect(within(card).getByTitle("Colour grade: Yellow")).toHaveTextContent("Yellow");
    expect(within(card).getByText("Blue holds")).toBeInTheDocument();
    expect(within(card).getByText("Cave")).toBeInTheDocument();
    // The filters wait behind one button, so the boulders are on the first screen.
    expect(screen.queryByRole("combobox", { name: "Grade" })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Filters" }));
    expect(screen.getByRole("combobox", { name: "Grade" })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Hold colour" })).toBeInTheDocument();
  });

  it("shows the filters in use as chips that remove them", async () => {
    reply("GET", "/api/gyms/crimp", gym);
    reply("GET", "/api/gyms/g1/sectors", sectors);
    reply("GET", "/api/gyms/g1/grade-systems", systems);
    reply("GET", "/api/gyms/g1/boulders", page([boulder("b1")]));
    renderAt("/gyms/crimp", "/gyms/:slug", <GymPage />);

    await userEvent.click(await screen.findByRole("button", { name: "Filters" }));
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Sector" }), "s1");
    expect(screen.getByRole("button", { name: /Filters\s*1/ })).toBeInTheDocument();
    expect(calls.some((c) => c.path.includes("sectorId=s1"))).toBe(true);

    await userEvent.click(screen.getByRole("button", { name: "Remove filter Cave" }));
    expect(screen.queryByRole("button", { name: "Remove filter Cave" })).not.toBeInTheDocument();
  });
});

describe("Boulder page", () => {
  it("puts the grade on the photo and the facts in one line under it", async () => {
    reply("GET", "/api/boulders/b1", { ...boulder("b1", { rating: { average: 4.5, count: 3 } }), gymSlug: "crimp", gymName: "Crimp Factory", photoPath: "p", setter: null, viewerRole: null, isFollowing: false });
    reply("GET", "/api/boulders/b1/beta", undefined);
    reply("GET", "/api/boulders/b1/grade-consensus", { viewerCanSuggest: false, systems: [] });
    reply("GET", "/api/boulders/b1/comments", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
    renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);

    const grade = await screen.findByTitle("Colour grade: Yellow");
    expect(grade.closest(".boulder-page__photo")).not.toBeNull();
    const facts = screen.getByRole("list", { name: "About this boulder" });
    expect(within(facts).getByText("Blue holds")).toBeInTheDocument();
    expect(within(facts).getByText("Cave")).toBeInTheDocument();
    expect(within(facts).getByLabelText(/Rated 4.5 out of 5/)).toBeInTheDocument();
  });

  it("keeps removed boulders readable with a history notice and no edit for climbers", async () => {
    reply("GET", "/api/boulders/b1", { ...boulder("b1", { status: "REMOVED", removedAt: "2026-09-12T10:00:00Z" }), gymSlug: "crimp", gymName: "Crimp Factory", photoPath: "p", setter: { userId: "u9", displayName: "Marco", avatarUrl: null }, viewerRole: null, isFollowing: false });
    renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);

    expect(await screen.findByText(/this boulder was removed/i)).toBeInTheDocument();
    expect(screen.getByText("Blue holds")).toBeInTheDocument();
    expect(screen.getByText("Marco")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /edit/i })).not.toBeInTheDocument();
  });

  it("keeps the photo small and opens it full screen on request", async () => {
    reply("GET", "/api/boulders/b1", { ...boulder("b1"), gymSlug: "crimp", gymName: "Crimp Factory", photoPath: "p", setter: null, viewerRole: null, isFollowing: false });
    renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);

    // Both the photo itself and the corner button open it.
    const enlarge = await screen.findAllByRole("button", { name: "Enlarge the photo" });
    expect(enlarge).toHaveLength(2);
    await userEvent.click(enlarge[1]!);
    const viewer = screen.getByRole("dialog");
    expect(within(viewer).getByRole("img")).toHaveAttribute("src", "/img/b1.jpg");
    await userEvent.click(within(viewer).getByRole("button", { name: "Zoom in" }));
    expect(within(viewer).getByRole("button", { name: "Zoom out" })).toHaveAttribute("aria-pressed", "true");
    await userEvent.click(within(viewer).getByRole("button", { name: "Close" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("has no climber-video section at all where the gym hasn't switched them on", async () => {
    reply("GET", "/api/boulders/b1", { ...boulder("b1"), gymSlug: "crimp", gymName: "Crimp Factory", photoPath: "p", setter: null, viewerRole: null, isFollowing: false, communityVideosEnabled: false });
    renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);

    expect(await screen.findByText("Blue holds")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: /community videos|video della community/i })).not.toBeInTheDocument();
    expect(screen.queryByText(/aren't open at this gym/i)).not.toBeInTheDocument();
    expect(calls.some((c) => c.path.startsWith("/api/boulders/b1/videos"))).toBe(false);
  });
});

describe("Staff boulder management", () => {
  function setupManage() {
    reply("GET", "/api/gyms/crimp", { ...gym, viewerRole: "STAFF" });
    reply("GET", "/api/gyms/g1/sectors", sectors);
    reply("GET", "/api/gyms/g1/grade-systems", systems);
    reply("GET", "/api/gyms/g1/staff", []);
  }

  it("bulk-removes selected boulders after confirmation and summarises by sector", async () => {
    setupManage();
    reply("GET", "/api/gyms/g1/boulders", page([boulder("b1"), boulder("b2"), boulder("b3")]));
    reply("POST", "/api/gyms/g1/boulders/remove", { removed: 2, sectors: [{ sectorId: "s1", sectorName: "Cave", removed: 2 }] });
    renderAt("/manage/crimp/boulders", "/manage/:slug", <ManageLayout />, <Route path="boulders" element={<ManageBoulders />} />);

    await userEvent.click(await screen.findByRole("button", { name: "Select" }));
    const cards = screen.getAllByRole("button", { pressed: false }).filter((b) => b.classList.contains("boulder-card"));
    await userEvent.click(cards[0]!);
    await userEvent.click(cards[1]!);
    expect(screen.getByText("2 selected")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Remove" }));
    expect(calls.some((c) => c.path.endsWith("/boulders/remove"))).toBe(false);
    await userEvent.click(screen.getByRole("button", { name: "Remove 2?" }));

    const post = calls.find((c) => c.path === "/api/gyms/g1/boulders/remove");
    expect(post?.body).toEqual({ boulderIds: ["b1", "b2"], notifyFollowers: true });
    expect(await screen.findByText(/2 boulders removed/)).toBeInTheDocument();
  });

  it("requires photo, sector and hold colour before saving, but not a grade", async () => {
    setupManage();
    renderAt("/manage/crimp/boulders/new", "/manage/:slug", <ManageLayout />, <Route path="boulders/new" element={<BoulderEditorPage />} />);

    // Grades use one select per system; holds use a separate radio group.
    expect(await screen.findByRole("combobox", { name: "Colour" })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Fontainebleau" })).toBeInTheDocument();
    expect(screen.getByRole("group", { name: /hold colour/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Add boulder" }));
    expect(screen.getByText("Add a photo of the boulder.")).toBeInTheDocument();
    expect(screen.getByText("Choose a sector.")).toBeInTheDocument();
    expect(screen.getByText("Choose the hold colour.")).toBeInTheDocument();
    // A boulder can go on the wall before setters decide its grade.
    expect(screen.queryByText("Give the boulder at least one official grade.")).not.toBeInTheDocument();
    expect(calls.some((c) => c.method === "POST")).toBe(false);
  });

  it("lets staff pick an existing photo from the library, not just the camera", async () => {
    setupManage();
    renderAt("/manage/crimp/boulders/new", "/manage/:slug", <ManageLayout />, <Route path="boulders/new" element={<BoulderEditorPage />} />);

    await waitFor(() => expect(document.querySelector("input[type=file]")).not.toBeNull());
    const input = document.querySelector("input[type=file]") as HTMLInputElement;
    expect(input.accept).toContain("image/");
    // "capture" would force the camera and hide the photo library on phones.
    expect(input.hasAttribute("capture")).toBe(false);
  });
});


describe("Ungraded boulders", () => {
  it("say so where the grade usually is", async () => {
    const { GradeLine } = await import("@/features/boulders/BoulderBits");
    const { render } = await import("@testing-library/react");
    render(<GradeLine grades={[]} />);
    expect(screen.getByText("Ungraded")).toBeInTheDocument();
  });
});
