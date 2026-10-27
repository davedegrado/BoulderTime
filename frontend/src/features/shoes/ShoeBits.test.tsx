import { fireEvent, render, screen } from "@testing-library/react";
import { ShoeRow } from "@/features/shoes/ShoeBits";
import type { CatalogShoe } from "@/features/shoes/api";

const ozone: CatalogShoe = {
  id: "1", brand: "OCÚN", name: "Ozone", status: "CURRENT",
  imageUrl: "https://www.ocun.com/assets/products/1_400x400/ozone.jpg", productUrl: "https://www.ocun.com/product/ozone3",
};
const row = (shoe: CatalogShoe) => render(<ul><ShoeRow shoe={shoe} /></ul>);

describe("the maker's picture of a shoe", () => {
  it("is credited under it with a link to the model's page on the maker's site", () => {
    row(ozone);
    const credit = screen.getByRole("link", { name: /OCÚN.*Ozone/ });
    expect(credit).toHaveAttribute("href", "https://www.ocun.com/product/ozone3");
    expect(credit).toHaveAttribute("target", "_blank");
    expect(credit.textContent).toMatch(/OCÚN/);
  });

  it("is credited without a link when there is no product page, as an older server sends it", () => {
    const { container } = row({ ...ozone, productUrl: undefined });
    expect(screen.queryByRole("link")).toBeNull();
    expect(container.querySelector(".shoe-credit")?.textContent).toMatch(/OCÚN/);
  });

  it("gives way to the brand's initials, without a credit, when there is none or it does not load", () => {
    const { container, unmount } = row({ ...ozone, imageUrl: null });
    expect(container.querySelector(".shoe-thumb--none")?.textContent).toBe("O");
    expect(container.querySelector(".shoe-credit")).toBeNull();
    unmount();

    const broken = row(ozone);
    fireEvent.error(broken.container.querySelector("img")!);
    expect(broken.container.querySelector(".shoe-thumb--none")).not.toBeNull();
    expect(broken.container.querySelector(".shoe-credit")).toBeNull();
  });
});
