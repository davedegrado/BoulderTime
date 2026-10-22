import { useCallback, useEffect, useState, type ReactNode } from "react";
import { Capacitor } from "@capacitor/core";
import { ArrowUpCircle, X } from "lucide-react";
import { api } from "@/lib/api";
import { isNativeApp, loadAppPlugin } from "@/lib/native";
import { storePage, updateVerdict, type AppVersions } from "@/lib/appVersion";
import { Logo } from "@/components/Logo";
import { t } from "@/i18n/i18n";

/** How long the app waits for the answer before carrying on without it. */
const CHECK_TIMEOUT_MS = 6000;
const DISMISSED_KEY = "bt:update-dismissed";

interface Check { verdict: "ok" | "recommended" | "required"; installed: string; recommended: string | null; storeUrl: string; platform: "ios" | "android" }

function readDismissed(): string | null {
  try { return localStorage.getItem(DISMISSED_KEY); } catch { return null; }
}

/**
 * Asks the API which versions of this store app may still be used, when the app starts and every time it comes back
 * to the front (ADR-059). Only in the store apps: the website is always the latest.
 * If the answer doesn't come (no network, the API down, an error), nothing changes: it never locks anyone out.
 */
function useVersionCheck(): Check | null {
  const [check, setCheck] = useState<Check | null>(null);

  const run = useCallback(async () => {
    const platform = Capacitor.getPlatform();
    if (platform !== "ios" && platform !== "android") return;
    const controller = new AbortController();
    const timer = window.setTimeout(() => controller.abort(), CHECK_TIMEOUT_MS);
    try {
      const App = await loadAppPlugin();
      const [{ version }, versions] = await Promise.all([App.getInfo(), api.get<AppVersions>("/api/app-version", { signal: controller.signal, anonymous: true })]);
      const mine = versions?.[platform];
      setCheck({ verdict: updateVerdict(version, mine), installed: version, recommended: mine?.recommended ?? null, storeUrl: storePage(platform, mine), platform });
    } catch {
      // Fail open: keep what the last answer said (nothing, at first).
    } finally {
      window.clearTimeout(timer);
    }
  }, []);

  useEffect(() => {
    if (!isNativeApp()) return;
    void run();
    let remove: (() => void) | undefined;
    let gone = false;
    void loadAppPlugin().then((App) =>
      App.addListener("appStateChange", ({ isActive }) => { if (isActive) void run(); }).then((handle) => {
        if (gone) void handle.remove();
        else remove = () => void handle.remove();
      })).catch(() => {});
    return () => { gone = true; remove?.(); };
  }, [run]);

  return check;
}

/** Shows the app, or, when this version may no longer be used, only the request to update it. */
export function AppVersionGate({ children }: { children: ReactNode }) {
  const check = useVersionCheck();
  const [dismissed, setDismissed] = useState(readDismissed);

  if (check?.verdict === "required") return <UpdateRequired check={check} />;

  const suggest = check?.verdict === "recommended" && check.recommended !== dismissed;
  return (
    <>
      {suggest && (
        <div className="update-banner" role="status">
          <ArrowUpCircle aria-hidden />
          <p>{t("A new version of BoulderTime is available.")}</p>
          <a className="btn btn--primary update-banner__action" href={check.storeUrl} target="_blank" rel="noopener noreferrer">{t("Update")}</a>
          <button type="button" className="icon-btn" aria-label={t("Not now")} onClick={() => {
            try { localStorage.setItem(DISMISSED_KEY, check.recommended ?? ""); } catch { /* the banner just comes back next time */ }
            setDismissed(check.recommended);
          }}><X aria-hidden /></button>
        </div>
      )}
      {children}
    </>
  );
}

function UpdateRequired({ check }: { check: Check }) {
  const store = check.platform === "ios" ? t("Open the App Store") : t("Open Google Play");
  return (
    <main className="update-required" role="alertdialog" aria-modal="true" aria-labelledby="update-title" aria-describedby="update-text">
      <div className="card stack update-required__card">
        <Logo variant="icon" height={64} />
        <h1 id="update-title" className="page__title">{t("Update the app to continue")}</h1>
        <p id="update-text" className="legal__p">{t("This version of BoulderTime can no longer be used. Update it from the store: it takes a moment, and everything you have done stays as it is.")}</p>
        <a className="btn btn--primary btn--block" href={check.storeUrl} target="_blank" rel="noopener noreferrer">{store}</a>
        <p className="field__hint">{t("Installed version: {version}", { version: check.installed })}</p>
      </div>
    </main>
  );
}
