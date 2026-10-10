import { Capacitor } from "@capacitor/core";

/**
 * The one place that knows whether BoulderTime is running as a store app or in a browser. Everything native is
 * reached through here, so the website and the PWA never load a plugin they don't need.
 */
export const isNativeApp = (): boolean => Capacitor.isNativePlatform();

/**
 * Links that leave the app (email confirmations, password resets) must point at the website, not at the app's own
 * internal address: inside the app `window.location.origin` is https://localhost (Android) or capacitor://localhost
 * (iOS), which a mail client cannot open.
 */
export const PUBLIC_ORIGIN = "https://bouldertime.com";

export function publicOrigin(): string {
  return isNativeApp() ? PUBLIC_ORIGIN : window.location.origin;
}

const OUR_HOSTS = new Set(["bouldertime.com", "www.bouldertime.com"]);

/**
 * The in-app path for a link the phone handed to the app, or null when the link isn't ours.
 * Only bouldertime.com links are followed: anything else reaching the app is ignored rather than trusted.
 */
export function appPathFromUrl(url: string): string | null {
  let parsed: URL;
  try { parsed = new URL(url); } catch { return null; }
  if (parsed.protocol !== "https:" || !OUR_HOSTS.has(parsed.hostname)) return null;
  return `${parsed.pathname || "/"}${parsed.search}${parsed.hash}`;
}

/**
 * Wires the few native behaviours the app needs:
 * - Android's back button walks back through the app instead of closing it, and only exits from the first screen
 *   (iOS has no back button: the event simply never fires there);
 * - bouldertime.com links opened on the phone go to the matching page in the app;
 * - tapping a notification opens the page it is about;
 * - the status bar uses dark text on the app's light header;
 * - the splash screen stays until React has drawn the first screen, so there is no blank flash in between;
 *   it also stays at least two seconds (SPLASH_MIN_MS), so it is seen rather than flashed. The status bar keeps light
 *   text over it and switches to dark text with the first screen.
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

  // A bouldertime.com link opened on the phone (an email, a shared boulder) lands on the same page inside the app.
  // Android calls these App Links, iOS universal links; Capacitor delivers both through the same event.
  // The app may be cold-started by the link (getLaunchUrl) or already running (appUrlOpen); the same link is never
  // followed twice, because email links carry one-time tokens.
  let lastLink = "";
  const follow = (url: string | undefined) => {
    if (!url || url === lastLink) return;
    const path = appPathFromUrl(url);
    if (!path) return;
    lastLink = url;
    void import("@/app/router").then(({ router }) => router.navigate(path));
  };
  App.addListener("appUrlOpen", ({ url }) => follow(url));

  // Tapping a notification opens the page it is about. The url travels in the message's data, as the sender puts it.
  // Only when this build carries its Firebase settings: without them the plugin throws, and Capacitor turns that into
  // a crash. Everything after this point still has to run, so it is a branch and never an early return.
  if (import.meta.env.VITE_PUSH_NATIVE === "1") {
    try {
      const { PushNotifications } = await import("@capacitor/push-notifications");
      await PushNotifications.addListener("pushNotificationActionPerformed", ({ notification }) => {
        const url = notification.data?.url;
        if (typeof url !== "string" || url.length === 0) return;
        const path = url.startsWith("/") ? url : appPathFromUrl(url);
        if (path) void import("@/app/router").then(({ router }) => router.navigate(path));
      });
    } catch {
      // Notifications are a bonus here; failing to listen must not stop the app from starting.
    }
  }
  void App.getLaunchUrl().then((launch) => follow(launch?.url)).catch(() => {});

  // Notifications turned on earlier on this phone: refresh the token quietly, so they keep arriving (and the switch in
  // the settings knows they are on).
  if (import.meta.env.VITE_PUSH_NATIVE === "1") {
    void import("@/features/notifications/push").then(({ syncNativePush }) => syncNativePush()).catch(() => {});
  }

  // Give React a frame to paint the first screen before revealing it, and keep the launch screen up for at least
  // SPLASH_MIN_MS from the start, so it is seen rather than flashed. The status bar turns to dark text on the light
  // header only now: until here it sat on the dark launch screen.
  requestAnimationFrame(() => {
    window.setTimeout(() => {
      void SplashScreen.hide({ fadeOutDuration: 400 });
      void lightStatusBar(StatusBar, Style);
    }, splashDelay(performance.now()));
  });
}

/** How long the launch screen stays at least, counted from the moment the app's page started loading. */
export const SPLASH_MIN_MS = 2000;

/** What is left of that time, given how long the app has been starting: never negative, never more than the minimum. */
export const splashDelay = (elapsedMs: number) => Math.min(SPLASH_MIN_MS, Math.max(0, SPLASH_MIN_MS - elapsedMs));

async function lightStatusBar(
  StatusBar: typeof import("@capacitor/status-bar").StatusBar,
  Style: typeof import("@capacitor/status-bar").Style,
): Promise<void> {
  try {
    await StatusBar.setStyle({ style: Style.Light });
    if (Capacitor.getPlatform() === "android") {
      await StatusBar.setOverlaysWebView({ overlay: false });
      await StatusBar.setBackgroundColor({ color: "#F8F8F7" });
    }
  } catch {
    // A status bar that keeps its default look is not worth failing the start for.
  }
}
