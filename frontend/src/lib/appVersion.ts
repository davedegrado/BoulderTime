/**
 * Versions of the store apps, compared part by part as numbers: 1.10.0 is newer than 1.9.0, and a missing part counts
 * as 0 (1.2 is 1.2.0). The same rules as the API's (ADR-059).
 */

/** The three numbers of a version of the form 1, 1.2 or 1.2.3; null for anything else. */
export function parseVersion(version: string | null | undefined): [number, number, number] | null {
  const parts = (version ?? "").trim().split(".");
  if (parts.length < 1 || parts.length > 3 || parts.some((p) => !/^\d{1,5}$/.test(p))) return null;
  const n = parts.map(Number);
  return [n[0] ?? 0, n[1] ?? 0, n[2] ?? 0];
}

/** Negative when a is older than b, positive when newer, 0 when the same; null when either isn't a version. */
export function compareVersions(a: string, b: string): number | null {
  const x = parseVersion(a), y = parseVersion(b);
  if (!x || !y) return null;
  for (let i = 0; i < 3; i++) if (x[i] !== y[i]) return x[i]! - y[i]!;
  return 0;
}

export interface PlatformVersions { minimum: string; recommended: string | null; storeUrl: string | null }
export interface AppVersions { ios: PlatformVersions; android: PlatformVersions }

/**
 * What the installed app should do: carry on, suggest updating, or ask for it before anything else.
 * Anything it can't read (no answer, a version it doesn't understand) means carry on: a problem on our side must never
 * lock people out.
 */
export function updateVerdict(installed: string | null | undefined, versions: PlatformVersions | null | undefined): "ok" | "recommended" | "required" {
  if (!installed || !versions) return "ok";
  if ((compareVersions(installed, versions.minimum) ?? 0) < 0) return "required";
  if (versions.recommended && (compareVersions(installed, versions.recommended) ?? 0) < 0) return "recommended";
  return "ok";
}

/** The app's page on its store, or a search for it there when none is set yet. */
export function storePage(platform: "ios" | "android", versions: PlatformVersions | null | undefined): string {
  if (versions?.storeUrl) return versions.storeUrl;
  return platform === "ios"
    ? "https://apps.apple.com/search?term=BoulderTime"
    : "https://play.google.com/store/apps/details?id=com.bouldertime.app";
}
