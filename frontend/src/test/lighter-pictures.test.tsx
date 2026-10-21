import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ToastProvider } from "@/components/Toast";

// jsdom has no image decoding: a picture is "decoded" to the size written in it, and encoding gives a blob whose size
// depends on the canvas and the quality, so the test can see what each picture was made into.
let encoded: { width: number; height: number; quality?: number }[] = [];
const sizes = new WeakMap<Blob, [number, number]>();
/** A JPEG of the given size in pixels and in bytes. */
const picture = (width: number, height: number, bytes = 1000) => {
  const blob = new Blob([new Uint8Array(bytes)], { type: "image/jpeg" });
  sizes.set(blob, [width, height]);
  return blob;
};
beforeAll(() => {
  vi.stubGlobal("createImageBitmap", vi.fn(async (blob: Blob) => {
    const [width, height] = sizes.get(blob) ?? [4032, 3024];
    return { width, height, close: () => {} };
  }));
  HTMLCanvasElement.prototype.getContext = function getContext() {
    return { drawImage: () => {} } as unknown as CanvasRenderingContext2D;
  } as unknown as typeof HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.toBlob = function toBlob(this: HTMLCanvasElement, cb: BlobCallback, type?: string, quality?: number) {
    encoded.push({ width: this.width, height: this.height, quality });
    cb(new Blob([new Uint8Array(Math.round((this.width * this.height) / 50))], { type: type ?? "image/png" }));
  };
});
afterAll(() => vi.unstubAllGlobals());

import { BOULDER_PHOTO, CARD_PICTURE, cropImage, prepareImage, WHOLE_CARD_PICTURE } from "@/features/boulders/imageResize";

describe("How boulder pictures are stored", () => {
  beforeEach(() => { encoded = []; });

  it("keeps the photo at 1280 px and encodes it again even when it is already small", async () => {
    await prepareImage(picture(4032, 3024), BOULDER_PHOTO);
    expect(encoded.at(-1)).toEqual({ width: 1280, height: 960, quality: 0.7 });
    // A small but heavy JPEG (a screenshot, an edited photo) no longer goes up as it is.
    await prepareImage(picture(1000, 800), BOULDER_PHOTO);
    expect(encoded.at(-1)).toEqual({ width: 1000, height: 800, quality: 0.7 });
  });

  it("makes card pictures of 480 px", async () => {
    await cropImage(picture(1280, 960), { x: 0, y: 0, width: 600, height: 480 }, CARD_PICTURE);
    expect(encoded.at(-1)).toEqual({ width: 480, height: 384, quality: 0.7 });
    await prepareImage(picture(1280, 960), WHOLE_CARD_PICTURE);
    expect(encoded.at(-1)).toEqual({ width: 480, height: 360, quality: 0.7 });
  });
});

type Handler = (body?: unknown) => unknown;
const routes = new Map<string, Handler>();
const calls: { method: string; path: string; body?: unknown }[] = [];
const reply = (method: string, path: string, value: unknown) => routes.set(`${method} ${path}`, typeof value === "function" ? (value as Handler) : () => value);
vi.mock("@/lib/api", () => {
  const call = (method: string) => async (path: string, bodyOrOpts?: unknown) => {
    const body = method === "GET" || method === "BLOB" ? undefined : bodyOrOpts;
    calls.push({ method, path, body });
    const handler = routes.get(`${method} ${path}`);
    if (!handler) throw new Error(`No mock for ${method} ${path}`);
    return handler(body);
  };
  return { api: { get: call("GET"), post: call("POST"), put: call("PUT"), delete: call("DELETE"), blob: call("BLOB") } };
});

import { LighterPicturesCard } from "@/features/admin/LighterPicturesCard";

const base = "/api/admin/gyms/g1/lighter-pictures";
const ticket = (path: string) => ({ bucket: "boulder-images", path, uploadUrl: "https://upload/x", method: "PUT", headers: {}, maxBytes: 1, expiresAt: "" });

function renderCard() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><ToastProvider><LighterPicturesCard gymId="g1" gymSlug="crimp" /></ToastProvider></QueryClientProvider>);
}

describe("Making a gym's pictures lighter", () => {
  beforeEach(() => {
    routes.clear();
    calls.length = 0;
    vi.stubGlobal("fetch", vi.fn(async () => new Response(null, { status: 200 })));
  });

  it("swaps each picture for a lighter copy, leaving one that is already light", async () => {
    let done = false;
    reply("GET", base, () => done
      ? { pending: [], replaced: 1, bytesBefore: 600_000, bytesAfter: 300_000, confirmed: 0 }
      : { pending: [{ boulderId: "b1", file: "PHOTO", path: "gyms/g1/boulders/a.jpg" }, { boulderId: "b2", file: "THUMBNAIL", path: "gyms/g1/boulders/b.thumb.jpg" }], replaced: 0, bytesBefore: 0, bytesAfter: 0, confirmed: 0 });
    // A heavy phone photo, and a card picture already about as light as it gets.
    reply("BLOB", `${base}/b1/PHOTO`, picture(4032, 3024, 500_000));
    reply("BLOB", `${base}/b2/THUMBNAIL`, picture(480, 384, 100));
    reply("POST", `${base}/uploads`, ticket("gyms/g1/boulders/new.jpg"));
    reply("POST", base, () => { done = true; return { bytesBefore: 500_000, bytesAfter: 24_576 }; });
    renderCard();

    expect(await screen.findByText("To make lighter: 2")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Make them lighter" }));

    await waitFor(() => expect(screen.getByText(/Replaced: 1/)).toBeInTheDocument());
    expect(calls.filter((c) => c.method === "POST" && c.path === base).map((c) => c.body)).toEqual([
      { boulderId: "b1", file: "PHOTO", from: "gyms/g1/boulders/a.jpg", to: "gyms/g1/boulders/new.jpg" },
    ]);
    expect(calls.find((c) => c.path === `${base}/uploads`)!.body).toEqual({ file: "PHOTO", sizeBytes: expect.any(Number) });
  });

  it("puts everything back or confirms only with the gym's slug typed", async () => {
    reply("GET", base, { pending: [], replaced: 3, bytesBefore: 3_000_000, bytesAfter: 1_200_000, confirmed: 0 });
    reply("POST", `${base}/undo`, { changed: 3, skipped: 0, bytesFreed: 1_200_000 });
    renderCard();

    expect(await screen.findByText("Replaced: 3 · from 2.9 MB to 1.1 MB (60% less)")).toBeInTheDocument();
    const undo = screen.getByRole("button", { name: "Put them all back" });
    expect(undo).toBeDisabled();
    expect(screen.getByRole("button", { name: "Confirm and delete the old files" })).toBeDisabled();
    await userEvent.type(screen.getByRole("textbox", { name: "Type the gym's slug to confirm" }), "crimp");
    await userEvent.click(undo);
    await waitFor(() => expect(calls.find((c) => c.path === `${base}/undo`)?.body).toEqual({ gymSlug: "crimp" }));
    expect(calls.some((c) => c.path === `${base}/confirm`)).toBe(false);
  });
});
