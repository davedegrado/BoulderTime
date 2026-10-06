import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "@/styles/tokens.css";
import "@/styles/global.css";
import { isNativeApp, startNativeShell } from "@/lib/native";

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
        <h1 className="state__title">BoulderTime couldn't start</h1>
        <p className="state__text">{err.message}</p>
      </div>,
    );
  });
