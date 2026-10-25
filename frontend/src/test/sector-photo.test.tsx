import { Route } from "react-router-dom";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";

vi.mock("@/auth/AuthProvider", () => ({ useAuth: () => ({ session: { access_token: "t" }, initializing: false }) }));

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string; body?: unknown }[] = [];
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
// jsdom has no canvas nor object URLs: the card picture is a stand-in, made as the real one would be asked to.
if (!URL.createObjectURL) Object.assign(URL, { createObjectURL: () => "blob:card", revokeObjectURL: () => {} });
const cardPicture = vi.fn(async (_source: Blob, mode: string) => new Blob([`card:${mode}`], { type: "image/jpeg" }));
vi.mock("@/features/boulders/imageResize", async (original) => ({ ...(await original<object>()), cardPicture: (s: Blob, m: string) => cardPicture(s, m) }));

vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string, bodyOrOpts?: unknown) => {
    const body = method === "GET" || method === "DELETE" ? undefined : bodyOrOpts;
    calls.push({ method, path, body });
    const handler = routes.get(`${method} ${path.split("?")[0]}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler(body);
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), patch: call("PATCH"), delete: call("DELETE"), blob: call("BLOB") } };
});

import { SectorPhotoPanel } from "@/features/gyms/SectorPhotoPanel";
import { ManageLayout } from "@/pages/manage/ManageLayout";
import { BoulderEditorPage } from "@/pages/manage/BoulderEditorPage";
import type { Sector } from "@/features/gyms/api";

const wallPath = "gyms/g1/sectors/wall.jpg";
const sector = (over: Partial<Sector> = {}): Sector => ({
  id: "s1", gymId: "g1", name: "Cave", description: null, imageUrl: null, photoPath: null, sortOrder: 0, isActive: true, isFollowing: false, ...over,
});
const withWall = () => sector({ imageUrl: "https://files/wall.jpg", photoPath: wallPath });
const ticket = { bucket: "boulder-images", path: "gyms/g1/sectors/new.jpg", uploadUrl: "https://upload/new", method: "PUT", headers: {}, maxBytes: 1, expiresAt: "" };
const photo = () => new File([new Uint8Array([1, 2, 3])], "wall.jpg", { type: "image/jpeg" });

beforeEach(() => {
  routes.clear();
  calls.length = 0;
  vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 200 })));
  reply("POST", "/api/sectors/s1/photo-uploads", ticket);
  reply("PUT", "/api/sectors/s1/photo", (body?: unknown) => ({ ...withWall(), photoPath: (body as { path: string | null }).path }));
});
afterEach(() => vi.unstubAllGlobals());

const upload = async (file: File) => userEvent.upload(screen.getByTestId("image-library"), file);
const photoWrite = () => calls.find((c) => c.method === "PUT" && c.path === "/api/sectors/s1/photo");

describe("A sector's wall photo", () => {
  it("is uploaded and set when the sector has none yet", async () => {
    renderAt("/x", "/x", <SectorPhotoPanel sector={sector()} gymId="g1" />);

    expect(screen.getByRole("button", { name: "Upload photo" })).toBeInTheDocument();
    await upload(photo());

    await waitFor(() => expect(photoWrite()?.body).toEqual({ path: ticket.path }));
  });

  it("is replaced without fuss: the boulders already set keep the photo they have", async () => {
    renderAt("/x", "/x", <SectorPhotoPanel sector={withWall()} gymId="g1" />);

    expect(screen.getByText(/the boulders already set keep the photo they have/)).toBeInTheDocument();
    await upload(photo());
    await waitFor(() => expect(photoWrite()?.body).toEqual({ path: ticket.path }));
  });

  it("can be removed", async () => {
    renderAt("/x", "/x", <SectorPhotoPanel sector={withWall()} gymId="g1" />);

    await userEvent.click(screen.getByRole("button", { name: "Remove" }));
    await waitFor(() => expect(photoWrite()?.body).toEqual({ path: null }));
  });
});

describe("Setting a boulder on its sector's wall photo", () => {
  const gym = { id: "g1", slug: "crimp", name: "Crimp Factory", city: "Milano", logoUrl: null, coverImageUrl: null, status: "ACTIVE", description: null, address: null, website: null, email: null, phone: null, createdAt: "2026-01-01T00:00:00Z", viewerRole: "STAFF" };
  function setup() {
    reply("GET", "/api/gyms/crimp", gym);
    reply("GET", "/api/gyms/g1/sectors", [withWall()]);
    reply("GET", "/api/gyms/g1/grade-systems", []);
    reply("GET", "/api/gyms/g1/staff", []);
    reply("GET", "/api/gyms/g1/tags", []);
    reply("POST", "/api/gyms/g1/boulders", (body?: unknown) => ({ id: "b1", ...(body as object) }));
  }

  it("offers to highlight the holds on the wall photo of the boulder's sector, and says what to do when there are none", async () => {
    setup();
    renderAt("/manage/crimp/boulders/new", "/manage/:slug", <ManageLayout />, <Route path="boulders/new" element={<BoulderEditorPage />} />);
    // No sector yet: no wall to offer.
    expect(await screen.findByText(/No photo of the wall yet/)).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByRole("combobox", { name: "Sector *" }), "s1");
    await userEvent.click(await screen.findByRole("button", { name: "Highlight the holds" }));
    expect(screen.getByRole("dialog", { name: "Highlight the holds" })).toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Photo of the wall" })).toHaveValue("sector:s1");
    await userEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("offers to go on from the holds highlighted before, and keeps them when only other things change", async () => {
    setup();
    const settings = JSON.stringify({ color: "YELLOW", outline: { on: true, color: "red", level: 2 } });
    reply("GET", "/api/boulders/b1", {
      id: "b1", gymId: "g1", gymName: "Crimp Factory", gymSlug: "crimp", sectorId: "s1", sectorName: "Cave", photoUrl: "/img/b1.jpg", photoPath: "gyms/g1/boulders/b1.jpg",
      thumbnailUrl: "/img/b1.thumb.jpg", holdColor: "YELLOW", grades: [], setter: null, status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null,
      viewerRole: "STAFF", rating: { average: null, count: 0 }, viewer: null, isFollowing: false, communityVideosEnabled: false, canAddOfficialBeta: true,
      highlight: { sourcePath: wallPath, maskPath: "gyms/g1/boulders/m.mask.png", settings },
    });
    reply("GET", "/api/boulders/b1/beta", null);
    reply("GET", "/api/settings/videos", { uploadsEnabled: true, maxUploadMegabytes: 50 });
    reply("PUT", "/api/boulders/b1", (body?: unknown) => body);
    renderAt("/manage/crimp/boulders/b1/edit", "/manage/:slug", <ManageLayout />, <Route path="boulders/:boulderId/edit" element={<BoulderEditorPage />} />);

    expect(await screen.findByRole("button", { name: "Edit the highlighted holds" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Start again from a photo" })).toBeInTheDocument();
    // The card picture's choices are there; how this one was made isn't known (an older server, or an older app).
    const modes = screen.getByRole("radiogroup", { name: "Card picture" });
    expect(within(modes).getAllByRole("radio").map((b) => b.textContent)).toEqual(["Fill", "Fit", "Custom crop"]);
    expect(within(modes).getAllByRole("radio").every((b) => b.getAttribute("aria-checked") === "false")).toBe(true);

    // Only the name changes: the photo and its highlight stay as they are (nothing sent replaces them).
    await userEvent.type(screen.getByRole("textbox", { name: "Name" }), "La Fessura");
    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(calls.find((c) => c.method === "PUT" && c.path === "/api/boulders/b1")).toBeDefined());
    const sent = calls.find((c) => c.method === "PUT" && c.path === "/api/boulders/b1")!.body as Record<string, unknown>;
    expect(sent.photoPath).toBe("gyms/g1/boulders/b1.jpg");
    expect(sent).not.toHaveProperty("highlight");
    // No new card picture, so nothing said about how one was made.
    expect(sent).not.toHaveProperty("thumbnailMode");
  });

  it("highlights a boulder of a sector without a wall photo on its own photo, never on another sector's wall", async () => {
    setup();
    // Only Cave has a wall photo; the boulder is in Slab, with a photo of its own.
    reply("GET", "/api/gyms/g1/sectors", [withWall(), sector({ id: "s2", name: "Slab", sortOrder: 1 })]);
    reply("GET", "/api/boulders/b2", {
      id: "b2", gymId: "g1", gymName: "Crimp Factory", gymSlug: "crimp", sectorId: "s2", sectorName: "Slab", photoUrl: "/img/b2.jpg", photoPath: "gyms/g1/boulders/b2.jpg",
      thumbnailUrl: null, holdColor: "BLUE", grades: [], setter: null, status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null,
      viewerRole: "STAFF", rating: { average: null, count: 0 }, viewer: null, isFollowing: false, communityVideosEnabled: false, canAddOfficialBeta: true, highlight: null,
    });
    reply("GET", "/api/boulders/b2/beta", null);
    reply("GET", "/api/settings/videos", { uploadsEnabled: true, maxUploadMegabytes: 50 });
    renderAt("/manage/crimp/boulders/b2/edit", "/manage/:slug", <ManageLayout />, <Route path="boulders/:boulderId/edit" element={<BoulderEditorPage />} />);

    await userEvent.click(await screen.findByRole("button", { name: "Highlight the holds" }));
    const photos = screen.getByRole("combobox", { name: "Photo of the wall" });
    expect(photos).toHaveValue("current");
    expect([...(photos as HTMLSelectElement).options].map((o) => o.textContent)).toEqual(["This boulder's photo"]);
  });

  it("explains that a wall photo is needed when no sector has one", async () => {
    setup();
    reply("GET", "/api/gyms/g1/sectors", [sector()]);
    renderAt("/manage/crimp/boulders/new", "/manage/:slug", <ManageLayout />, <Route path="boulders/new" element={<BoulderEditorPage />} />);
    expect(await screen.findByText(/No photo of the wall yet/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Highlight the holds" })).not.toBeInTheDocument();
  });

  it("shows how a saved card picture was made, and makes a new one with the choice picked, saying which", async () => {
    setup();
    reply("GET", "/api/boulders/b1", {
      id: "b1", gymId: "g1", gymName: "Crimp Factory", gymSlug: "crimp", sectorId: "s1", sectorName: "Cave", photoUrl: "/img/b1.jpg", photoPath: "gyms/g1/boulders/b1.jpg",
      thumbnailUrl: "/img/b1.thumb.jpg", holdColor: "BLUE", grades: [], setter: null, status: "ACTIVE", createdAt: "2026-09-01T00:00:00Z", removedAt: null,
      viewerRole: "STAFF", rating: { average: null, count: 0 }, viewer: null, isFollowing: false, communityVideosEnabled: false, canAddOfficialBeta: true,
      thumbnailMode: "FIT",
    });
    reply("GET", "/api/boulders/b1/beta", null);
    reply("GET", "/api/settings/videos", { uploadsEnabled: true, maxUploadMegabytes: 50 });
    reply("BLOB", "/api/boulders/b1/photo", new Blob(["photo"], { type: "image/jpeg" }));
    reply("POST", "/api/gyms/g1/boulder-photos", { ...ticket, path: "gyms/g1/boulders/new.thumb.jpg" });
    reply("PUT", "/api/boulders/b1", (body?: unknown) => body);
    renderAt("/manage/crimp/boulders/b1/edit", "/manage/:slug", <ManageLayout />, <Route path="boulders/:boulderId/edit" element={<BoulderEditorPage />} />);

    expect(await screen.findByRole("radio", { name: /Fit/ })).toHaveAttribute("aria-checked", "true");
    expect(screen.queryByRole("radiogroup", { name: "Part of the photo to keep" })).not.toBeInTheDocument();

    // Fill starts from the centre, then keeps the top.
    await userEvent.click(screen.getByRole("radio", { name: /Fill/ }));
    await waitFor(() => expect(cardPicture).toHaveBeenLastCalledWith(expect.any(Blob), "FILL_CENTER"));
    await userEvent.click(screen.getByRole("radio", { name: "Top" }));
    await waitFor(() => expect(cardPicture).toHaveBeenLastCalledWith(expect.any(Blob), "FILL_TOP"));
    expect(screen.getByRole("radio", { name: "Top" })).toHaveAttribute("aria-checked", "true");

    await userEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(calls.find((c) => c.method === "PUT" && c.path === "/api/boulders/b1")).toBeDefined());
    const sent = calls.find((c) => c.method === "PUT" && c.path === "/api/boulders/b1")!.body as Record<string, unknown>;
    expect(sent.thumbnailPath).toBe("gyms/g1/boulders/new.thumb.jpg");
    expect(sent.thumbnailMode).toBe("FILL_TOP");
  });

  it("opens the cropper for a part chosen by hand", async () => {
    setup();
    renderAt("/manage/crimp/boulders/new", "/manage/:slug", <ManageLayout />, <Route path="boulders/new" element={<BoulderEditorPage />} />);
    reply("BLOB", "/api/sectors/s1/photo", new Blob(["wall"], { type: "image/jpeg" }));
    await userEvent.selectOptions(await screen.findByRole("combobox", { name: "Sector *" }), "s1");
    await userEvent.click(screen.getByRole("button", { name: "Use the photo of Cave" }));
    await userEvent.click(await screen.findByRole("radio", { name: /Custom crop/ }));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("offers the wall once the sector is chosen, leaves the card picture optional, and saves the wall's path with a small picture of it", async () => {
    setup();
    reply("BLOB", "/api/sectors/s1/photo", new Blob(["wall"], { type: "image/jpeg" }));
    reply("POST", "/api/gyms/g1/boulder-photos", { ...ticket, path: "gyms/g1/boulders/whole.thumb.jpg" });
    renderAt("/manage/crimp/boulders/new", "/manage/:slug", <ManageLayout />, <Route path="boulders/new" element={<BoulderEditorPage />} />);

    const sectorSelect = await screen.findByRole("combobox", { name: "Sector *" });
    expect(screen.queryByRole("button", { name: "Use the photo of Cave" })).not.toBeInTheDocument();
    await userEvent.selectOptions(sectorSelect, "s1");
    await userEvent.click(screen.getByRole("button", { name: "Use the photo of Cave" }));

    // The card picture is made by itself, filling the card from the centre: nothing opens.
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("radio", { name: /Fill/ })).toHaveAttribute("aria-checked", "true"));
    expect(screen.getByRole("radio", { name: "Centre" })).toHaveAttribute("aria-checked", "true");
    // Already set on it: nothing more to offer.
    expect(screen.queryByRole("button", { name: "Use the photo of Cave" })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("radio", { name: /^blue$/i }));
    await userEvent.click(screen.getByRole("button", { name: "Add boulder" }));

    await waitFor(() => expect(calls.find((c) => c.method === "POST" && c.path === "/api/gyms/g1/boulders")).toBeDefined());
    const saved = calls.find((c) => c.method === "POST" && c.path === "/api/gyms/g1/boulders")!.body as { photoPath: string; thumbnailPath: string | null; thumbnailMode?: string };
    expect(saved.thumbnailMode).toBe("FILL_CENTER");
    expect(cardPicture).toHaveBeenLastCalledWith(expect.any(Blob), "FILL_CENTER");
    expect(saved.photoPath).toBe(wallPath);
    // A small picture of the whole wall for the lists, not the 1600 px photo; no photo of its own uploaded.
    expect(saved.thumbnailPath).toBe("gyms/g1/boulders/whole.thumb.jpg");
    const uploads = calls.filter((c) => c.path === "/api/gyms/g1/boulder-photos").map((c) => c.body as { thumbnail?: boolean });
    expect(uploads).toEqual([expect.objectContaining({ thumbnail: true })]);
  });
});
