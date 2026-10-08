# Store apps (Capacitor)

The Android and iOS apps wrap the same build as the website. `frontend/` stays the only frontend: Capacitor copies
`frontend/dist` into `frontend/android` and `frontend/ios` and adds a thin native shell.

## What changes when the code runs inside the app

All in `frontend/src/lib/native.ts`:

- **No service worker.** The files are already inside the app; a service worker would keep serving the previous
  version after an update. The website and the PWA still register it.
- **Back button (Android)** goes back through the app, and exits only from the first screen.
- **Status bar** dark text on the light header; **splash screen** stays until the first screen is drawn.
- **Links that end up in emails** (sign-up confirmation, password reset) point at `https://bouldertime.com`,
  because inside the app the page address is `https://localhost`, which a mail client cannot open.

## Building

GitHub Actions builds the Android app on every push that touches `frontend/` (`.github/workflows/android.yml`).
The installable APK is attached to the run under **Artifacts**. Nobody needs Android Studio.

The workflow needs these **repository variables** (Settings → Secrets and variables → Actions → Variables), the same
values Cloudflare uses for the website:

| Variable | Value |
| --- | --- |
| `VITE_SUPABASE_URL` | `https://<project>.supabase.co` |
| `VITE_SUPABASE_ANON_KEY` | the anon (public) key |
| `VITE_API_BASE_URL` | `https://api.bouldertime.com` (must be a full address) |
| `VITE_MAP_TILE_URL` | the MapTiler URL with the key |
| `VITE_MAP_ATTRIBUTION` | `© MapTiler © OpenStreetMap contributors` |

## One-time configuration outside the repository

- **API (Railway):** add `Cors__AllowedOrigins__1=https://localhost` so the Android app may call the API, and
  `Cors__AllowedOrigins__2=capacitor://localhost` for the iOS app (iOS serves the app's pages from that address).
- **MapTiler:** add `localhost` to the key's allowed origins, or the map stays blank inside the app.

## Versions

`frontend/package.json` `version` is the version people see (`0.1.0`). The build number is the GitHub Actions run
number, so every build is higher than the previous one without editing a file.

## Photos and videos

Every upload goes through `components/MediaInput.tsx`. In a browser it is a plain file field, which already offers
camera and library. Inside the app Android's WebView doesn't: without `capture` it opens the gallery only, with it the
camera only (see Capacitor's `BridgeWebChromeClient.onShowFileChooser`). So in the app the field first asks
"take a photo / choose from the gallery" and opens the matching one. No camera plugin needed.

Two declarations in `android/app/src/main/` make the camera half work, and both fail silently (the gallery opens
instead): the `<queries>` block for `IMAGE_CAPTURE`/`VIDEO_CAPTURE`, without which Android 11+ reports that no camera
app exists, and `res/xml/file_paths.xml`, which must cover the folder the photo is written to. Regenerating the Android
project drops both, so a test asserts they are there.

## Notifications

Browsers and the installed PWA use Web Push; the store apps cannot — there is no service worker inside a native app —
so they go through Firebase Cloud Messaging, which reaches Android directly and iOS through Apple. One queue, one set
of preferences, one sender per platform picked by the device's own `platform` column.

Setting it up, once:

1. **Firebase console** → create a project (analytics not needed) → add an **Android** app with package name
   `com.bouldertime.app` → download `google-services.json`.
2. **GitHub → Settings → Secrets and variables → Actions → Secrets:** `ANDROID_GOOGLE_SERVICES_JSON` = the contents of
   that file. It is written into the build by CI and never committed. The same secret also sets `VITE_PUSH_NATIVE`,
   which is how the app knows whether it may talk to the notification plugin at all: without Firebase the plugin
   throws inside Android and Capacitor turns that into a crash (`Bridge.callPluginMethod` rethrows), so a build
   without the secret shows "this version can't do notifications" instead of a switch.
3. **Firebase console** → Project settings → **Service accounts** → *Generate new private key* → a JSON file.
4. **Railway → Variables:** `Push__ServiceAccountJson` = that JSON on one line. Keep it secret: it can send
   notifications to every BoulderTime device.

**The notification icon** is its own file (`res/drawable-*/ic_stat_bouldertime.png`), declared in the manifest.
Android keeps only a notification icon's silhouette, and the app icon is fully opaque, so without this one every
notification shows a filled square. Regenerating the Android project drops the declaration, so a test asserts it.

iOS additionally needs an APNs key uploaded to Firebase; that comes with the iOS milestone.

## Links that open the app

`https://bouldertime.com/...` links open in the Android app (App Links). Android checks the claim against
`frontend/public/.well-known/assetlinks.json`, which names the app and the SHA-256 fingerprint of its signing key; the
CI build fails if the APK is signed with a key that file doesn't list, because otherwise links would quietly open in
the browser instead. When the app is on Google Play, add Play's app-signing fingerprint (Play Console → App
integrity) to the same list.

Email links go to `/auth/confirm?token_hash=…&type=…` instead of Supabase's default code link: the token is checked
by the server, so the link works in the app, in any browser and on any device. The templates are in
`docs/email-templates/` and have to be pasted into Supabase.

## Signing

Release builds are signed with the upload key from four repository **secrets**: `ANDROID_KEYSTORE_BASE64`,
`ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`. The key itself is kept by the owner, outside
the repository (`*.jks` is git-ignored). Losing it means the app can no longer be updated under the same name. Without
the secrets the workflow falls back to a debug APK.

## iOS

The Xcode project lives in `frontend/ios` (Swift Package Manager, no CocoaPods) and is built by GitHub Actions on a
Mac (`.github/workflows/ios.yml`) on every push that touches `frontend/`. Nobody needs a Mac or Xcode.

**Today, without an Apple developer account,** the build is unsigned and for the simulator: it proves the app
compiles with the current Xcode, and the run leaves `BoulderTime-…-simulator.zip` under **Artifacts**. Unzipped, the
`App.app` inside can be dragged onto the iOS Simulator of any Mac, or uploaded as it is to Appetize.io to try it
from a browser. The simulator has no camera: «Take a photo» offers the photo library and files instead, while an
iPhone opens the camera. It can't be installed on an iPhone: that needs a
signature.

What the project already declares (`ios/App/App/Info.plist`, `App.entitlements`), asserted by
`src/test/ios-project.test.ts` because regenerating the project would drop it:

- **Why it asks** for the camera, the microphone (sound of a recorded video), the photo library and the location.
  iOS closes an app that opens one of these without a reason, and App Review rejects it. The texts are in Italian.
- **No export-compliance form** on every upload (`ITSAppUsesNonExemptEncryption = false`: only standard HTTPS).
- **The claim on `bouldertime.com` links** (Associated Domains). iOS checks it against a file on the website, which
  needs the Apple Team ID, so it is not there yet (see below). Until then links simply open in Safari.
- **iPhone only.** An iPad build would need its own screenshots and review; it can be switched on later.
- BoulderTime's icon and splash, from `frontend/resources/`.

Inside the app the page address is `capacitor://localhost`, which the API has to allow (above).

**Maps on iOS need their own MapTiler key.** The website's key accepts only listed origins, which MapTiler checks
against the `Referer` of each tile request. Android's pages come from `https://localhost`, so adding `localhost` to the
key is enough; iOS serves them from `capacitor://localhost` and WebKit sends no `Referer` from there, so every tile
answers "Invalid key". Create a second key in MapTiler for the app, without origin restrictions (it is inside the app
anyway, like every map key in a store app), keep an eye on its usage, and save the tile URL with that key as the
repository variable `VITE_MAP_TILE_URL_IOS`. The iOS workflow uses it when it is set. The same safe-area
CSS that keeps the installed PWA clear of the notch and the home indicator does it here.

**When the Apple developer account exists** (in this order):

1. Note the **Team ID** (developer.apple.com → Membership details) and register the App ID `com.bouldertime.app`
   with **Associated Domains** (and **Push Notifications**, for the notifications milestone).
2. Add `frontend/public/.well-known/apple-app-site-association` (no extension; served as JSON by `_headers`):
   `{"applinks":{"details":[{"appIDs":["<TEAM_ID>.com.bouldertime.app"],"components":[{"/":"*"}]}]}}`.
3. Add signing to the workflow (distribution certificate and an App Store Connect API key as repository secrets),
   archive with `-sdk iphoneos`, and upload to TestFlight.

**Not yet on iOS:** notifications. The plugin hands back Apple's own token, while the server sends through Firebase
and needs a Firebase token, so the app needs the Firebase Messaging SDK and Firebase needs an APNs key. Until then the
iOS build has `VITE_PUSH_NATIVE` empty and says the version can't do notifications, exactly like an Android build
without `google-services.json`.

## Not yet

The stores themselves (Google Play, then the App Store), iOS notifications and platform-wide reporting come in the
next milestones; see `docs/roadmap.md`.
