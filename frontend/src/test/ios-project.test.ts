import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { resolve } from "node:path";

const ios = (path: string) => resolve(__dirname, "../../ios/App", path);
const infoPlist = readFileSync(ios("App/Info.plist"), "utf8");
const entitlements = readFileSync(ios("App/App.entitlements"), "utf8");
const project = readFileSync(ios("App.xcodeproj/project.pbxproj"), "utf8");
const sha = (path: string) => createHash("sha256").update(readFileSync(path)).digest("hex");

/** The text of the `<string>` that follows a key in a plist, or null when the key is missing or empty. */
function plistString(plist: string, key: string): string | null {
  const match = plist.match(new RegExp(`<key>${key}</key>\\s*<string>([^<]*)</string>`));
  const value = match?.[1]?.trim();
  return value ? value : null;
}

/**
 * The iOS project is generated, and regenerating it would quietly drop all of this. Unlike Android, iOS does not fail
 * silently: an app that opens the camera, the photo library or the location without saying why is closed on the spot
 * by the system, and App Review rejects it.
 */
describe("What the iOS app must declare", () => {
  it.each([
    ["NSCameraUsageDescription", "the camera, for boulder photos and beta videos"],
    ["NSMicrophoneUsageDescription", "the microphone, for the sound of a recorded video"],
    ["NSPhotoLibraryUsageDescription", "the photo library, for choosing a photo or a video"],
    ["NSLocationWhenInUseUsageDescription", "the location, for nearby gyms on the map"],
  ])("says why it asks for %s (%s)", (key) => {
    expect(plistString(infoPlist, key)).not.toBeNull();
  });

  it("answers the export question up front, so every upload isn't held for a compliance form", () => {
    expect(infoPlist).toMatch(/<key>ITSAppUsesNonExemptEncryption<\/key>\s*<false\/>/);
  });

  it("is called BoulderTime on the home screen", () => {
    expect(plistString(infoPlist, "CFBundleDisplayName")).toBe("BoulderTime");
  });

  it("claims bouldertime.com links, and the build actually uses that claim", () => {
    expect(entitlements).toContain("applinks:bouldertime.com");
    expect(project.match(/CODE_SIGN_ENTITLEMENTS = App\/App\.entitlements;/g)).toHaveLength(2);
  });

  it("uses the bundle id the Android app and Firebase already know", () => {
    expect(project).toContain("PRODUCT_BUNDLE_IDENTIFIER = com.bouldertime.app;");
  });

  it("shows BoulderTime's icon and splash, not Capacitor's", () => {
    const resources = resolve(__dirname, "../../resources");
    expect(sha(ios("App/Assets.xcassets/AppIcon.appiconset/AppIcon-512@2x.png"))).toBe(sha(`${resources}/icon.png`));
    for (const name of ["splash-2732x2732", "splash-2732x2732-1", "splash-2732x2732-2"]) {
      expect(sha(ios(`App/Assets.xcassets/Splash.imageset/${name}.png`))).toBe(sha(`${resources}/splash.png`));
    }
  });
});
