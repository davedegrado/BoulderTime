import { act, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { compareVersions, parseVersion, storePage, updateVerdict } from "@/lib/appVersion";

// A store app on an iPhone, unless a test says otherwise.
let native = true;
let platform = "ios";
let installed = "1.1.0";
vi.mock("@/lib/native", () => ({ isNativeApp: () => native, loadAppPlugin: async () => appPlugin }));
vi.mock("@capacitor/core", () => ({ Capacitor: { getPlatform: () => platform, isNativePlatform: () => native } }));
const capApp = vi.hoisted(() => ({
  listeners: [] as ((s: { isActive: boolean }) => void)[],
}));
const appPlugin = {
  getInfo: async () => ({ version: installed }),
  addListener: async (_: string, handler: (s: { isActive: boolean }) => void) => { capApp.listeners.push(handler); return { remove: async () => {} }; },
};
const get = vi.fn();
vi.mock("@/lib/api", () => ({ api: { get: (...args: unknown[]) => get(...args) } }));

import { AppVersionGate } from "@/features/appVersion/AppVersionGate";

const versions = (ios: object, android: object = { minimum: "1.0.0", recommended: null, storeUrl: null }) => ({ ios: { recommended: null, storeUrl: null, ...ios }, android });
const renderApp = () => render(<AppVersionGate><p>The app</p></AppVersionGate>);

beforeEach(() => {
  native = true; platform = "ios"; installed = "1.1.0"; capApp.listeners.length = 0;
  get.mockReset();
  localStorage.clear();
});

describe("Comparing versions", () => {
  it("compares them as numbers, part by part", () => {
    expect(compareVersions("1.10.0", "1.9.0")).toBeGreaterThan(0);
    expect(compareVersions("1.9.0", "1.10.0")).toBeLessThan(0);
    expect(compareVersions("1.2", "1.2.0")).toBe(0);
    expect(compareVersions("2", "1.99.99")).toBeGreaterThan(0);
    expect(compareVersions("1.0.0", "1.0.1")).toBeLessThan(0);
  });

  it("doesn't read what isn't a version, and then never asks to update", () => {
    expect(parseVersion("1.2.x")).toBeNull();
    expect(parseVersion("1.2.3.4")).toBeNull();
    expect(compareVersions("abc", "1.0.0")).toBeNull();
    expect(updateVerdict("abc", { minimum: "9.0.0", recommended: null, storeUrl: null })).toBe("ok");
    expect(updateVerdict("1.0.0", null)).toBe("ok");
  });

  it("asks to update below the minimum, suggests it below the recommended version, and is quiet otherwise", () => {
    const v = { minimum: "1.1.0", recommended: "1.10.0", storeUrl: null };
    expect(updateVerdict("1.0.9", v)).toBe("required");
    expect(updateVerdict("1.1.0", v)).toBe("recommended");
    expect(updateVerdict("1.9.0", v)).toBe("recommended");
    expect(updateVerdict("1.10.0", v)).toBe("ok");
  });

  it("opens the store page that is set, or the store when none is", () => {
    expect(storePage("ios", { minimum: "1", recommended: null, storeUrl: "https://apps.apple.com/app/id1" })).toBe("https://apps.apple.com/app/id1");
    expect(storePage("android", null)).toBe("https://play.google.com/store/apps/details?id=com.bouldertime.app");
  });
});

describe("The version check in the store apps", () => {
  it("shows only the request to update when this version may no longer be used", async () => {
    get.mockResolvedValue(versions({ minimum: "1.2.0", storeUrl: "https://apps.apple.com/app/id1" }));
    renderApp();

    expect(await screen.findByRole("heading", { name: "Update the app to continue" })).toBeInTheDocument();
    expect(screen.queryByText("The app")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open the App Store" })).toHaveAttribute("href", "https://apps.apple.com/app/id1");
    expect(screen.getByText("Installed version: 1.1.0")).toBeInTheDocument();
    expect(get).toHaveBeenCalledWith("/api/app-version", expect.objectContaining({ anonymous: true }));
  });

  it("uses the values of its own platform", async () => {
    platform = "android";
    get.mockResolvedValue(versions({ minimum: "9.0.0" }, { minimum: "1.0.0", recommended: null, storeUrl: null }));
    renderApp();
    await waitFor(() => expect(get).toHaveBeenCalled());
    expect(await screen.findByText("The app")).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Update the app to continue" })).not.toBeInTheDocument();
  });

  it("never locks anyone out when the API doesn't answer", async () => {
    get.mockRejectedValue(new Error("network down"));
    renderApp();
    await waitFor(() => expect(get).toHaveBeenCalled());
    expect(screen.getByText("The app")).toBeInTheDocument();
    expect(screen.queryByRole("alertdialog")).not.toBeInTheDocument();
  });

  it("suggests a recommended version in a banner that can be closed, and stays closed for that version", async () => {
    get.mockResolvedValue(versions({ minimum: "1.0.0", recommended: "1.2.0" }));
    const { unmount } = renderApp();
    expect(await screen.findByText("A new version of BoulderTime is available.")).toBeInTheDocument();
    expect(screen.getByText("The app")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Not now" }));
    expect(screen.queryByText("A new version of BoulderTime is available.")).not.toBeInTheDocument();

    unmount();
    renderApp();
    await waitFor(() => expect(get).toHaveBeenCalledTimes(2));
    expect(screen.queryByText("A new version of BoulderTime is available.")).not.toBeInTheDocument();
  });

  it("checks again when the app comes back to the front", async () => {
    get.mockResolvedValue(versions({ minimum: "1.0.0" }));
    renderApp();
    await waitFor(() => expect(get).toHaveBeenCalled());
    await waitFor(() => expect(capApp.listeners).toHaveLength(1));
    expect(screen.getByText("The app")).toBeInTheDocument();

    get.mockResolvedValue(versions({ minimum: "2.0.0" }));
    await act(async () => capApp.listeners[0]!({ isActive: true }));
    expect(await screen.findByRole("heading", { name: "Update the app to continue" })).toBeInTheDocument();
  });

  it("never asks on the website", async () => {
    native = false;
    platform = "web";
    get.mockResolvedValue(versions({ minimum: "99.0.0" }));
    renderApp();
    expect(screen.getByText("The app")).toBeInTheDocument();
    await new Promise((r) => setTimeout(r, 20));
    expect(get).not.toHaveBeenCalled();
  });
});
