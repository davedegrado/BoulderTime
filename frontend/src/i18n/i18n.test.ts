import { activeLanguage, plural, t, translate } from "@/i18n/i18n";

describe("translations", () => {
  it("translates into Italian and leaves English alone", () => {
    expect(translate("it", "Your progress")).toBe("I tuoi progressi");
    expect(translate("en", "Your progress")).toBe("Your progress");
  });

  it("fills in values and picks singular or plural", () => {
    expect(translate("it", "{count} gyms", { count: 3 })).toContain("3");
    expect(plural(1, "{count} boulder", "{count} boulders")).toBe("1 boulder");
    expect(plural(4, "{count} boulder", "{count} boulders")).toBe("4 boulders");
  });

  it("falls back to the English source when a translation is missing", () => {
    expect(translate("it", "A string nobody translated")).toBe("A string nobody translated");
  });

  it("uses the browser language in tests (English), so screens assert English text", () => {
    expect(activeLanguage()).toBe("en");
    expect(t("Your progress")).toBe("Your progress");
  });
});
