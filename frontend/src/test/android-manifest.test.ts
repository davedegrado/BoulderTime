import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const manifest = readFileSync(resolve(__dirname, "../../android/app/src/main/AndroidManifest.xml"), "utf8");
const filePaths = readFileSync(resolve(__dirname, "../../android/app/src/main/res/xml/file_paths.xml"), "utf8");
const res = (path: string) => resolve(__dirname, "../../android/app/src/main/res", path);

/**
 * The Android project is generated, and regenerating it would quietly drop these. Both failures are silent: the
 * camera would just open the gallery, which is how the first one was found — on a phone, not here.
 */
describe("What the Android app must declare for the camera", () => {
  it("declares the camera intents, or Android 11+ reports no camera app at all", () => {
    expect(manifest).toContain("android.media.action.IMAGE_CAPTURE");
    expect(manifest).toContain("android.media.action.VIDEO_CAPTURE");
    // <queries> only counts as a direct child of <manifest>.
    expect(manifest).toMatch(/<\/application>[\s\S]*<queries>/);
  });

  it("asks for camera permission", () => {
    expect(manifest).toContain("android.permission.CAMERA");
  });

  it("has its own notification icon, or Android shows a filled square", () => {
    // The app icon is fully opaque; Android keeps only a notification icon's silhouette, so it would arrive as a blob.
    expect(manifest).toContain("com.google.firebase.messaging.default_notification_icon");
    expect(manifest).toContain("@drawable/ic_stat_bouldertime");
    for (const density of ["mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi"]) {
      expect(existsSync(resolve(__dirname, `../../android/app/src/main/res/drawable-${density}/ic_stat_bouldertime.png`)))
        .toBe(true);
    }
  });

  it("shares the folder a captured photo is written to", () => {
    expect(filePaths).toContain("external-files-path");
    expect(filePaths).toContain('path="Pictures"');
  });
});

/**
 * bouldertime.com vouches for the app in assetlinks.json. The store app is signed by Google, the CI builds by our upload
 * key: drop either fingerprint and links open in the browser for those installs, with no error anywhere.
 */
describe("Links that open the Android app", () => {
  const links = JSON.parse(readFileSync(resolve(__dirname, "../../public/.well-known/assetlinks.json"), "utf8")) as
    { target: { package_name: string; sha256_cert_fingerprints: string[] } }[];
  const target = links[0]!.target;

  it("names the app", () => {
    expect(target.package_name).toBe("com.bouldertime.app");
  });

  it.each([
    ["the upload key, for the APKs built by CI", "74:E9:81:DE"],
    ["Google Play's signing key, for the app from the store", "FE:B7:60:8C"],
  ])("trusts %s", (_, start) => {
    expect(target.sha256_cert_fingerprints.some((f) => f.startsWith(start))).toBe(true);
  });

  it("lists only well-formed SHA-256 fingerprints", () => {
    for (const f of target.sha256_cert_fingerprints) expect(f).toMatch(/^([0-9A-F]{2}:){31}[0-9A-F]{2}$/);
  });
});

/** Android 12+ draws the launch screen itself from the launch theme: a colour, a centred icon, a name at the bottom. */
describe("The Android launch screen", () => {
  const styles = readFileSync(res("values/styles.xml"), "utf8");
  const styles31 = readFileSync(res("values-v31/styles.xml"), "utf8");

  it("is BoulderTime's dark background with the climber in the middle", () => {
    expect(manifest).toContain('android:theme="@style/AppTheme.NoActionBarLaunch"');
    for (const theme of [styles, styles31]) {
      expect(theme).toContain('<item name="windowSplashScreenBackground">@color/bt_splash_background</item>');
      expect(theme).toContain('<item name="windowSplashScreenAnimatedIcon">@drawable/splash_icon</item>');
      expect(theme).toContain('<item name="postSplashScreenTheme">@style/AppTheme.NoActionBar</item>');
    }
    expect(readFileSync(res("values/colors.xml"), "utf8")).toContain('<color name="bt_splash_background">#0C0D0F</color>');
  });

  it("adds the name at the bottom on Android 12+", () => {
    expect(styles31).toContain('<item name="android:windowSplashScreenBrandingImage">@drawable/splash_branding</item>');
  });

  it("has every image in every density", () => {
    for (const density of ["mdpi", "hdpi", "xhdpi", "xxhdpi", "xxxhdpi"]) {
      for (const name of ["splash_icon", "splash_branding"]) {
        expect(existsSync(res(`drawable-${density}/${name}.png`))).toBe(true);
      }
    }
  });
});
