import { api } from "@/lib/api";
import { isNativeApp } from "@/lib/native";

export interface PushStatus { available: boolean; publicKey: string | null; subscribedOnThisDevice: boolean }

/**
 * Whether this device can receive notifications at all. The store apps always can — the system handles it — while a
 * browser needs a service worker and the Push API, and on iPhone the app has to be on the home screen first.
 */
export function pushSupportedHere(): boolean {
  if (isNativeApp()) return nativePushInThisBuild();
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
  const address = isNativeApp() ? (nativeToken ?? storedNativeToken()) : await currentEndpoint();
  const query = new URLSearchParams();
  if (address) query.set("address", address);
  if (isNativeApp()) query.set("platform", "NATIVE");
  const suffix = query.toString();
  return api.get<PushStatus>(`/api/users/me/push${suffix ? `?${suffix}` : ""}`);
}

/**
 * Whether this build of the app can receive notifications at all — that is, whether it was built with its Firebase
 * settings. Asking the plugin is not an option: without them it throws inside Android, and Capacitor rethrows that,
 * which closes the app. So the build says, and we never call the plugin unless it can answer.
 */
export function nativePushInThisBuild(): boolean {
  return isNativeApp() && import.meta.env.VITE_PUSH_NATIVE === "1";
}

/**
 * The store app's own notification token. Firebase hands it over through a listener rather than returning it, so
 * registering means asking and then waiting for the answer. It is also kept on the phone: without it, after the app
 * is closed and opened again, the app could no longer tell the server which device it is asking about, and the
 * switch would show notifications as off while they are still on.
 */
let nativeToken: string | undefined;
const TOKEN_KEY = "bt.nativePushToken";

function storedNativeToken(): string | undefined {
  try { return localStorage.getItem(TOKEN_KEY) ?? undefined; } catch { return undefined; }
}

function rememberNativeToken(token: string | undefined) {
  nativeToken = token;
  try {
    if (token) localStorage.setItem(TOKEN_KEY, token);
    else localStorage.removeItem(TOKEN_KEY);
  } catch {
    // Without storage the token lives for this run only; the next start asks Firebase again.
  }
}

type PushPlugin = (typeof import("@capacitor/push-notifications"))["PushNotifications"];

/** Asks Firebase for this phone's token and waits for it; null if it doesn't come. */
async function fetchNativeToken(PushNotifications: PushPlugin): Promise<string | null> {
  const handles: { remove: () => Promise<void> }[] = [];
  const token = await new Promise<string | null>((resolve) => {
    // If Firebase answers neither way, the person gets a clear failure instead of a spinner that never stops.
    const giveUp = window.setTimeout(() => resolve(null), 15_000);
    const settle = (value: string | null) => { window.clearTimeout(giveUp); resolve(value); };
    void PushNotifications.addListener("registration", (t) => settle(t.value)).then((h) => handles.push(h));
    void PushNotifications.addListener("registrationError", () => settle(null)).then((h) => handles.push(h));
    void PushNotifications.register();
  });
  await Promise.all(handles.map((h) => h.remove().catch(() => {})));
  return token;
}

async function enableNativePush(): Promise<"enabled" | "denied" | "unsupported"> {
  if (!nativePushInThisBuild()) return "unsupported";
  const { PushNotifications } = await import("@capacitor/push-notifications");

  const asked = await PushNotifications.requestPermissions();
  if (asked.receive !== "granted") return "denied";

  const token = await fetchNativeToken(PushNotifications);
  if (!token) return "unsupported";

  await api.post("/api/users/me/push", { platform: "NATIVE", token });
  rememberNativeToken(token);
  return "enabled";
}

async function disableNativePush(): Promise<void> {
  if (!nativePushInThisBuild()) return;
  const { PushNotifications } = await import("@capacitor/push-notifications");
  const token = nativeToken ?? storedNativeToken();
  // Removing the stored notifications too, so the tray doesn't keep showing what we stopped sending.
  await PushNotifications.removeAllDeliveredNotifications().catch(() => {});
  // Always this phone only: without an address the server would forget every device of the account.
  if (token) await api.delete(`/api/users/me/push?address=${encodeURIComponent(token)}`);
  rememberNativeToken(undefined);
}

/**
 * At every start of the store app: if notifications were turned on here and the permission still stands, ask Firebase
 * for the token again, quietly (no prompt). Firebase renews tokens now and then; a new one is registered and the old
 * one dropped, so notifications keep arriving without the person doing anything.
 */
export async function syncNativePush(): Promise<void> {
  if (!nativePushInThisBuild()) return;
  const previous = storedNativeToken();
  if (!previous) return; // never turned on on this phone
  const { PushNotifications } = await import("@capacitor/push-notifications");
  if ((await PushNotifications.checkPermissions()).receive !== "granted") return;
  const token = await fetchNativeToken(PushNotifications);
  if (!token) return;
  nativeToken = token;
  if (token !== previous) {
    await api.post("/api/users/me/push", { platform: "NATIVE", token });
    await api.delete(`/api/users/me/push?address=${encodeURIComponent(previous)}`).catch(() => {});
    rememberNativeToken(token);
  }
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

export interface PushTestResult { outcome: "delivered" | "failed" | "gone" | "skipped"; detail: string | null }

/** Has the server send a notification to this device now, and returns what the push service answered. */
export async function sendTestPush(): Promise<PushTestResult> {
  const address = isNativeApp() ? (nativeToken ?? storedNativeToken()) : await currentEndpoint();
  if (!address) return { outcome: "gone", detail: null };
  return api.post<PushTestResult>(`/api/users/me/push/test?address=${encodeURIComponent(address)}`);
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
