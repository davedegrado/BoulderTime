import { useCallback, useEffect, useState } from "react";
import { isNativeApp } from "@/lib/native";

export type LocationState =
  | { status: "idle" | "locating" }
  | { status: "found"; lat: number; lng: number; accuracy: number }
  | { status: "denied" | "unavailable" };

const CACHE_KEY = "bt:last-location";

/**
 * The device's position (asks permission once; in a browser it needs HTTPS). The last known position is remembered on this device
 * so the map opens in the right place immediately next time.
 */
export function useUserLocation(auto = false) {
  const [state, setState] = useState<LocationState>(() => {
    try {
      const cached = JSON.parse(localStorage.getItem(CACHE_KEY) ?? "null") as { lat: number; lng: number } | null;
      return cached ? { status: "found", lat: cached.lat, lng: cached.lng, accuracy: 5000 } : { status: "idle" };
    } catch {
      return { status: "idle" };
    }
  });

  const locate = useCallback(() => {
    if (!isNativeApp() && !("geolocation" in navigator)) { setState({ status: "unavailable" }); return; }
    setState((s) => (s.status === "found" ? s : { status: "locating" }));
    currentPosition().then(
      (pos) => {
        const next = { lat: pos.lat, lng: pos.lng };
        try { localStorage.setItem(CACHE_KEY, JSON.stringify(next)); } catch { /* storage full or disabled */ }
        setState({ status: "found", ...next, accuracy: pos.accuracy });
      },
      (denied: boolean) => setState((s) => (s.status === "found" ? s : { status: denied ? "denied" : "unavailable" })),
    );
  }, []);

  useEffect(() => { if (auto) locate(); }, [auto, locate]);
  return { location: state, locate };
}

const OPTIONS = { enableHighAccuracy: false, timeout: 10_000, maximumAge: 5 * 60_000 };

/**
 * Where the phone is. In the store apps the system is asked directly (Capacitor's Geolocation): the WebView's own
 * location asks a second time, as "localhost", on every start on iPhone. Approximate is enough — the position is
 * coarsened before it leaves the phone anyway (coarse.ts) — so Android 12+ offers only "approximate".
 * Rejects with true when the person refused, false when the position just couldn't be had.
 */
async function currentPosition(): Promise<{ lat: number; lng: number; accuracy: number }> {
  if (isNativeApp()) {
    try {
      const { Geolocation } = await import("@capacitor/geolocation");
      const pos = await Geolocation.getCurrentPosition(OPTIONS);
      return { lat: pos.coords.latitude, lng: pos.coords.longitude, accuracy: pos.coords.accuracy };
    } catch (e) {
      // OS-PLUG-GLOC-0003: refused; 0008: restricted on iPhone (Screen Time, profiles).
      const code = (e as { code?: string }).code ?? "";
      throw code.endsWith("0003") || code.endsWith("0008");
    }
  }
  return new Promise((resolve, reject) => navigator.geolocation.getCurrentPosition(
    (pos) => resolve({ lat: pos.coords.latitude, lng: pos.coords.longitude, accuracy: pos.coords.accuracy }),
    (err) => reject(err.code === err.PERMISSION_DENIED),
    OPTIONS,
  ));
}
