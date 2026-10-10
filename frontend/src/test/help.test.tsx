import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";
import { HelpPage } from "@/pages/HelpPage";
import { FAQ_EN } from "@/features/help/faq.en";
import { FAQ_IT } from "@/features/help/faq.it";
import { searchFaq } from "@/features/help/faq";

describe("Help and FAQ", () => {
  it("opens on the climbers' guide, and the staff link opens the staff one", async () => {
    renderAt("/aiuto", "/aiuto", <HelpPage />);
    expect(screen.getByRole("radio", { name: "Climbers" })).toHaveAttribute("aria-checked", "true");
    expect(screen.getByText("How do I log a send?")).toBeInTheDocument();
    expect(screen.queryByText("How do I add a boulder?")).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("radio", { name: "Gym staff" }));
    expect(screen.getByText("How do I add a boulder?")).toBeInTheDocument();
  });

  it("starts on the staff guide from the staff area", () => {
    renderAt("/aiuto#staff", "/aiuto", <HelpPage />);
    expect(screen.getByRole("radio", { name: "Gym staff" })).toHaveAttribute("aria-checked", "true");
  });

  it("searches both guides at once and opens what it finds", async () => {
    renderAt("/aiuto", "/aiuto", <HelpPage />);
    await userEvent.type(screen.getByRole("searchbox"), "floor plan");
    const found = screen.getByText("How do I draw the sectors on the floor plan?").closest("details");
    expect(found).toHaveAttribute("open");
    expect(screen.queryByText("How do I log a send?")).not.toBeInTheDocument();
  });

  it("finds words whatever their accents and capitals", () => {
    expect(searchFaq(FAQ_IT.climbers, "PIU VOTATO").flatMap((s) => s.items.map((i) => i.q))).toContain("Come propongo un grado diverso da quello ufficiale?");
  });

  it("asks the same questions in both languages", () => {
    const shape = (g: typeof FAQ_IT) => [...g.climbers, ...g.staff].map((s) => `${s.id}:${s.items.length}`);
    expect(shape(FAQ_EN)).toEqual(shape(FAQ_IT));
  });
});
