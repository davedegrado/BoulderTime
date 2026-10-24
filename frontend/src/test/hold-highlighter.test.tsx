import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

// jsdom draws nothing: the photo is a 40×20 picture whose left half is yellow and right half is the grey wall, and the
// canvas keeps pixels in memory so the real detection and composition run on them.
vi.mock("@/features/boulders/imageResize", () => ({
  BOULDER_PHOTO: { maxSide: 1280, quality: 0.7, force: true },
  decodeImage: vi.fn(async (blob: Blob) => {
    if (blob.type === "image/heic") throw new Error("unsupported");
    return { image: { naturalWidth: 40, naturalHeight: 20, kind: blob.type }, release: () => {} };
  }),
}));

const W = 40, H = 20;
/** A saved mask: white only over the left quarter (x < 10), where the automatic guess would take the whole yellow half. */
function maskPixels() {
  const data = new Uint8ClampedArray(W * H * 4);
  for (let p = 0; p < W * H; p++) { const v = (p % W) < 10 ? 255 : 0; data.set([v, v, v, 255], p * 4); }
  return data;
}
function photoPixels() {
  const data = new Uint8ClampedArray(W * H * 4);
  for (let p = 0; p < W * H; p++) data.set((p % W) < W / 2 ? [245, 200, 0, 255] : [190, 190, 185, 255], p * 4);
  return data;
}
let drawn: Uint8ClampedArray | null = null;
// The picture as it is encoded for saving (the JPEG), apart from the mask PNG encoded after it.
let savedJpeg: Uint8ClampedArray | null = null;
beforeAll(() => {
  HTMLCanvasElement.prototype.getContext = function getContext() {
    let kind = "";
    return {
      drawImage: (img: { kind?: string }) => { kind = img.kind ?? ""; },
      getImageData: (_x: number, _y: number, w: number, h: number) => ({ width: w, height: h, data: kind === "image/png" ? maskPixels() : photoPixels() }),
      createImageData: (w: number, h: number) => ({ width: w, height: h, data: new Uint8ClampedArray(w * h * 4) }),
      putImageData: (img: { data: Uint8ClampedArray }) => { drawn = img.data; },
    } as unknown as CanvasRenderingContext2D;
  } as unknown as typeof HTMLCanvasElement.prototype.getContext;
  HTMLCanvasElement.prototype.toBlob = function toBlob(cb: BlobCallback, type?: string) {
    if (type === "image/jpeg") savedJpeg = drawn && drawn.slice();
    cb(new Blob([type ?? "png"], { type: type ?? "image/png" }));
  };
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
    await waitFor(() => expect(onDone).toHaveBeenCalledWith(expect.any(Blob), "YELLOW", expect.objectContaining({ sourceId: "Cave" })));
    // Handed back to be kept with the boulder: the mask as a PNG and the choices, to go on from next time.
    const kept = onDone.mock.calls[0]![2];
    expect(kept.mask.type).toBe("image/png");
    expect(JSON.parse(kept.settings)).toEqual({ color: "YELLOW", outline: { on: true, color: "red", level: 2 } });
    expect(cave.load).toHaveBeenCalledTimes(1);
  });

  it("outlines the selected holds, follows the switch and the colour, and saves the outline without touching the selection", async () => {
    localStorage.clear();
    const onDone = vi.fn();
    render(<HoldHighlighter sources={[wallOf("Cave")]} initialColor="YELLOW" onCancel={() => {}} onDone={onDone} />);
    await userEvent.click(screen.getByRole("button", { name: "Show the preview" }));
    await screen.findByRole("radio", { name: "Keep colour" });
    // Just outside the yellow half (x 0–19): the red line; inside, the hold's own colour.
    const px = (x: number, y = 10) => Array.from(drawn!.slice((y * W + x) * 4, (y * W + x) * 4 + 3));
    await waitFor(() => expect(px(20)[0]).toBeGreaterThan(180));
    expect(px(20)[1]).toBeLessThan(80);
    expect(px(5)).toEqual([245, 200, 0]);

    const toggle = screen.getByRole("button", { name: "Outline" });
    expect(toggle).toHaveAttribute("aria-pressed", "true");
    await userEvent.click(toggle);
    const off = px(20);
    expect(off[0]).toBe(off[2]); // grey again: the line is gone
    expect(px(5)).toEqual([245, 200, 0]); // and the selection is as it was
    await userEvent.click(toggle);

    await userEvent.click(screen.getByText("Outline colour and width"));
    await userEvent.click(screen.getByRole("radio", { name: "White line" }));
    await waitFor(() => expect(px(20)).toEqual([255, 255, 255]));
    await userEvent.click(screen.getByRole("radio", { name: "Thick" }));
    expect(JSON.parse(localStorage.getItem("bt:hold-outline")!)).toEqual({ on: true, color: "white", level: 3 });

    // The saved picture carries the outline too.
    await userEvent.click(screen.getAllByRole("button", { name: "Use this photo" })[0]!);
    await waitFor(() => expect(onDone).toHaveBeenCalled());
    const saved = (x: number, y = 10) => Array.from(savedJpeg!.slice((y * W + x) * 4, (y * W + x) * 4 + 3));
    expect(saved(20)).toEqual([255, 255, 255]);
    expect(saved(5)).toEqual([245, 200, 0]);
  });

  it("goes on from a saved highlight: its own mask, colour and outline, not a new automatic guess", async () => {
    localStorage.clear();
    const onDone = vi.fn();
    const load = vi.fn(async () => ({ source: new Blob(["photo"], { type: "image/jpeg" }), mask: new Blob(["mask"], { type: "image/png" }) }));
    render(<HoldHighlighter sources={[wallOf("Cave")]} initialColor="" onCancel={() => {}} onDone={onDone}
      resume={{ load, color: "YELLOW", outline: { on: false, color: "red", level: 2 } }} />);

    // Straight to editing, no photo or colour to choose again.
    await screen.findByRole("radio", { name: "Keep colour" });
    expect(screen.queryByRole("button", { name: "Show the preview" })).not.toBeInTheDocument();
    const px = (x: number, y = 10) => Array.from(drawn!.slice((y * W + x) * 4, (y * W + x) * 4 + 3));
    await waitFor(() => expect(px(5)).toEqual([245, 200, 0]));
    const corrected = px(15); // grey: as it was left, though the automatic guess would keep it yellow
    expect(corrected[0]).toBe(corrected[2]);
    expect(screen.getByRole("button", { name: "Outline" })).toHaveAttribute("aria-pressed", "false");

    // "Automatic result" is still there to go back to the guess.
    await userEvent.click(screen.getByRole("button", { name: /Automatic result/ }));
    await waitFor(() => expect(px(15)).toEqual([245, 200, 0]));
    await userEvent.click(screen.getByRole("button", { name: /Undo/ }));
    await waitFor(() => expect(px(15)[0]).toBe(px(15)[2]));

    await userEvent.click(screen.getAllByRole("button", { name: "Use this photo" })[0]!);
    // Made from the same photo as before: no new source to save.
    await waitFor(() => expect(onDone).toHaveBeenCalledWith(expect.any(Blob), "YELLOW", expect.objectContaining({ sourceId: null })));
    expect(load).toHaveBeenCalledTimes(1);
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

  it("takes a whole area away with a tap of the wand, undoes it, says when it finds nothing, and moves the photo on a drag", async () => {
    // jsdom has no PointerEvent and lays nothing out: the stage is 400 × 200, so the 40 × 20 photo is drawn ten times larger.
    class FakePointerEvent extends MouseEvent {
      pointerId: number;
      constructor(type: string, init: MouseEventInit & { pointerId?: number } = {}) { super(type, init); this.pointerId = init.pointerId ?? 1; }
    }
    vi.stubGlobal("PointerEvent", FakePointerEvent);
    vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(400);
    vi.spyOn(HTMLElement.prototype, "clientHeight", "get").mockReturnValue(200);
    try {
      localStorage.clear();
      render(<HoldHighlighter sources={[wallOf("Cave")]} initialColor="YELLOW" onCancel={() => {}} onDone={() => {}} />);
      await userEvent.click(screen.getByRole("button", { name: "Show the preview" }));
      await screen.findByRole("radio", { name: "Keep colour" });
      await userEvent.click(screen.getByRole("button", { name: "Outline" })); // no line, to read the pixels plainly
      const px = (x: number, y = 10) => Array.from(drawn!.slice((y * W + x) * 4, (y * W + x) * 4 + 3));
      await waitFor(() => expect(px(5)).toEqual([245, 200, 0]));

      await userEvent.click(screen.getByRole("radio", { name: "Tap a hold" }));
      expect(screen.getByText(/Tap a grey hold to add it whole/)).toBeInTheDocument();
      const stage = document.querySelector(".highlighter__stage") as HTMLElement;
      const tap = (x: number, y: number) => act(() => {
        fireEvent.pointerDown(stage, { pointerId: 1, clientX: x, clientY: y });
        fireEvent.pointerUp(stage, { pointerId: 1, clientX: x, clientY: y });
      });

      // A tap on the yellow half, selected: all of it goes grey at once.
      tap(50, 100);
      await waitFor(() => expect(px(15)[0]).toBe(px(15)[2]));
      expect(px(2)[0]).toBe(px(2)[2]);
      expect(screen.getByText("Area taken away.")).toBeInTheDocument();
      const undo = screen.getByRole("button", { name: /Undo/ });
      expect(undo).toBeEnabled();
      await userEvent.click(undo);
      await waitFor(() => expect(px(15)).toEqual([245, 200, 0]));
      expect(undo).toBeDisabled();

      // On the grey wall: as wide as half the photo, no hold — nothing changes, nothing to undo.
      tap(300, 100);
      expect(screen.getByText("No hold found there: paint it with the brush.")).toBeInTheDocument();
      expect(px(30)[0]).toBe(px(30)[2]);
      expect(px(5)).toEqual([245, 200, 0]);
      expect(undo).toBeDisabled();

      // A drag is no tap: it leaves the selection alone.
      act(() => {
        fireEvent.pointerDown(stage, { pointerId: 1, clientX: 50, clientY: 100 });
        fireEvent.pointerMove(stage, { pointerId: 1, clientX: 120, clientY: 100 });
        fireEvent.pointerUp(stage, { pointerId: 1, clientX: 120, clientY: 100 });
      });
      expect(px(5)).toEqual([245, 200, 0]);
      expect(undo).toBeDisabled();
    } finally {
      vi.unstubAllGlobals();
      vi.restoreAllMocks();
    }
  });
});
