import { Capacitor } from "@capacitor/core";

/**
 * The one place that knows whether BoulderTime is running as a store app or in a browser. Everything native is
 * reached through here, so the website and the PWA never load a plugin they don't need.
 */
export const isNativeApp = (): boolean => Capacitor.isNativePlatform();

/**
 * Links that leave the app (email confirmations, password resets) must point at the website, not at the app's own
 * internal address: inside the app `window.location.origin` is https://localhost, which a mail client cannot open.
 */
export const PUBLIC_ORIGIN = "https://bouldertime.com";

export function publicOrigin(): string {
  return isNativeApp() ? PUBLIC_ORIGIN : window.location.origin;
}

/**
 * Wires the few native behaviours the app needs from its first version:
 * - Android's back button walks back through the app instead of closing it, and only exits from the first screen;
 * - the status bar uses dark text on the app's light header;
 * - the splash screen stays until React has drawn the first screen, so there is no blank flash in between.
 * Plugins are imported here, on demand, so the web build never ships them.
 */
export async function startNativeShell(): Promise<void> {
  if (!isNativeApp()) return;

  const [{ App }, { StatusBar, Style }, { SplashScreen }] = await Promise.all([
    import("@capacitor/app"),
    import("@capacitor/status-bar"),
    import("@capacitor/splash-screen"),
  ]);

  App.addListener("backButton", ({ canGoBack }) => {
    if (canGoBack) window.history.back();
    else App.exitApp();
  });

  try {
    await StatusBar.setStyle({ style: Style.Light });
    if (Capacitor.getPlatform() === "android") {
      await StatusBar.setOverlaysWebView({ overlay: false });
      await StatusBar.setBackgroundColor({ color: "#F8F8F7" });
    }
  } catch {
    // A status bar that keeps its default look is not worth failing the start for.
  }

  // Give React a frame to paint the first screen before revealing it.
  requestAnimationFrame(() => { void SplashScreen.hide({ fadeOutDuration: 200 }); });
}
