import { useCallback, useRef } from "react";
import { useLocation, useNavigate } from "react-router-dom";

/**
 * "Back" that goes back: to the screen the person actually came from (a profile, a filtered list, a search, the
 * notifications), the way the phone's own back button and the browser's do. Only when the app was opened right on
 * this screen (a shared link, a fresh tab) is there nothing behind it, and then `fallback` stands in, replacing the
 * current entry so the back stack doesn't grow a duplicate.
 *
 * React Router gives the first entry of a session the key "default"; every screen reached inside the app has another.
 */
export function useGoBack() {
  const navigate = useNavigate();
  const { key } = useLocation();
  return useCallback((fallback: string) => {
    if (key !== "default") navigate(-1);
    else navigate(fallback, { replace: true });
  }, [key, navigate]);
}

/** Where this entry sits in the session's history: React Router keeps it as `idx` in the history state. */
function historyIndex(): number | null {
  const idx = (window.history.state as { idx?: unknown } | null)?.idx;
  return typeof idx === "number" ? idx : null;
}

/**
 * "Back" out of an area made of several screens (the staff area and its tabs): to the screen the person was on before
 * entering it, however many tabs they went through since, so that screen's own back doesn't lead into the area again.
 * Call it in the area's layout, which stays mounted while its tabs change. Opened right inside the area, with nothing
 * behind it, `fallback` replaces the current entry.
 */
export function useLeaveArea() {
  const navigate = useNavigate();
  const entry = useRef<number | null | undefined>(undefined);
  if (entry.current === undefined) entry.current = historyIndex();
  return useCallback((fallback: string) => {
    const start = entry.current, now = historyIndex();
    if (start != null && start > 0 && now != null && now >= start) navigate(-(now - start + 1));
    else navigate(fallback, { replace: true });
  }, [navigate]);
}
