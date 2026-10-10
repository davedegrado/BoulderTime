import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const native = vi.fn(() => false);
vi.mock("@/lib/native", () => ({ isNativeApp: () => native() }));

const calls: { method: string; path: string; body?: unknown }[] = [];
vi.mock("@/lib/api", () => ({
  api: {
    get: async (path: string) => { calls.push({ method: "GET", path }); return { available: true, publicKey: null, subscribedOnThisDevice: false }; },
    post: async (path: string, body: unknown) => { calls.push({ method: "POST", path, body }); return {}; },
    delete: async (path: string) => { calls.push({ method: "DELETE", path }); return {}; },
  },
}));

type Listener = (payload: unknown) => void;
const listeners = new Map<string, Listener>();
const permission = vi.fn(async () => ({ receive: "granted" }));
const registerCalled = vi.fn();
let nextToken = "fcm-token-123";
vi.mock("@capacitor/push-notifications", () => ({
  PushNotifications: {
    requestPermissions: () => permission(),
    checkPermissions: () => permission(),
    register: async () => { registerCalled(); listeners.get("registration")?.({ value: nextToken }); },
    addListener: async (name: string, fn: Listener) => { listeners.set(name, fn); return { remove: async () => {} }; },
    removeAllDeliveredNotifications: async () => {},
  },
}));

beforeEach(() => {
  calls.length = 0;
  nextToken = "fcm-token-123";
  localStorage.clear();
  listeners.clear();
  registerCalled.mockClear();
  permission.mockClear();
  permission.mockResolvedValue({ receive: "granted" });
  // What CI sets when the build carries its Firebase settings.
  vi.stubEnv("VITE_PUSH_NATIVE", "1");
});
afterEach(() => { native.mockReset(); vi.unstubAllEnvs(); });

describe("Notifications inside the store app", () => {
  it("registers the phone with its Firebase token, not with browser keys", async () => {
    native.mockReturnValue(true);
    const { enablePush } = await import("@/features/notifications/push");

    await expect(enablePush("")).resolves.toBe("enabled");

    expect(registerCalled).toHaveBeenCalledTimes(1);
    expect(calls).toContainEqual({ method: "POST", path: "/api/users/me/push", body: { platform: "NATIVE", token: "fcm-token-123" } });
  });

  it("asks the server about the phone, not about a browser endpoint", async () => {
    native.mockReturnValue(true);
    const { enablePush, pushStatus } = await import("@/features/notifications/push");
    await enablePush("");
    calls.length = 0;

    await pushStatus();

    expect(calls[0]?.path).toBe("/api/users/me/push?address=fcm-token-123&platform=NATIVE");
  });

  it("says so when the person refuses, instead of registering nothing and claiming success", async () => {
    native.mockReturnValue(true);
    permission.mockResolvedValue({ receive: "denied" });
    const { enablePush } = await import("@/features/notifications/push");

    await expect(enablePush("")).resolves.toBe("denied");
    expect(calls.filter((c) => c.method === "POST")).toHaveLength(0);
  });

  it("a test notification goes to this phone, even after the app was closed and opened again", async () => {
    native.mockReturnValue(true);
    localStorage.setItem("bt.nativePushToken", "fcm-token-123");
    const { sendTestPush } = await import("@/features/notifications/push");

    await sendTestPush();

    expect(calls).toContainEqual({ method: "POST", path: "/api/users/me/push/test?address=fcm-token-123", body: undefined });
  });

  it("turning them off tells the server which device stopped", async () => {
    native.mockReturnValue(true);
    const { enablePush, disablePush } = await import("@/features/notifications/push");
    await enablePush("");
    calls.length = 0;

    await disablePush();

    expect(calls).toContainEqual({ method: "DELETE", path: "/api/users/me/push?address=fcm-token-123" });
  });

  it("is available in the app, with no install step to explain", async () => {
    native.mockReturnValue(true);
    const { pushSupportedHere, needsInstallFirst } = await import("@/features/notifications/push");
    expect(pushSupportedHere()).toBe(true);
    expect(needsInstallFirst()).toBe(false);
  });

  it("never touches the plugin in a build without Firebase, because that closes the app", async () => {
    // Capacitor rethrows what the plugin throws (Bridge.callPluginMethod), so a missing Firebase config is a crash,
    // not an error we could catch. The build says whether it has one, and nothing asks the plugin otherwise.
    native.mockReturnValue(true);
    vi.stubEnv("VITE_PUSH_NATIVE", "");
    const { enablePush, disablePush, pushSupportedHere, nativePushInThisBuild } = await import("@/features/notifications/push");

    expect(nativePushInThisBuild()).toBe(false);
    expect(pushSupportedHere()).toBe(false);
    await expect(enablePush("")).resolves.toBe("unsupported");
    await expect(disablePush()).resolves.toBeUndefined();

    expect(registerCalled).not.toHaveBeenCalled();
    expect(permission).not.toHaveBeenCalled();
    expect(calls).toHaveLength(0);
  });

  it("still knows they are on after the app is closed and opened again", async () => {
    native.mockReturnValue(true);
    const first = await import("@/features/notifications/push");
    await first.enablePush("");

    // A new start of the app: nothing in memory, only what the phone kept.
    vi.resetModules();
    const again = await import("@/features/notifications/push");
    calls.length = 0;
    await again.pushStatus();
    expect(calls[0]?.path).toBe("/api/users/me/push?address=fcm-token-123&platform=NATIVE");
  });

  it("registers a renewed token at start and drops the old one, without asking for permission", async () => {
    native.mockReturnValue(true);
    const { enablePush } = await import("@/features/notifications/push");
    await enablePush("");
    vi.resetModules();
    const { syncNativePush } = await import("@/features/notifications/push");
    nextToken = "fcm-token-456";
    calls.length = 0;

    await syncNativePush();

    expect(calls).toContainEqual({ method: "POST", path: "/api/users/me/push", body: { platform: "NATIVE", token: "fcm-token-456" } });
    expect(calls).toContainEqual({ method: "DELETE", path: "/api/users/me/push?address=fcm-token-123" });
  });

  it("does nothing at start on a phone where they were never turned on", async () => {
    native.mockReturnValue(true);
    const { syncNativePush } = await import("@/features/notifications/push");
    await syncNativePush();
    expect(registerCalled).not.toHaveBeenCalled();
    expect(calls).toHaveLength(0);
  });

  it("never turns off every device of the account when it doesn't know this phone's token", async () => {
    native.mockReturnValue(true);
    vi.resetModules();
    const { disablePush } = await import("@/features/notifications/push");
    await disablePush();
    expect(calls.filter((c) => c.method === "DELETE")).toHaveLength(0);
  });
});
