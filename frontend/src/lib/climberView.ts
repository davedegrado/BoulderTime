import { useSyncExternalStore } from "react";
import { useCurrentUser } from "@/features/users/api";

/**
 * "See gyms as a climber", for BoulderTime's own admins: they are staff of the demo gyms, and want to see them the way a
 * climber does. While it is on, the staff's buttons are left out of the gyms' and boulders' pages (manage, edit, hidden
 * sectors, flagging a leaderboard row). The staff area itself stays reachable from the profile. Kept on this device.
 */
const KEY = "bt:climber-view";
const listeners = new Set<() => void>();
const read = () => { try { return localStorage.getItem(KEY) === "1"; } catch { return false; } };

export function setClimberView(on: boolean) {
  try { if (on) localStorage.setItem(KEY, "1"); else localStorage.removeItem(KEY); } catch { /* not kept: off next time */ }
  listeners.forEach((l) => l());
}

const subscribe = (l: () => void) => { listeners.add(l); return () => { listeners.delete(l); }; };

/** Whether the switch is on (whoever turned it on). */
export function useClimberViewSwitch() {
  return useSyncExternalStore(subscribe, read, () => false);
}

/** Whether to leave the staff's buttons out: the switch is on, and this is a BoulderTime admin. */
export function useHideStaffTools() {
  const on = useClimberViewSwitch();
  const me = useCurrentUser();
  return on && (me.data?.isPlatformAdmin ?? false);
}
