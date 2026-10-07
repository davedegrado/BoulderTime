import { execFileSync } from "node:child_process";
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { it as italian } from "@/i18n/it";

// vitest runs from the frontend directory, so the sources are a known hop away.
const ROOT = process.cwd();
const SRC = join(ROOT, "src");

/**
 * Italian is the language every climber in these gyms reads, so English reaching the screen is a bug, not a
 * cosmetic detail. Thirty-eight strings had already shipped untranslated before these two tests existed.
 */
describe("every string a person reads goes through t()", () => {
  it("finds no English written straight into the screen", () => {
    try {
      execFileSync("node", [join(ROOT, "scripts", "find-untranslated.mjs")], { encoding: "utf8" });
    } catch (e) {
      const out = (e as { stdout?: string }).stdout ?? "";
      expect.fail(`Wrap these in t() and add the Italian to src/i18n/it.ts:\n${out}`);
    }
  });

  it("finds no t(\"…\") without an Italian translation", () => {
    const files: string[] = [];
    const walk = (dir: string) => {
      for (const name of readdirSync(dir)) {
        const p = join(dir, name);
        if (statSync(p).isDirectory()) { if (name !== "test") walk(p); }
        else if (/\.tsx?$/.test(name) && !/\.test\.tsx?$/.test(name)) files.push(p);
      }
    };
    walk(SRC);

    const missing = new Set<string>();
    for (const file of files) {
      const source = readFileSync(file, "utf8");
      // t("…") and plural(n, "one", "many") — the two ways copy enters the app.
      for (const m of source.matchAll(/(?<![\w.])t\(\s*"((?:[^"\\]|\\.)*)"/g)) add(m[1]!);
      for (const m of source.matchAll(/plural\([^,]+,\s*"((?:[^"\\]|\\.)*)"\s*,\s*"((?:[^"\\]|\\.)*)"/g)) { add(m[1]!); add(m[2]!); }
    }
    function add(key: string) {
      const text = key.replace(/\\"/g, '"');
      if (!(text in italian)) missing.add(text);
    }

    expect([...missing].sort(), "add these to src/i18n/it.ts").toEqual([]);
  });
});
