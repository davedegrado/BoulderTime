import { useCallback } from "react";
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
