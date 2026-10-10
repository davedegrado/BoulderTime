import { afterEach, describe, expect, it, vi } from "vitest";

const isNative = vi.fn(() => false);
vi.mock("@capacitor/core", () => ({ Capacitor: { isNativePlatform: () => isNative(), getPlatform: () => "web" } }));

afterEach(() => isNative.mockReset());

describe("Running as a website or as a store app", () => {
  it("keeps links on the page's own address on the web", async () => {
    isNative.mockReturnValue(false);
    const { publicOrigin } = await import("@/lib/native");
    expect(publicOrigin()).toBe(window.location.origin);
  });

  it("sends links from the store app to the website, because a mail client can't open the app's internal address", async () => {
    isNative.mockReturnValue(true);
    const { publicOrigin } = await import("@/lib/native");
    expect(publicOrigin()).toBe("https://bouldertime.com");
  });

  it("does nothing native on the web, so the website and the PWA load no plugins", async () => {
    isNative.mockReturnValue(false);
    const { startNativeShell } = await import("@/lib/native");
    await expect(startNativeShell()).resolves.toBeUndefined();
  });

  it("keeps the launch screen up for about two seconds, however fast the app starts", async () => {
    const { splashDelay, SPLASH_MIN_MS } = await import("@/lib/native");
    expect(SPLASH_MIN_MS).toBe(2000);
    expect(splashDelay(300)).toBe(1700);
    // A slow start already showed it long enough: no extra wait on top.
    expect(splashDelay(2600)).toBe(0);
    expect(splashDelay(-5)).toBe(2000);
  });
});
