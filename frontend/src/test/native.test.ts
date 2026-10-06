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
});
