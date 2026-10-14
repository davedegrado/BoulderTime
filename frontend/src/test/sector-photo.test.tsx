import { Route } from "react-router-dom";
import { screen, waitFor } from "@testing-library/react";
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

  it("offers the wall once the sector is chosen, opens the card picture choice, and saves the wall's path", async () => {
    setup();
    renderAt("/manage/crimp/boulders/new", "/manage/:slug", <ManageLayout />, <Route path="boulders/new" element={<BoulderEditorPage />} />);

    const sectorSelect = await screen.findByRole("combobox", { name: "Sector *" });
    expect(screen.queryByRole("button", { name: "Use the photo of Cave" })).not.toBeInTheDocument();
    await userEvent.selectOptions(sectorSelect, "s1");
    await userEvent.click(screen.getByRole("button", { name: "Use the photo of Cave" }));

    // Straight to choosing this boulder's part of the wall.
    expect(screen.getByRole("heading", { name: "Card picture" })).toBeInTheDocument();
    await userEvent.click(screen.getAllByRole("button", { name: "Cancel" })[0]!);
    // Already set on it: nothing more to offer.
    expect(screen.queryByRole("button", { name: "Use the photo of Cave" })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("radio", { name: /blue/i }));
    await userEvent.click(screen.getByRole("button", { name: "Add boulder" }));

    await waitFor(() => expect(calls.find((c) => c.method === "POST" && c.path === "/api/gyms/g1/boulders")).toBeDefined());
    const saved = calls.find((c) => c.method === "POST" && c.path === "/api/gyms/g1/boulders")!.body as { photoPath: string; thumbnailPath: string | null };
    expect(saved.photoPath).toBe(wallPath);
    expect(saved.thumbnailPath).toBeNull();
    // No photo of its own was uploaded.
    expect(calls.some((c) => c.path === "/api/gyms/g1/boulder-photos")).toBe(false);
  });
});
