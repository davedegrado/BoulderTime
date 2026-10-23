import { useState } from "react";
import { act, fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PhotoViewer } from "@/features/boulders/PhotoViewer";
import { usePinchZoom } from "@/features/boulders/usePinchZoom";

// jsdom has no PointerEvent: a mouse event with the finger's id is all the viewers read.
class FakePointerEvent extends MouseEvent {
  pointerId: number;
  constructor(type: string, init: MouseEventInit & { pointerId?: number } = {}) {
    super(type, init);
    this.pointerId = init.pointerId ?? 1;
  }
}

/** The screen the viewer measures: 400 × 800, and a square picture, so it is drawn 400 × 400 in the middle. */
beforeEach(() => {
  vi.stubGlobal("PointerEvent", FakePointerEvent);
  vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(function (this: HTMLElement) {
    return this.classList.contains("zoom-stage")
      ? ({ left: 0, top: 0, width: 400, height: 800, right: 400, bottom: 800, x: 0, y: 0, toJSON() {} } as DOMRect)
      : ({ left: 0, top: 0, width: 0, height: 0, right: 0, bottom: 0, x: 0, y: 0, toJSON() {} } as DOMRect);
  });
});
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); });

const stage = () => document.querySelector(".zoom-stage") as HTMLElement;
const content = () => document.querySelector(".zoom-stage__content") as HTMLElement;
/** The picture's scale and position, read back from its transform. */
const view = () => {
  const m = /translate\((-?[\d.]+)px, (-?[\d.]+)px\) scale\(([\d.]+)\)/.exec(content().style.transform);
  return m ? { x: Number(m[1]), y: Number(m[2]), scale: Number(m[3]) } : null;
};
const finger = (type: "pointerDown" | "pointerMove" | "pointerUp", id: number, x: number, y: number) =>
  act(() => { fireEvent[type](stage(), { pointerId: id, clientX: x, clientY: y }); });

function openPhoto() {
  render(<PhotoViewer src="/b1.jpg" alt="The boulder" onClose={() => {}} />);
  const img = within(screen.getByRole("dialog")).getByRole("img");
  Object.defineProperty(img, "naturalWidth", { value: 1000 });
  Object.defineProperty(img, "naturalHeight", { value: 1000 });
  act(() => { fireEvent.load(img); });
}

describe("A photo full screen", () => {
  it("is shown whole before anything else, fitted in the middle", () => {
    openPhoto();
    expect(view()).toEqual({ x: 0, y: 200, scale: 1 });
  });

  it("zooms with two fingers, following them, and stays where the fingers left it", () => {
    openPhoto();
    finger("pointerDown", 1, 150, 400);
    finger("pointerDown", 2, 250, 400);
    finger("pointerMove", 1, 100, 400);
    finger("pointerMove", 2, 300, 400);
    // The fingers spread twice as far: twice the size, the point between them still between them.
    expect(view()!.scale).toBeCloseTo(2);
    finger("pointerUp", 1, 100, 400);
    finger("pointerUp", 2, 300, 400);
    expect(view()!.scale).toBeCloseTo(2);
    // A pinch is not a tap: lifting the fingers didn't zoom back out.
    expect(screen.getByRole("button", { name: "Zoom out" })).toBeInTheDocument();
  });

  it("goes back to the whole picture when pinched in, never smaller", () => {
    openPhoto();
    finger("pointerDown", 1, 100, 400);
    finger("pointerDown", 2, 300, 400);
    finger("pointerMove", 1, 190, 400);
    finger("pointerMove", 2, 210, 400);
    expect(view()).toEqual({ x: 0, y: 200, scale: 1 });
  });

  it("moves with one finger once enlarged, and not before", () => {
    openPhoto();
    finger("pointerDown", 1, 200, 400);
    finger("pointerMove", 1, 260, 400);
    finger("pointerUp", 1, 260, 400);
    expect(view()).toEqual({ x: 0, y: 200, scale: 1 });

    finger("pointerDown", 1, 150, 400);
    finger("pointerDown", 2, 250, 400);
    finger("pointerMove", 1, 50, 400);
    finger("pointerMove", 2, 350, 400);
    // One finger lifts and the other carries on moving the picture.
    finger("pointerUp", 2, 350, 400);
    const before = view()!;
    finger("pointerMove", 1, 120, 430);
    expect(view()!.x).toBeCloseTo(before.x + 70);
    expect(view()!.y).toBeCloseTo(before.y + 30);
    expect(view()!.scale).toBeCloseTo(before.scale);
  });

  it("zooms in on the spot tapped, and a second tap goes back out", () => {
    openPhoto();
    finger("pointerDown", 1, 100, 300);
    finger("pointerUp", 1, 100, 300);
    expect(view()!.scale).toBeCloseTo(2.5);
    finger("pointerDown", 1, 200, 400);
    finger("pointerUp", 1, 200, 400);
    expect(view()).toEqual({ x: 0, y: 200, scale: 1 });
  });

  it("still zooms with the button, and with the wheel on a computer", async () => {
    openPhoto();
    await userEvent.click(screen.getByRole("button", { name: "Zoom in" }));
    expect(view()!.scale).toBeCloseTo(2.5);
    await userEvent.click(screen.getByRole("button", { name: "Zoom out" }));
    expect(view()!.scale).toBe(1);
    act(() => { stage().dispatchEvent(new WheelEvent("wheel", { deltaY: -300, clientX: 200, clientY: 400, cancelable: true })); });
    expect(view()!.scale).toBeGreaterThan(1.5);
  });
});

/** A plan's viewer: what's under the fingers keeps its taps; a drag or a pinch is never one. */
function Plan({ onSector }: { onSector: () => void }) {
  const zoom = usePinchZoom({ w: 1000, h: 1000 });
  const [, rerender] = useState(0);
  return (
    <div ref={zoom.box} className="zoom-stage" {...zoom.boxProps}>
      <div className="zoom-stage__content" style={zoom.contentStyle}>
        <button type="button" onClick={() => { onSector(); rerender((n) => n + 1); }}>Sector</button>
      </div>
    </div>
  );
}

describe("A plan full screen", () => {
  it("opens a sector on a tap, but not after a drag or a pinch, and zooms on a double tap", () => {
    const onSector = vi.fn();
    render(<Plan onSector={onSector} />);
    const sector = screen.getByRole("button", { name: "Sector" });

    finger("pointerDown", 1, 200, 400);
    finger("pointerUp", 1, 200, 400);
    fireEvent.click(sector);
    expect(onSector).toHaveBeenCalledTimes(1);
    expect(view()!.scale).toBe(1);

    finger("pointerDown", 1, 200, 400);
    finger("pointerMove", 1, 260, 450);
    finger("pointerUp", 1, 260, 450);
    fireEvent.click(sector);
    expect(onSector).toHaveBeenCalledTimes(1);

    finger("pointerDown", 1, 200, 400);
    finger("pointerUp", 1, 200, 400);
    finger("pointerDown", 1, 202, 401);
    finger("pointerUp", 1, 202, 401);
    expect(view()!.scale).toBeCloseTo(2.5);
  });
});
