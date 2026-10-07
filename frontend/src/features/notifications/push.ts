import { api } from "@/lib/api";
import { isNativeApp } from "@/lib/native";

export interface PushStatus { available: boolean; publicKey: string | null; subscribedOnThisDevice: boolean }

/**
 * Whether this device can receive notifications at all. The store apps always can — the system handles it — while a
 * browser needs a service worker and the Push API, and on iPhone the app has to be on the home screen first.
 */
export function pushSupportedHere(): boolean {
  if (isNativeApp()) return true;
  return typeof window !== "undefined" && "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
}

/** True on an iPhone that is browsing in Safari: there, notifications only work once the app is installed. */
export function needsInstallFirst(): boolean {
  if (isNativeApp()) return false;
  const iOS = /iphone|ipad|ipod/i.test(navigator.userAgent);
  const installed = window.matchMedia("(display-mode: standalone)").matches || (navigator as { standalone?: boolean }).standalone === true;
  return iOS && !installed;
}

async function registration(): Promise<ServiceWorkerRegistration | null> {
  if (!pushSupportedHere()) return null;
  return (await navigator.serviceWorker.getRegistration()) ?? (await navigator.serviceWorker.ready);
}

async function currentEndpoint(): Promise<string | undefined> {
  const reg = await registration();
  const existing = await reg?.pushManager.getSubscription();
  return existing?.endpoint;
}

export async function pushStatus(): Promise<PushStatus> {
  const address = isNativeApp() ? nativeToken : await currentEndpoint();
  const query = new URLSearchParams();
  if (address) query.set("address", address);
  if (isNativeApp()) query.set("platform", "NATIVE");
  const suffix = query.toString();
  return api.get<PushStatus>(`/api/users/me/push${suffix ? `?${suffix}` : ""}`);
}

/**
 * The store app's own notification token, remembered for as long as the app runs. Firebase hands it over through a
 * listener rather than returning it, so registering means asking and then waiting for the answer.
 */
let nativeToken: string | undefined;

async function enableNativePush(): Promise<"enabled" | "denied" | "unsupported"> {
  const { PushNotifications } = await import("@capacitor/push-notifications");

  const asked = await PushNotifications.requestPermissions();
  if (asked.receive !== "granted") return "denied";

  const token = await new Promise<string | null>((resolve) => {
    // If Firebase answers neither way, the person gets a clear failure instead of a spinner that never stops.
    const giveUp = window.setTimeout(() => resolve(null), 15_000);
    const settle = (value: string | null) => { window.clearTimeout(giveUp); resolve(value); };
    void PushNotifications.addListener("registration", (t) => settle(t.value));
    void PushNotifications.addListener("registrationError", () => settle(null));
    void PushNotifications.register();
  });
  if (!token) return "unsupported";

  await api.post("/api/users/me/push", { platform: "NATIVE", token });
  nativeToken = token;
  return "enabled";
}

async function disableNativePush(): Promise<void> {
  const { PushNotifications } = await import("@capacitor/push-notifications");
  const token = nativeToken;
  // Removing the stored notifications too, so the tray doesn't keep showing what we stopped sending.
  await PushNotifications.removeAllDeliveredNotifications().catch(() => {});
  await api.delete(`/api/users/me/push${token ? `?address=${encodeURIComponent(token)}` : ""}`);
  nativeToken = undefined;
}

/**
 * Asks for permission and registers this device. Returns why it didn't work, so the app can say something
 * more useful than "failed" — a refused permission can't be asked for again from here.
 */
export async function enablePush(publicKey: string): Promise<"enabled" | "denied" | "unsupported" | "misconfigured"> {
  if (isNativeApp()) return enableNativePush();

  const reg = await registration();
  if (!reg) return "unsupported";
  if ((await Notification.requestPermission()) !== "granted") return "denied";

  let key: ArrayBuffer;
  try { key = decodeKey(publicKey); } catch { return "misconfigured"; }
  const subscription = (await reg.pushManager.getSubscription())
    ?? (await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key }));
  const json = subscription.toJSON();
  await api.post("/api/users/me/push", { endpoint: subscription.endpoint, keys: { p256dh: json.keys?.p256dh, auth: json.keys?.auth } });
  return "enabled";
}

export async function disablePush(): Promise<void> {
  if (isNativeApp()) return disableNativePush();

  const reg = await registration();
  const subscription = await reg?.pushManager.getSubscription();
  const endpoint = subscription?.endpoint;
  await subscription?.unsubscribe();
  await api.delete(`/api/users/me/push${endpoint ? `?address=${encodeURIComponent(endpoint)}` : ""}`);
}

/** The server sends the key as base64url; the browser wants raw bytes. */
function decodeKey(raw: string): ArrayBuffer {
  // A key pasted with quotes, spaces or a trailing newline is a configuration mistake, not a browser problem.
  const value = raw.trim().replace(/^"|"$/g, "");
  if (!/^[A-Za-z0-9\-_]{80,100}$/.test(value)) throw new Error("push_key_invalid");
  const padded = value.replace(/-/g, "+").replace(/_/g, "/").padEnd(value.length + ((4 - (value.length % 4)) % 4), "=");
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}
