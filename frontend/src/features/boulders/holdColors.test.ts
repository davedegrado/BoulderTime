import { holdLabel, inkOn } from "@/features/boulders/holdColors";

describe("hold colours", () => {
  it("always describes holds explicitly", () => {
    expect(holdLabel("BLUE")).toBe("Blue holds");
  });
  it("picks readable text on grade colours", () => {
    expect(inkOn("#F5C400")).toBe("#1A1A1A");
    expect(inkOn("#1A1A1A")).toBe("#FFFFFF");
  });
});
