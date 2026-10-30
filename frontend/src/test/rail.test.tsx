import { fireEvent, render } from "@testing-library/react";
import { Rail } from "@/components/Rail";

function size(el: HTMLElement, { scrollLeft, clientWidth, scrollWidth }: { scrollLeft: number; clientWidth: number; scrollWidth: number }) {
  Object.defineProperty(el, "scrollLeft", { configurable: true, value: scrollLeft });
  Object.defineProperty(el, "clientWidth", { configurable: true, value: clientWidth });
  Object.defineProperty(el, "scrollWidth", { configurable: true, value: scrollWidth });
}

describe("A row of cards", () => {
  it("fades the side with more to see, and neither once it is all in view", () => {
    const { container } = render(<Rail><div>a</div><div>b</div><div>c</div></Rail>);
    const rail = container.querySelector(".rail") as HTMLElement;
    const fades = () => [...container.querySelectorAll(".rail-wrap__fade")].map((f) => f.className.includes("--start") ? "start" : "end");
    // The row itself is never masked (older Safari stops painting a masked row while it scrolls): fades lie over it.
    expect(rail.style.maskImage ?? "").toBe("");

    size(rail, { scrollLeft: 0, clientWidth: 300, scrollWidth: 900 });
    fireEvent.scroll(rail);
    expect(fades()).toEqual(["end"]);

    size(rail, { scrollLeft: 300, clientWidth: 300, scrollWidth: 900 });
    fireEvent.scroll(rail);
    expect(fades()).toEqual(["start", "end"]);

    // At the end the last card is whole: no fade on the right.
    size(rail, { scrollLeft: 600, clientWidth: 300, scrollWidth: 900 });
    fireEvent.scroll(rail);
    expect(fades()).toEqual(["start"]);
  });
});
