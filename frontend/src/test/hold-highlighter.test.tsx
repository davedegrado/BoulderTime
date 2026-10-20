import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

// jsdom draws nothing: the photo is a 40×20 picture whose left half is yellow and right half is the grey wall, and the
// canvas keeps pixels in memory so the real detection and composition run on them.
vi.mock("@/features/boulders/imageResize", () => ({
  decodeImage: vi.fn(async (blob: Blob) => {
    if (blob.type === "image/heic") throw new Error("unsupported");
    return { image: { naturalWidth: 40, naturalHeight: 20 }, release: () => {} };
  }),
}));

const W = 40, H = 20;
function photoPixels() {
  const data = new Uint8ClampedArray(W * H * 4);
  for (let p = 0; p < W * H; p++) data.set((p % W) < W / 2 ? [245, 200, 0, 255] : [190, 190, 185, 255], p * 4);
  return data;
}
let drawn: Uint8ClampedArray | null = null;
beforeAll(() => {
  HTMLCanvasElement.prototype.getContext = function getContext() {
    return {
      drawImage: () => {},
      getImageData: (_x: number, _y: number, w: number, h: number) => ({ width: w, height: h, data: photoPixels() }),
      createImageData: (w: number, h: number) => ({ width: w, height: h, data: new Uint8ClampedArray(w * h * 4) }),
      putImageData: (img: { data: Uint8ClampedArray }) => { drawn = img.data; },
    } as unknown as CanvasRenderingContext2D;
  } as unknown as typeof HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.toBlob = function toBlob(cb: BlobCallback) { cb(new Blob(["jpeg"], { type: "image/jpeg" })); };
});

import { HoldHighlighter, type HighlightSource } from "@/features/boulders/HoldHighlighter";

const wallOf = (name: string, type = "image/jpeg"): HighlightSource => ({ id: name, label: `Wall of ${name}`, load: vi.fn(async () => new Blob(["x"], { type })) });

describe("Highlighting the holds", () => {
  it("starts from the boulder's colour, makes a preview, and hands back a photo in that colour", async () => {
    const onDone = vi.fn();
    const cave = wallOf("Cave");
    render(<HoldHighlighter sources={[cave, wallOf("Slab")]} initialColor="YELLOW" onCancel={() => {}} onDone={onDone} />);

    expect(screen.getByRole("combobox", { name: "Photo of the wall" })).toHaveValue("Cave");
    expect(screen.getByRole("radio", { name: "Yellow" })).toHaveAttribute("aria-checked", "true");
    await userEvent.click(screen.getByRole("button", { name: "Show the preview" }));

    // The yellow half keeps its colour, the wall turns grey.
    await screen.findByRole("radio", { name: "Keep colour" });
    await waitFor(() => expect(drawn).not.toBeNull());
    expect(Array.from(drawn!.slice(0, 3))).toEqual([245, 200, 0]);
    const wallPixel = Array.from(drawn!.slice((W - 1) * 4, (W - 1) * 4 + 3));
    expect(wallPixel[0]).toBe(wallPixel[2]);
    expect(screen.getByRole("button", { name: /Undo/ })).toBeDisabled();

    await userEvent.click(screen.getAllByRole("button", { name: "Use this photo" })[0]!);
    await waitFor(() => expect(onDone).toHaveBeenCalledWith(expect.any(Blob), "YELLOW"));
    expect(cave.load).toHaveBeenCalledTimes(1);
  });

  it("says when a photo can't be opened and lets staff try again", async () => {
    render(<HoldHighlighter sources={[wallOf("Cave", "image/heic")]} initialColor="RED" onCancel={() => {}} onDone={() => {}} />);
    await userEvent.click(screen.getByRole("button", { name: "Show the preview" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("This photo can't be opened here");
    expect(screen.getByRole("button", { name: "Try again" })).toBeEnabled();
  });

  it("asks for a colour when the boulder has none yet, and warns that mixed holds are painted by hand", async () => {
    render(<HoldHighlighter sources={[wallOf("Cave")]} initialColor="" onCancel={() => {}} onDone={() => {}} />);
    expect(screen.getByRole("button", { name: "Show the preview" })).toBeDisabled();
    await userEvent.click(screen.getByRole("radio", { name: "Mixed" }));
    expect(screen.getByText(/Mixed holds can't be found by colour/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Show the preview" })).toBeEnabled();
  });

  it("closes without touching anything when cancelled", async () => {
    const onCancel = vi.fn();
    const onDone = vi.fn();
    render(<HoldHighlighter sources={[wallOf("Cave")]} initialColor="BLUE" onCancel={onCancel} onDone={onDone} />);
    await userEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onCancel).toHaveBeenCalled();
    expect(onDone).not.toHaveBeenCalled();
  });
});
