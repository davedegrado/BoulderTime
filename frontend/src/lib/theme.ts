import { useSyncExternalStore } from "react";
import { Capacitor } from "@capacitor/core";

/** What the person picked in Profile. "system" follows the phone or computer, and is the default. */
export type ThemePreference = "system" | "light" | "dark";
export type Theme = "light" | "dark";

const STORAGE_KEY = "bt:theme";
/** The page colour of each theme: the browser's address bar and Android's status bar take it on. */
const PAGE_COLOUR: Record<Theme, string> = { light: "#F8F8F8", dark: "#0F1012" };

const listeners = new Set<() => void>();
let preference: ThemePreference = readPreference();

function readPreference(): ThemePreference {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    return stored === "light" || stored === "dark" ? stored : "system";
  } catch {
    return "system";
  }
}

const systemQuery = () => (typeof window !== "undefined" && window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null);

export function resolveTheme(pref: ThemePreference, systemDark: boolean): Theme {
  return pref === "system" ? (systemDark ? "dark" : "light") : pref;
}

export const currentTheme = (): Theme => resolveTheme(preference, systemQuery()?.matches ?? false);
export const themePreference = () => preference;

/** Puts the theme on the page: data-theme on <html> (the CSS reads it), the browser's bar colour, the status bar. */
function apply() {
  const theme = currentTheme();
  document.documentElement.dataset.theme = theme;
  document.querySelector('meta[name="theme-color"]')?.setAttribute("content", PAGE_COLOUR[theme]);
  if (statusBarReady) void paintStatusBar(theme);
  listeners.forEach((l) => l());
}

export function setThemePreference(next: ThemePreference) {
  preference = next;
  try {
    if (next === "system") localStorage.removeItem(STORAGE_KEY);
    else localStorage.setItem(STORAGE_KEY, next);
  } catch {
    // Without storage the choice holds until the app is closed.
  }
  apply();
}

/** Called once before the first render, so the page never flashes in the wrong theme. */
export function startTheme() {
  apply();
  // "Automatico" follows the phone when it switches (at sunset, for instance) while the app is open.
  systemQuery()?.addEventListener?.("change", () => { if (preference === "system") apply(); });
}

/** The theme in use, re-rendering when it changes. */
export function useTheme(): { theme: Theme; preference: ThemePreference } {
  const theme = useSyncExternalStore(subscribe, currentTheme, () => "light" as Theme);
  const pref = useSyncExternalStore(subscribe, themePreference, () => "system" as ThemePreference);
  return { theme, preference: pref };
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/**
 * The store apps' status bar. It stays as the launch screen left it (light text on dark) until the splash is gone;
 * from then on it follows the theme: dark text on the light theme, light text on the dark one.
 */
let statusBarReady = false;
export async function startStatusBar() {
  statusBarReady = true;
  await paintStatusBar(currentTheme());
}

async function paintStatusBar(theme: Theme) {
  if (!Capacitor.isNativePlatform()) return;
  try {
    const { StatusBar, Style } = await import("@capacitor/status-bar");
    // Style names the background the text sits on: Light = dark text, Dark = light text.
    await StatusBar.setStyle({ style: theme === "dark" ? Style.Dark : Style.Light });
    if (Capacitor.getPlatform() === "android") {
      await StatusBar.setOverlaysWebView({ overlay: false });
      await StatusBar.setBackgroundColor({ color: PAGE_COLOUR[theme] });
    }
  } catch {
    // A status bar that keeps its look is not worth failing over.
  }
}
