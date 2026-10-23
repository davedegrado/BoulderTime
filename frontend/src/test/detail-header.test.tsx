import { useRef } from "react";
import { RouterProvider, createMemoryRouter } from "react-router-dom";
import { act, render, screen } from "@testing-library/react";

const setStatusBarOverCover = vi.fn();
vi.mock("@/lib/theme", () => ({ setStatusBarOverCover: (on: boolean) => setStatusBarOverCover(on) }));

import { DetailHeader } from "@/components/DetailHeader";

/** A stand-in for the browser's observer: the test says when an element has gone above the header's bottom edge. */
type Callback = (entries: Partial<IntersectionObserverEntry>[]) => void;
const observers: { callback: Callback; margin?: string; targets: Element[] }[] = [];
class FakeObserver {
  targets: Element[] = [];
  constructor(public callback: Callback, options?: IntersectionObserverInit) { observers.push({ callback, margin: options?.rootMargin, targets: this.targets }); }
  observe(el: Element) { this.targets.push(el); }
  disconnect() { this.targets.length = 0; }
  unobserve() {}
  takeRecords() { return []; }
}
const scrolled = (el: Element, gone: boolean) => act(() => {
  for (const o of observers) if (o.targets.includes(el)) {
    o.callback([{ target: el, isIntersecting: !gone, rootBounds: { top: 52 } as DOMRectReadOnly, boundingClientRect: { bottom: gone ? 20 : 300 } as DOMRectReadOnly }]);
  }
});

function Page() {
  const cover = useRef<HTMLDivElement>(null);
  const name = useRef<HTMLHeadingElement>(null);
  return (
    <>
      <DetailHeader title="Rock'n Fire" coverRef={cover} titleRef={name} menu={<button>More actions</button>} />
      <div ref={cover} data-testid="cover" />
      <h1 ref={name}>Rock'n Fire</h1>
    </>
  );
}
const show = (element: JSX.Element) =>
  render(<RouterProvider router={createMemoryRouter([{ path: "/g", element }, { path: "/", element: <h1>Home</h1> }], { initialEntries: ["/g"] })} />);
const header = () => document.querySelector(".detail-header")!;

beforeEach(() => {
  observers.length = 0;
  setStatusBarOverCover.mockClear();
  vi.stubGlobal("IntersectionObserver", FakeObserver);
});
afterEach(() => vi.unstubAllGlobals());

describe("A gym's or a person's header", () => {
  it("has back and the menu, and no logo", () => {
    show(<Page />);
    expect(screen.getByRole("button", { name: "Back" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "More actions" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /BoulderTime/ })).not.toBeInTheDocument();
    expect(header().querySelector("img, svg[aria-label*='BoulderTime']")).toBeNull();
  });

  it("is transparent over the cover, solid once it's gone, and names the page once its name is gone too", () => {
    show(<Page />);
    const cover = screen.getByTestId("cover");
    const name = screen.getByRole("heading", { name: "Rock'n Fire" });
    // The line is the header's own bottom edge, measured, not a number picked for one phone.
    expect(observers.at(-1)!.margin).toMatch(/^-\d+px 0px 0px 0px$/);
    expect(header()).toHaveClass("detail-header--over");
    expect(header()).not.toHaveClass("is-solid");
    expect(setStatusBarOverCover).toHaveBeenLastCalledWith(true);

    scrolled(cover, true);
    expect(header()).toHaveClass("is-solid");
    expect(header()).not.toHaveClass("has-title");
    expect(setStatusBarOverCover).toHaveBeenLastCalledWith(false);

    scrolled(name, true);
    expect(header()).toHaveClass("has-title");
    expect(header().querySelector(".detail-header__title")).toHaveTextContent("Rock'n Fire");
    // The echo is for the eyes: the page's heading is still its only name for a screen reader.
    expect(screen.getAllByRole("heading", { name: "Rock'n Fire" })).toHaveLength(1);

    // Back to the top: transparent again, and the name leaves the header.
    scrolled(name, false);
    scrolled(cover, false);
    expect(header()).not.toHaveClass("is-solid");
    expect(header()).not.toHaveClass("has-title");
  });

  it("is solid from the start where there is no cover (loading, an error), and gives the status bar back when it goes", () => {
    const { unmount } = show(<DetailHeader />);
    expect(header()).toHaveClass("is-solid");
    expect(header()).not.toHaveClass("detail-header--over");
    expect(setStatusBarOverCover).not.toHaveBeenCalledWith(true);
    unmount();
    expect(setStatusBarOverCover).toHaveBeenLastCalledWith(false);
  });
});
