import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "@/styles/tokens.css";
import "@/styles/global.css";
import { isNativeApp, startNativeShell } from "@/lib/native";
import { t } from "@/i18n/i18n";
import { startTheme } from "@/lib/theme";

// Before anything renders, so the page never shows in the wrong theme first.
startTheme();

const root = createRoot(document.getElementById("root")!);

// Service worker (production builds only): offline app shell and image cache, updated automatically.
// Not in the store apps: their files are already inside the app, and a service worker there would keep serving the
// previous version after an update.
if (import.meta.env.PROD && "serviceWorker" in navigator && !isNativeApp()) {
  import("virtual:pwa-register").then(({ registerSW }) => registerSW({ immediate: true })).catch(() => {});
}

// Import lazily so a missing env var renders a readable message instead of a blank page.
import("@/app/App")
  .then(({ App }) => {
    root.render(<StrictMode><App /></StrictMode>);
    void startNativeShell();
  })
  .catch((err: Error) => {
    root.render(
      <div className="state state--error" role="alert">
        <h1 className="state__title">{t("BoulderTime couldn't start")}</h1>
        <p className="state__text">{err.message}</p>
      </div>,
    );
  });
