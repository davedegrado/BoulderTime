import { readFileSync } from "node:fs";
import { createHash } from "node:crypto";
import { resolve } from "node:path";

const ios = (path: string) => resolve(__dirname, "../../ios/App", path);
const infoPlist = readFileSync(ios("App/Info.plist"), "utf8");
const entitlements = readFileSync(ios("App/App.entitlements"), "utf8");
const releaseEntitlements = readFileSync(ios("App/App.Release.entitlements"), "utf8");
const project = readFileSync(ios("App.xcodeproj/project.pbxproj"), "utf8");
const appDelegate = readFileSync(ios("App/AppDelegate.swift"), "utf8");
const privacyManifest = readFileSync(ios("App/PrivacyInfo.xcprivacy"), "utf8");
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
    // Debug uses App.entitlements, the App Store build App.Release.entitlements (same claim, production push).
    expect(project.match(/CODE_SIGN_ENTITLEMENTS = App\/App\.entitlements;/g)).toHaveLength(1);
    expect(project.match(/CODE_SIGN_ENTITLEMENTS = App\/App\.Release\.entitlements;/g)).toHaveLength(1);
  });

  it("uses the bundle id the Android app and Firebase already know", () => {
    expect(project).toContain("PRODUCT_BUNDLE_IDENTIFIER = com.bouldertime.app;");
  });

  it("shows BoulderTime's icon and splash, not Capacitor's", () => {
    const resources = resolve(__dirname, "../../resources");
    expect(sha(ios("App/Assets.xcassets/AppIcon.appiconset/AppIcon-512@2x.png"))).toBe(sha(`${resources}/icon.png`));
    for (const [set, file] of [["SplashLogo", "logo"], ["SplashCornerTop", "corner-top-left"], ["SplashCornerBottom", "corner-bottom-right"]]) {
      expect(sha(ios(`App/Assets.xcassets/${set}.imageset/${file}.png`))).toBe(sha(`${resources}/splash/${file}.png`));
    }
  });

  it("draws the launch screen from those images, on BoulderTime's dark background", () => {
    const storyboard = readFileSync(ios("App/Base.lproj/LaunchScreen.storyboard"), "utf8");
    expect(plistString(infoPlist, "UILaunchStoryboardName")).toBe("LaunchScreen");
    for (const image of ["SplashLogo", "SplashCornerTop", "SplashCornerBottom"]) {
      expect(storyboard).toContain(`image="${image}"`);
    }
    // Light status bar text over the dark launch screen.
    expect(plistString(infoPlist, "UIStatusBarStyle")).toBe("UIStatusBarStyleLightContent");
  });
});

/** Notifications go through Firebase on iOS as on Android (ADR-039); each of these breaks them silently if lost. */
describe("Notifications in the iOS app", () => {
  it("may receive them: from Apple's test servers in Debug, from the real ones in the App Store build", () => {
    expect(entitlements).toMatch(/<key>aps-environment<\/key>\s*<string>development<\/string>/);
    expect(releaseEntitlements).toMatch(/<key>aps-environment<\/key>\s*<string>production<\/string>/);
    // The same links claim in both.
    expect(releaseEntitlements).toContain("<string>applinks:bouldertime.com</string>");
  });

  it("links Firebase Messaging and bundles the Firebase settings the workflow writes", () => {
    expect(project).toContain('repositoryURL = "https://github.com/firebase/firebase-ios-sdk";');
    expect(project).toMatch(/productName = FirebaseMessaging;/);
    expect(project).toMatch(/GoogleService-Info\.plist in Resources \*\/,/);
  });

  it("hands Firebase's token to Capacitor, not Apple's", () => {
    // Firebase is told about Apple's token itself, rather than by swizzling the app delegate behind Capacitor's back.
    expect(infoPlist).toMatch(/<key>FirebaseAppDelegateProxyEnabled<\/key>\s*<false\/>/);
    expect(appDelegate).toContain("Messaging.messaging().apnsToken = deviceToken");
    expect(appDelegate).toContain("name: .capacitorDidRegisterForRemoteNotifications, object: token");
    expect(appDelegate).toContain(".capacitorDidFailToRegisterForRemoteNotifications");
  });
});

describe("The iOS privacy manifest", () => {
  it("is in the app and says BoulderTime tracks nobody", () => {
    expect(project).toMatch(/PrivacyInfo\.xcprivacy in Resources \*\/,/);
    expect(privacyManifest).toMatch(/<key>NSPrivacyTracking<\/key>\s*<false\/>/);
    expect(privacyManifest).not.toMatch(/<key>NSPrivacyCollectedDataTypeTracking<\/key>\s*<true\/>/);
  });
});

/** The App Store build (ADR-045): signed by hand in Release, for this team and bundle id, never with Debug's entitlements. */
describe("The App Store build", () => {
  const release = project.slice(project.indexOf("504EC3181FED79650016851F /* Release */"), project.indexOf("/* End XCBuildConfiguration section */"));

  it("signs the App target's Release with Apple Distribution and the profile the workflow names", () => {
    expect(release).toContain("CODE_SIGN_STYLE = Manual;");
    expect(release).toContain('"CODE_SIGN_IDENTITY[sdk=iphoneos*]" = "Apple Distribution";');
    expect(release).toContain("DEVELOPMENT_TEAM = AGXG273RG9;");
    expect(release).toContain('PROVISIONING_PROFILE_SPECIFIER = "$(BT_PROFILE_NAME)";');
    expect(release).toContain("CODE_SIGN_ENTITLEMENTS = App/App.Release.entitlements;");
    expect(release).toContain("PRODUCT_BUNDLE_IDENTIFIER = com.bouldertime.app;");
  });

  it("lets bouldertime.com vouch for the app, so links open in it", () => {
    const aasa = JSON.parse(readFileSync(resolve(__dirname, "../../public/.well-known/apple-app-site-association"), "utf8"));
    expect(aasa.applinks.details[0].appIDs).toEqual(["AGXG273RG9.com.bouldertime.app"]);
  });

  it("archives for iPhones and checks the profile against the same team and bundle id", () => {
    const workflow = readFileSync(resolve(__dirname, "../../../.github/workflows/ios.yml"), "utf8");
    expect(workflow).toContain("APPLE_TEAM_ID: AGXG273RG9");
    expect(workflow).toContain("-sdk iphoneos");
    expect(workflow).toContain("<key>method</key><string>app-store-connect</string>");
    expect(workflow).toContain('"$APPLE_TEAM_ID.com.bouldertime.app"');
  });
});
