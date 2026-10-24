import { screen, within } from "@testing-library/react";
import { renderAt } from "@/test/renderApp";

let signedIn = true;
vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: signedIn ? { access_token: "t" } : null, initializing: false }) }));

type Handler = () => unknown;
const routes = new Map<string, Handler>();
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, () => value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string) => {
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler();
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE") } };
});

import { BoulderPage } from "@/pages/BoulderPage";
import { HOLD_COLORS, holdLabel } from "@/features/boulders/holdColors";
import { it as italian } from "@/i18n/it";

const grades = [{ gradeSystemId: "col", systemName: "Colour", systemType: "COLOR", gradeValueId: "vg", label: "Green", rank: 1, colorHex: "#2E9E4F" }];
/** Everything a boulder can have; each test takes away what it is about. */
const full = {
  id: "b1", gymId: "g1", gymSlug: "crimp", gymName: "Crimp Factory", sectorId: "s1", sectorName: "Onda", photoUrl: "/b1.jpg", photoPath: "p",
  name: "Ex Moon", holdColor: "BLUE", grades, points: 46, status: "ACTIVE", createdAt: "2026-10-13T00:00:00Z", removedAt: null,
  rating: { average: 3, count: 4 }, completions: 2, tags: [{ id: "t1", name: "Slab" }, { id: "t2", name: "Dynamic" }],
  setter: null, intensity: null, viewer: null, viewerRole: null, isFollowing: false,
};

function show(boulder: object) {
  reply("GET", "/api/boulders/b1", boulder);
  reply("GET", "/api/boulders/b1/beta", undefined);
  reply("GET", "/api/boulders/b1/grade-consensus", { viewerCanSuggest: false, systems: [] });
  reply("GET", "/api/boulders/b1/comments", { items: [], page: 1, pageSize: 20, total: 0, hasMore: false });
  reply("GET", "/api/gyms/crimp", { id: "g1", slug: "crimp", name: "Crimp Factory" });
  reply("GET", "/api/gyms/g1/sectors", []);
  return renderAt("/boulders/b1", "/boulders/:id", <BoulderPage />);
}

beforeEach(() => { routes.clear(); signedIn = true; });

describe("A boulder's page", () => {
  it("with everything: photo corners, name, facts, community rating, then the tags before the progress", async () => {
    const { container } = show(full);
    expect(await screen.findByRole("heading", { level: 1, name: "Ex Moon" })).toBeInTheDocument();
    const photo = container.querySelector(".boulder-page__photo")!;
    expect(within(photo as HTMLElement).getByText("Blue holds").closest(".boulder-page__holds")).not.toBeNull();
    expect(photo.querySelector("button.boulder-page__enlarge")).toHaveAccessibleName("Enlarge the photo");
    expect(within(photo as HTMLElement).getByTitle("Colour grade: Green").closest(".boulder-page__grades--on-photo")).not.toBeNull();
    expect(within(photo as HTMLElement).getByText("+46 pt")).toBeInTheDocument();

    const facts = screen.getByRole("list", { name: "About this boulder" });
    expect(within(facts).getByText("Onda")).toBeInTheDocument();
    expect(within(facts).getByText("2 sends")).toBeInTheDocument();
    expect(screen.getByLabelText("Rated 3 out of 5 by 4 climbers")).toHaveTextContent("3.0");

    const tags = screen.getByRole("list", { name: "Tags" });
    const progress = screen.getByRole("region", { name: "Your progress" });
    expect(tags.compareDocumentPosition(progress) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(facts.compareDocumentPosition(tags) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("without a name has no title row, only a heading for screen readers", async () => {
    const { container } = show({ ...full, name: null });
    const heading = await screen.findByRole("heading", { level: 1, name: "Boulder in Onda" });
    expect(heading).toHaveClass("sr-only");
    expect(container.querySelector(".boulder-page__name")).toBeNull();
    expect(screen.getByRole("list", { name: "Tags" })).toBeInTheDocument();
  });

  it("without a name nor tags leaves no empty space for either", async () => {
    const { container } = show({ ...full, name: "  ", tags: [] });
    await screen.findByRole("list", { name: "About this boulder" });
    expect(container.querySelector(".boulder-page__name")).toBeNull();
    expect(container.querySelector(".tag-list")).toBeNull();
  });

  it("with most of it missing shows only what there is", async () => {
    const { container } = show({ ...full, name: null, tags: undefined, rating: { average: null, count: 0 }, completions: 0, points: null, setter: null, intensity: null });
    const facts = await screen.findByRole("list", { name: "About this boulder" });
    expect(within(facts).getAllByRole("listitem")).toHaveLength(2); // sector and date
    expect(container.querySelector(".boulder-page__rating-card")).toBeNull();
    expect(container.querySelector(".boulder-page__points")).toBeNull();
    expect(container.querySelector(".tag-list")).toBeNull();
    // The hold colour is still on the photo.
    expect(container.querySelector(".boulder-page__holds")).toHaveTextContent("Blue holds");
  });

  it("without a photo puts hold colour, grade and points in the facts instead", async () => {
    const { container } = show({ ...full, photoUrl: null });
    const facts = await screen.findByRole("list", { name: "About this boulder" });
    expect(within(facts).getByText("Blue holds")).toBeInTheDocument();
    expect(within(facts).getByText("Worth 46 points")).toBeInTheDocument();
    expect(container.querySelector(".boulder-page__holds")).toBeNull();
  });

  it("not completed: attempts, the climber's own rating and Mark as completed", async () => {
    show({ ...full, viewer: { attempts: 3, completed: false, completedAt: null, rating: 4 } });
    const progress = await screen.findByRole("region", { name: "Your progress" });
    expect(within(progress).getByText("3")).toBeInTheDocument();
    expect(within(progress).getByRole("radiogroup", { name: "Your rating" })).toBeInTheDocument();
    expect(within(progress).getByRole("button", { name: "Mark as completed" })).toBeInTheDocument();
    // The community's rating is not in the progress, the climber's own is not up top.
    expect(within(progress).queryByText("Community rating")).not.toBeInTheDocument();
  });

  it("completed: when, and a way to undo it, where the button was", async () => {
    show({ ...full, viewer: { attempts: 2, completed: true, completedAt: "2026-10-14T00:00:00Z", rating: null } });
    const progress = await screen.findByRole("region", { name: "Your progress" });
    expect(within(progress).getByText("Completed")).toBeInTheDocument();
    expect(within(progress).getByRole("button", { name: "Undo" })).toBeInTheDocument();
    expect(within(progress).queryByRole("button", { name: "Mark as completed" })).not.toBeInTheDocument();
  });

  it("taken off the wall says so and keeps everything else", async () => {
    show({ ...full, status: "REMOVED", removedAt: "2026-10-20T00:00:00Z" });
    expect(await screen.findByRole("note")).toHaveTextContent("This boulder was removed on");
    expect(screen.getByRole("heading", { level: 1, name: "Ex Moon" })).toBeInTheDocument();
  });

  it("with no comments says so in one line and, signed in, offers to write the first", async () => {
    show(full);
    expect(await screen.findByText(/No comments yet\. Be the first/)).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "Write a comment" })).toBeInTheDocument();
  });

  it.each([["LIGHT_BLUE", "Light blue holds"], ["WOOD", "Wood holds"]])("shows %s holds", async (holdColor, label) => {
    const { container } = show({ ...full, holdColor });
    await screen.findByRole("list", { name: "About this boulder" });
    expect(container.querySelector(".boulder-page__holds")).toHaveTextContent(label);
  });
});

describe("Hold colours", () => {
  it("each has its own name, Italian wording and swatch, and the new ones sit next to their kin", () => {
    const values = HOLD_COLORS.map((c) => c.value);
    expect(new Set(values).size).toBe(values.length);
    expect(new Set(HOLD_COLORS.map((c) => c.hex)).size).toBe(values.length);
    for (const c of HOLD_COLORS) expect(italian[c.label], c.label).toBeTruthy();
    expect(values.indexOf("LIGHT_BLUE")).toBe(values.indexOf("BLUE") + 1);
    expect(values.indexOf("WOOD")).toBe(values.indexOf("BROWN") + 1);
    expect(holdLabel("LIGHT_BLUE")).toBe("Light blue holds");
    expect(holdLabel("WOOD")).toBe("Wood holds");
  });

  it("an unknown colour from a future server still shows as a colour, not as a crash", () => {
    expect(holdLabel("TEAL" as never)).toMatch(/holds$/);
  });
});
