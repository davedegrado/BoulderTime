import { readFileSync } from "node:fs";
import { resolveTheme, setThemePreference, startTheme, themePreference } from "@/lib/theme";

describe("Light and dark theme", () => {
  afterEach(() => setThemePreference("system"));

  it("follows the phone unless the person chose", () => {
    expect(resolveTheme("system", true)).toBe("dark");
    expect(resolveTheme("system", false)).toBe("light");
    expect(resolveTheme("light", true)).toBe("light");
    expect(resolveTheme("dark", false)).toBe("dark");
  });

  it("puts the choice on the page and remembers it on this device", () => {
    document.head.insertAdjacentHTML("beforeend", '<meta name="theme-color" content="#1A1A1A">');
    startTheme();
    setThemePreference("dark");
    expect(document.documentElement.dataset.theme).toBe("dark");
    expect(localStorage.getItem("bt:theme")).toBe("dark");
    expect(document.querySelector('meta[name="theme-color"]')?.getAttribute("content")).toBe("#0F1012");

    setThemePreference("system");
    expect(themePreference()).toBe("system");
    expect(localStorage.getItem("bt:theme")).toBeNull();
  });

  it("gives every colour the page relies on a dark value", () => {
    const tokens = readFileSync("src/styles/tokens.css", "utf8");
    const dark = tokens.slice(tokens.indexOf(':root[data-theme="dark"]'));
    for (const name of ["--bt-light", "--bt-ink", "--bt-ink-2", "--bt-ink-3", "--bt-line", "--bt-surface", "--bt-surface-2", "--bt-orange-ink", "--bt-orange-tint", "--bt-strong", "--bt-on-strong", "--bt-deep", "--bt-chrome"]) {
      expect(dark, name).toContain(`${name}:`);
    }
  });
});
