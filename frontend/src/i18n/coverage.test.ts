import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { it as italian } from "@/i18n/it";

/** Every English source string used in the app must have Italian wording (missing ones would show English). */
function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) return sourceFiles(path);
    return /\.tsx?$/.test(entry) && !/\.test\./.test(entry) ? [path] : [];
  });
}

describe("Italian coverage", () => {
  it("has wording for every translated string", () => {
    const keys = new Set<string>();
    for (const file of sourceFiles("src")) {
      if (file.includes("i18n")) continue;
      const source = readFileSync(file, "utf8");
      for (const match of source.matchAll(/(?<![A-Za-z0-9_$.])(?:t|plural)\(\s*"((?:[^"\\\\]|\\\\.)*)"(?:\s*,\s*"((?:[^"\\\\]|\\\\.)*)")?/g)) {
        keys.add(match[1]!);
        if (match[2]) keys.add(match[2]);
      }
    }
    expect(keys.size).toBeGreaterThan(300);
    const missing = [...keys].filter((key) => italian[key] === undefined);
    expect(missing, `missing Italian for: ${missing.join(" | ")}`).toEqual([]);
  });

  it("has no user-visible text built with a template literal", () => {
    // Text assembled as `Back to ${gym.name}` never reaches the dictionary, so it silently stays English —
    // including aria-labels, which screen readers read aloud.
    const props = /(title|body|label|hint|placeholder|aria-label|confirmLabel|submitLabel|alt)=\{`([^`]*)`\}/g;
    const offenders: string[] = [];
    for (const file of sourceFiles("src")) {
      if (file.includes("i18n")) continue;
      const source = readFileSync(file, "utf8");
      for (const match of source.matchAll(props)) {
        const withoutValues = match[2]!.replace(/\$\{[^}]*\}/g, "");
        if (/[A-Za-z]{3,}/.test(withoutValues)) offenders.push(`${file}: ${match[0]}`);
      }
    }
    expect(offenders, `translate these: ${offenders.join(" | ")}`).toEqual([]);
  });
});
