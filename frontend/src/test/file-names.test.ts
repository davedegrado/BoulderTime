import { readdirSync } from "node:fs";
import { join, resolve } from "node:path";

/** Every file under a folder, as paths relative to it. */
function walk(root: string, dir = root): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) => {
    const full = join(dir, e.name);
    return e.isDirectory() ? walk(root, full) : [full.slice(root.length + 1)];
  });
}

describe("File names", () => {
  // The iOS build runs on macOS, whose disks don't tell "Friends.tsx" from "friends.ts": two such names build here
  // and fail there, after the build machine has already been paid for.
  it("never differ only in capitals, so the build on macOS sees the same files as this one", () => {
    const src = resolve(__dirname, "..");
    const seen = new Map<string, string>();
    const clashes: string[] = [];
    for (const file of walk(src)) {
      const key = file.replace(/\.(tsx?|jsx?)$/, "").toLowerCase();
      const other = seen.get(key);
      if (other) clashes.push(`${other} ↔ ${file}`);
      else seen.set(key, file);
    }
    expect(clashes).toEqual([]);
  });
});
