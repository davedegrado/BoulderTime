import { useCallback, useEffect, useState } from "react";

export type LocationState =
  | { status: "idle" | "locating" }
  | { status: "found"; lat: number; lng: number; accuracy: number }
  | { status: "denied" | "unavailable" };

const CACHE_KEY = "bt:last-location";

/**
 * The browser's position (asks permission once; needs HTTPS). The last known position is remembered on this device
 * so the map opens in the right place immediately next time.
 */
export function useUserLocation(auto = true) {
  const [state, setState] = useState<LocationState>(() => {
    try {
      const cached = JSON.parse(localStorage.getItem(CACHE_KEY) ?? "null") as { lat: number; lng: number } | null;
      return cached ? { status: "found", lat: cached.lat, lng: cached.lng, accuracy: 5000 } : { status: "idle" };
    } catch {
      return { status: "idle" };
    }
  });

  const locate = useCallback(() => {
    if (!("geolocation" in navigator)) { setState({ status: "unavailable" }); return; }
    setState((s) => (s.status === "found" ? s : { status: "locating" }));
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const next = { lat: pos.coords.latitude, lng: pos.coords.longitude };
        try { localStorage.setItem(CACHE_KEY, JSON.stringify(next)); } catch { /* storage full or disabled */ }
        setState({ status: "found", ...next, accuracy: pos.coords.accuracy });
      },
      (err) => setState((s) => (s.status === "found" ? s : { status: err.code === err.PERMISSION_DENIED ? "denied" : "unavailable" })),
      { enableHighAccuracy: false, timeout: 10_000, maximumAge: 5 * 60_000 },
    );
  }, []);

  useEffect(() => { if (auto) locate(); }, [auto, locate]);
  return { location: state, locate };
}
