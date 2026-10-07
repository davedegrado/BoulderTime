import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const manifest = readFileSync(resolve(__dirname, "../../android/app/src/main/AndroidManifest.xml"), "utf8");
const filePaths = readFileSync(resolve(__dirname, "../../android/app/src/main/res/xml/file_paths.xml"), "utf8");

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

  it("shares the folder a captured photo is written to", () => {
    expect(filePaths).toContain("external-files-path");
    expect(filePaths).toContain('path="Pictures"');
  });
});
