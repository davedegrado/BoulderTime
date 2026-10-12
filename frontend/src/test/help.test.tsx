import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, resolve } from "node:path";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderAt } from "@/test/renderApp";
import { forSettings, searchFaq, type FaqGuide } from "@/features/help/faq";

// The guide lives on the server now; these are its files, read as the server reads them.
const helpDir = resolve(__dirname, "../../../backend/src/BoulderTime.Application/Help");
const FAQ_IT = JSON.parse(readFileSync(join(helpDir, "faq-it.json"), "utf8")) as FaqGuide;
const FAQ_EN = JSON.parse(readFileSync(join(helpDir, "faq-en.json"), "utf8")) as FaqGuide;

let served: FaqGuide = FAQ_EN;
const calls: string[] = [];
vi.mock("@/lib/api", () => ({
  api: { get: async (path: string) => { calls.push(path); return path.startsWith("/api/help/faq") ? served : { uploadsEnabled: false, maxUploadMegabytes: 50 }; } },
}));

import { HelpPage } from "@/pages/HelpPage";

beforeEach(() => { served = FAQ_EN; calls.length = 0; });

describe("Help and FAQ", () => {
  it("opens on the climbers' guide, and staff can switch to theirs", async () => {
    renderAt("/aiuto", "/aiuto", <HelpPage />);
    expect(await screen.findByText("How do I log a send?")).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Climbers" })).toHaveAttribute("aria-checked", "true");
    expect(screen.queryByText("How do I add a boulder?")).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("radio", { name: "Gym staff" }));
    expect(screen.getByText("How do I add a boulder?")).toBeInTheDocument();
    expect(calls).toContain("/api/help/faq?lang=en");
  });

  it("shows no staff guide to someone the server gives none, even from a staff link", async () => {
    served = { ...FAQ_EN, staff: [] };
    renderAt("/aiuto#staff", "/aiuto", <HelpPage />);
    expect(await screen.findByText("How do I log a send?")).toBeInTheDocument();
    expect(screen.queryByRole("radio", { name: "Gym staff" })).not.toBeInTheDocument();
    expect(screen.queryByText("How do I add a boulder?")).not.toBeInTheDocument();
  });

  it("starts on the staff guide from the staff area", async () => {
    renderAt("/aiuto#staff", "/aiuto", <HelpPage />);
    expect(await screen.findByRole("radio", { name: "Gym staff" })).toHaveAttribute("aria-checked", "true");
  });

  it("searches what the person can read and opens what it finds", async () => {
    renderAt("/aiuto", "/aiuto", <HelpPage />);
    await screen.findByText("How do I log a send?");
    await userEvent.type(screen.getByRole("searchbox"), "floor plan");
    const found = screen.getByText("How do I draw the sectors on the floor plan?").closest("details");
    expect(found).toHaveAttribute("open");
    expect(screen.queryByText("How do I log a send?")).not.toBeInTheDocument();
  });

  it("finds words whatever their accents and capitals", () => {
    expect(searchFaq(FAQ_IT.climbers, "PIU VOTATO").flatMap((s) => s.items.map((i) => i.q))).toContain("Come propongo un grado diverso da quello ufficiale?");
  });

  it("asks the same questions in both languages", () => {
    const shape = (g: FaqGuide) => [...g.climbers, ...g.staff].map((s) => `${s.id}:${s.items.length}`);
    expect(shape(FAQ_EN)).toEqual(shape(FAQ_IT));
  });

  it("describes video uploads only when the server has them on, with the server's limit", () => {
    const questions = (g: FaqGuide) => [...g.climbers, ...g.staff].flatMap((sec) => sec.items);
    const off = questions(forSettings(FAQ_EN, { uploadsEnabled: false, maxUploadMegabytes: 50 }));
    expect(off.map((i) => i.q)).not.toContain("How do I upload a video?");
    expect(off.filter((i) => i.q === "How do I add the official beta?")).toHaveLength(1);
    expect(off.find((i) => i.q === "How do I add the official beta?")!.a).not.toMatch(/upload/i);

    const on = questions(forSettings(FAQ_IT, { uploadsEnabled: true, maxUploadMegabytes: 200 }));
    expect(on.find((i) => i.q === "Come carico un mio video?")!.a).toContain("fino a 200 MB");
    expect(on.every((i) => !i.a.includes("{maxVideoMb}"))).toBe(true);
  });

  it("keeps the staff answers out of the app's code", () => {
    const files: string[] = [];
    const walk = (dir: string) => readdirSync(dir).forEach((name) => {
      const path = join(dir, name);
      if (statSync(path).isDirectory()) { if (name !== "test") walk(path); } else if (/\.(ts|tsx)$/.test(name)) files.push(path);
    });
    walk(resolve(__dirname, ".."));
    const source = files.map((f) => readFileSync(f, "utf8")).join("\n");
    const staffAnswers = [...FAQ_IT.staff, ...FAQ_EN.staff].flatMap((s) => s.items.map((i) => i.a.slice(0, 60)));
    expect(staffAnswers.filter((a) => source.includes(a))).toEqual([]);
  });
});
