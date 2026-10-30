import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigationType } from "react-router-dom";
import { WifiOff } from "lucide-react";
import { t } from "@/i18n/i18n";

export function useOnlineStatus() {
  const [online, setOnline] = useState(typeof navigator === "undefined" ? true : navigator.onLine);
  useEffect(() => {
    const on = () => setOnline(true);
    const off = () => setOnline(false);
    window.addEventListener("online", on);
    window.addEventListener("offline", off);
    return () => { window.removeEventListener("online", on); window.removeEventListener("offline", off); };
  }, []);
  return online;
}

/** Shown while the phone has no connection — common inside climbing gyms. */
export function OfflineBanner() {
  const online = useOnlineStatus();
  if (online) return null;
  return (
    <div className="offline-banner" role="status">
      <WifiOff aria-hidden /> {t("You're offline. Things you've already opened still show; changes need a connection.")}
    </div>
  );
}

/** Where each screen was scrolled to, by history entry, kept for the tab so a reload comes back to the same place. */
const SCROLL_KEY = "bt:scroll";
const scrollPositions: Record<string, number> = (() => {
  try { return JSON.parse(sessionStorage.getItem(SCROLL_KEY) ?? "{}") as Record<string, number>; } catch { return {}; }
})();
function rememberScroll(key: string, y: number) {
  scrollPositions[key] = y;
  try { sessionStorage.setItem(SCROLL_KEY, JSON.stringify(scrollPositions)); } catch { /* not kept: the screen opens at the top */ }
}

/**
 * Puts a screen back where it was left. Lists may still be drawing (a page of boulders arriving), so it tries again
 * for a moment while the page is shorter than the place to return to, and gives up as soon as the person scrolls.
 */
function restoreScroll(y: number) {
  let tries = 0;
  let stopped = false;
  const stop = () => { stopped = true; };
  window.addEventListener("wheel", stop, { once: true, passive: true });
  window.addEventListener("touchstart", stop, { once: true, passive: true });
  const attempt = () => {
    if (stopped) return;
    window.scrollTo({ top: y });
    const reachable = document.documentElement.scrollHeight - window.innerHeight >= y - 1;
    if (!reachable && tries++ < 60) window.requestAnimationFrame(attempt);
    else { window.removeEventListener("wheel", stop); window.removeEventListener("touchstart", stop); }
  };
  attempt();
}

/**
 * After client-side navigation: updates the document title from the page's heading and moves focus to the main
 * region so screen readers announce the new page. Skipped on first load so autofocus and scroll aren't disturbed.
 *
 * Scrolling: a new screen opens at the top; going back (the app's back arrow, the phone's or the browser's) returns
 * to where that screen was left — the same place in a long list of boulders or in someone's history.
 */
export function RouteAnnouncer() {
  const location = useLocation();
  const navigationType = useNavigationType();
  const first = useRef(true);
  const lastPath = useRef(location.pathname);

  // The browser's own restoring would fight this one, and never knows when a list has finished drawing.
  useEffect(() => {
    if ("scrollRestoration" in window.history) window.history.scrollRestoration = "manual";
  }, []);

  useEffect(() => {
    // The same screen with a new address (a tab, a filter, a sector opened): it is where the page already is, kept
    // now, since no scrolling may follow before the person moves on — and back would find nothing to return to.
    if (navigationType === "REPLACE") rememberScroll(location.key, window.scrollY);
    let frame = 0;
    const save = () => {
      window.cancelAnimationFrame(frame);
      frame = window.requestAnimationFrame(() => rememberScroll(location.key, window.scrollY));
    };
    window.addEventListener("scroll", save, { passive: true });
    return () => { window.removeEventListener("scroll", save); window.cancelAnimationFrame(frame); };
  }, [location.key]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    const changedScreen = lastPath.current !== location.pathname;
    lastPath.current = location.pathname;
    const frame = window.requestAnimationFrame(() => {
      const heading = document.querySelector("main h1, h1")?.textContent?.trim();
      document.title = heading ? `${heading} · BoulderTime` : "BoulderTime";
      const saved = scrollPositions[location.key];
      if (first.current) {
        first.current = false;
        if (navigationType === "POP" && saved) restoreScroll(saved);
        return;
      }
      // A tab or filter kept in the address is the same screen: it stays where it is.
      if (!changedScreen && navigationType !== "POP") return;
      document.getElementById("main")?.focus({ preventScroll: true });
      if (navigationType === "POP" && saved != null) restoreScroll(saved);
      else window.scrollTo({ top: 0 });
    });
    return () => window.cancelAnimationFrame(frame);
  }, [location.key, location.pathname, navigationType]);
  return null;
}
