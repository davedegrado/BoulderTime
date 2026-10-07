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
vi.mock("@capacitor/push-notifications", () => ({
  PushNotifications: {
    requestPermissions: () => permission(),
    register: async () => { registerCalled(); listeners.get("registration")?.({ value: "fcm-token-123" }); },
    addListener: async (name: string, fn: Listener) => { listeners.set(name, fn); return { remove: async () => {} }; },
    removeAllDeliveredNotifications: async () => {},
  },
}));

beforeEach(() => {
  calls.length = 0;
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
});
