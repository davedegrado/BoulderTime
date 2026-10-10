import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ThumbnailCropper } from "@/features/boulders/ThumbnailCropper";

// jsdom lays nothing out: give the frame a size, and the photo its own once it "loads".
beforeEach(() => {
  vi.spyOn(HTMLElement.prototype, "clientWidth", "get").mockReturnValue(500);
  vi.spyOn(HTMLElement.prototype, "clientHeight", "get").mockReturnValue(400);
});
afterEach(() => vi.restoreAllMocks());

function load(img: HTMLImageElement, w: number, h: number) {
  Object.defineProperty(img, "naturalWidth", { configurable: true, value: w });
  Object.defineProperty(img, "naturalHeight", { configurable: true, value: h });
  fireEvent.load(img);
}

describe("Choosing the card picture", () => {
  it("starts from the middle of the photo, filling the card's shape", async () => {
    const onDone = vi.fn();
    const { container } = render(<ThumbnailCropper src="/p.jpg" onCancel={() => {}} onDone={onDone} />);
    load(container.querySelector("img")!, 1000, 2000); // a tall photo in a 5:4 frame

    await userEvent.click(screen.getAllByRole("button", { name: "Use this part" })[0]!);
    // The whole width, and a 5:4 slice from the middle of the height.
    expect(onDone).toHaveBeenCalledWith({ x: 0, y: 600, width: 1000, height: 800 });
  });

  it("moves with the arrow keys but never leaves an empty edge", async () => {
    const onDone = vi.fn();
    const { container } = render(<ThumbnailCropper src="/p.jpg" onCancel={() => {}} onDone={onDone} />);
    load(container.querySelector("img")!, 1000, 2000);

    const frame = screen.getByLabelText("Drag to choose what the cards show");
    frame.focus();
    for (let i = 0; i < 100; i++) await userEvent.keyboard("{ArrowUp}"); // far past the top of the photo
    await userEvent.click(screen.getAllByRole("button", { name: "Use this part" })[0]!);
    expect(onDone).toHaveBeenCalledWith({ x: 0, y: 0, width: 1000, height: 800 });
  });

  it("zooms in around the middle", async () => {
    const onDone = vi.fn();
    const { container } = render(<ThumbnailCropper src="/p.jpg" onCancel={() => {}} onDone={onDone} />);
    load(container.querySelector("img")!, 1000, 2000);

    fireEvent.change(screen.getByRole("slider", { name: "Zoom" }), { target: { value: "2" } });
    await userEvent.click(screen.getAllByRole("button", { name: "Use this part" })[0]!);
    expect(onDone).toHaveBeenCalledWith({ x: 250, y: 800, width: 500, height: 400 });
  });
});
