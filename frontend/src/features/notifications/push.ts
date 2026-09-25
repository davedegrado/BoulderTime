import { api } from "@/lib/api";

export interface PushStatus { available: boolean; publicKey: string | null; subscribedOnThisDevice: boolean }

/** Push needs a service worker, the Push API, and — on iPhone — the app installed to the home screen. */
export function pushSupportedHere(): boolean {
  return typeof window !== "undefined" && "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
}

/** True on an iPhone that is browsing in Safari: there, notifications only work once the app is installed. */
export function needsInstallFirst(): boolean {
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
  const endpoint = await currentEndpoint();
  const query = endpoint ? `?endpoint=${encodeURIComponent(endpoint)}` : "";
  return api.get<PushStatus>(`/api/users/me/push${query}`);
}

/**
 * Asks for permission and registers this device. Returns why it didn't work, so the app can say something
 * more useful than "failed" — a refused permission can't be asked for again from here.
 */
export async function enablePush(publicKey: string): Promise<"enabled" | "denied" | "unsupported"> {
  const reg = await registration();
  if (!reg) return "unsupported";
  if ((await Notification.requestPermission()) !== "granted") return "denied";

  const subscription = (await reg.pushManager.getSubscription())
    ?? (await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: decodeKey(publicKey) }));
  const json = subscription.toJSON();
  await api.post("/api/users/me/push", { endpoint: subscription.endpoint, keys: { p256dh: json.keys?.p256dh, auth: json.keys?.auth } });
  return "enabled";
}

export async function disablePush(): Promise<void> {
  const reg = await registration();
  const subscription = await reg?.pushManager.getSubscription();
  const endpoint = subscription?.endpoint;
  await subscription?.unsubscribe();
  await api.delete(`/api/users/me/push${endpoint ? `?endpoint=${encodeURIComponent(endpoint)}` : ""}`);
}

/** The server sends the key as base64url; the browser wants raw bytes. */
function decodeKey(value: string): ArrayBuffer {
  const padded = value.replace(/-/g, "+").replace(/_/g, "/").padEnd(value.length + ((4 - (value.length % 4)) % 4), "=");
  const binary = atob(padded);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes.buffer;
}
