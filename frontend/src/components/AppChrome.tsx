import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router-dom";
import { WifiOff } from "lucide-react";

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
      <WifiOff aria-hidden /> You're offline. Things you've already opened still show; changes need a connection.
    </div>
  );
}

/**
 * After client-side navigation: updates the document title from the page's heading and moves focus to the main
 * region so screen readers announce the new page. Skipped on first load so autofocus and scroll aren't disturbed.
 */
export function RouteAnnouncer() {
  const location = useLocation();
  const first = useRef(true);
  useEffect(() => {
    const frame = window.requestAnimationFrame(() => {
      const heading = document.querySelector("main h1, h1")?.textContent?.trim();
      document.title = heading ? `${heading} · BoulderTime` : "BoulderTime";
      if (first.current) { first.current = false; return; }
      document.getElementById("main")?.focus({ preventScroll: true });
      window.scrollTo({ top: 0 });
    });
    return () => window.cancelAnimationFrame(frame);
  }, [location.pathname]);
  return null;
}
