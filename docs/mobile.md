# Store apps (Capacitor)

The Android and iOS apps wrap the same build as the website. `frontend/` stays the only frontend: Capacitor copies
`frontend/dist` into `frontend/android` and `frontend/ios` and adds a thin native shell.

## What changes when the code runs inside the app

All in `frontend/src/lib/native.ts`:

- **No service worker.** The files are already inside the app; a service worker would keep serving the previous
  version after an update. The website and the PWA still register it.
- **Back button (Android)** goes back through the app, and exits only from the first screen.
- **Status bar** dark text on the light header; **splash screen** (dark wall and logo on iOS, logo on dark on Android: `frontend/resources/splash/README.md`) stays until the first screen is drawn.
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

**iOS** goes through the same Firebase project (ADR-039). Apple hands the app its own APNs token; `AppDelegate.swift`
passes it to Firebase Messaging and gives Capacitor Firebase's token instead, so the web code, the server and the
`NATIVE` platform are the same as on Android. Firebase's own swizzling is off (`FirebaseAppDelegateProxyEnabled`), so
the app delegate stays Capacitor's. Setting it up:

1. **Firebase console** → the same project → add an **iOS** app with bundle ID `com.bouldertime.app` → download
   `GoogleService-Info.plist`. This needs no Apple account.
2. **GitHub → Secrets:** `IOS_GOOGLE_SERVICE_INFO_PLIST` = the contents of that file. Like Android's, it sets
   `VITE_PUSH_NATIVE` for the iOS build and is written into it by CI; without it the build gets an empty file and no
   notifications.
3. **Needs the Apple account:** developer.apple.com → Keys → a key with **Apple Push Notifications service (APNs)** →
   download the `.p8` (only once) → Firebase console → Project settings → Cloud Messaging → Apple app configuration
   → upload it with the Key ID and the Team ID. Turn on **Push Notifications** for the App ID `com.bouldertime.app`.

Until step 3 nothing can arrive on an iPhone: Apple only delivers to signed apps whose App ID allows push, and the
simulator build is unsigned. The app already asks for `aps-environment` (`App.entitlements`); signing for the App Store
turns it into the production environment by itself.

## Links that open the app

`https://bouldertime.com/...` links open in the Android app (App Links). Android checks the claim against
`frontend/public/.well-known/assetlinks.json`, which names the app and the SHA-256 fingerprint of its signing key; the
CI build fails if the APK is signed with a key that file doesn't list, because otherwise links would quietly open in
the browser instead. The list has two fingerprints: the upload key (APKs built by CI, `74:E9:…`) and the key Google
Play signs the store app with (`FE:B7:…`, Play Console → Protected with Play → Play App Signing). Without the second,
links open in the browser for everyone who installed from the store. If Google ever rotates the signing key, add the new
fingerprint here before the rotation reaches devices.

Email links go to `/auth/confirm?token_hash=…&type=…` instead of Supabase's default code link: the token is checked
by the server, so the link works in the app, in any browser and on any device. The templates are in
`docs/email-templates/` and have to be pasted into Supabase.

## Signing

Release builds are signed with the upload key from four repository **secrets**: `ANDROID_KEYSTORE_BASE64`,
`ANDROID_KEYSTORE_PASSWORD`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`. The key itself is kept by the owner, outside
the repository (`*.jks` is git-ignored). Losing it means the app can no longer be updated under the same name. Without
the secrets the workflow falls back to a debug APK.

## Google Play

Every build of `main` goes to a Play **testing track** by itself (ADR-040): `apply.sh` is enough to put a new version
in the testers' hands, and the Play Store updates their phones on its own. What changes only on the server or the
website (API, `assetlinks.json`, legal pages) needs no new app at all. Releasing to everyone is never automatic: in
Play Console, **Test and release → the tested release → Promote to Production**.

Setting it up, once:

1. **Google Cloud console** (console.cloud.google.com), in the Firebase project or a new one → **APIs & Services →
   Library → Google Play Android Developer API → Enable**.
2. **IAM & Admin → Service accounts → Create service account** (for example `github-play-upload`); no roles are
   needed here. Open it → **Keys → Add key → Create new key → JSON**: a file is downloaded.
3. **Play Console → Users and permissions → Invite new users** → the service account's email
   (`…@….iam.gserviceaccount.com`) → **App permissions → BoulderTime** → tick **Release apps to testing tracks** (and
   nothing that touches production, payments or users) → invite. No password, no confirmation email: it's active.
4. **GitHub → Settings → Secrets and variables → Actions → Secrets:** `PLAY_SERVICE_ACCOUNT_JSON` = the whole JSON
   file. Delete the file afterwards; a new key can always be made.
5. Optional **variables**: `PLAY_TRACK` = `internal` (default), `alpha` for the closed test, or the name of another
   closed track; `PLAY_RELEASE_STATUS` = `draft` only while the app has never had a release out.

Each upload needs a higher version code than the last, which the run number already is. A build Play refuses (the
version code already used, a missing declaration in App content) fails the run with Google's message.

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
- **The claim on `bouldertime.com` links** (Associated Domains). iOS checks it against
  `frontend/public/.well-known/apple-app-site-association`, which names `AGXG273RG9.com.bouldertime.app`.
- **iPhone only.** An iPad build would need its own screenshots and review; it can be switched on later.
- BoulderTime's icon, from `frontend/resources/`, and its launch screen (`frontend/resources/splash/README.md`).
- **Notifications:** the `aps-environment` entitlement, Firebase Messaging (Swift Package) and the Firebase settings
  file the workflow writes (see Notifications above).
- **The privacy manifest** (`PrivacyInfo.xcprivacy`): no tracking, and the data the app collects, matching
  `docs/store-privacy.md`.

Inside the app the page address is `capacitor://localhost`, which the API has to allow (above).

**Maps on iOS need their own MapTiler key.** The website's key accepts only listed origins, which MapTiler checks
against the `Referer` of each tile request. Android's pages come from `https://localhost`, so adding `localhost` to the
key is enough; iOS serves them from `capacitor://localhost` and WebKit sends no `Referer` from there, so every tile
answers "Invalid key". Create a second key in MapTiler for the app, without origin restrictions (it is inside the app
anyway, like every map key in a store app), keep an eye on its usage, and save the tile URL with that key as the
repository variable `VITE_MAP_TILE_URL_IOS`. The iOS workflow uses it when it is set. The same safe-area
CSS that keeps the installed PWA clear of the notch and the home indicator does it here.

### App Store and TestFlight (ADR-045)

Team ID `AGXG273RG9`, bundle id `com.bouldertime.app`. The iOS workflow always builds for the simulator; with the
secrets below it also archives for iPhones, signs with Apple Distribution, exports an `.ipa` (kept 14 days under the
run's Artifacts, with its dSYMs) and, from `main`, uploads it to App Store Connect. Version = `package.json`, build
number = the run's number (a "Re-run" repeats it and Apple refuses a duplicate: push again or use "Run workflow").

**Once, on Apple's sites:**

1. developer.apple.com → Certificates, IDs & Profiles → Identifiers: the App ID `com.bouldertime.app` with
   **Push Notifications** and **Associated Domains** on. Create the APNs key and upload it to Firebase
   (Notifications, step 3).
2. The **Apple Distribution certificate**, without a Mac, from the Codespace:
   ```bash
   openssl genrsa -out dist.key 2048
   openssl req -new -key dist.key -out dist.csr -subj "/CN=BoulderTime Distribution/C=IT"
   ```
   Certificates → + → **Apple Distribution** → upload `dist.csr` → download `distribution.cer`, then:
   ```bash
   openssl x509 -inform DER -in distribution.cer -out dist.pem
   openssl pkcs12 -export -legacy -inkey dist.key -in dist.pem -out dist.p12   # asks for a password: keep it
   base64 -w0 dist.p12 > dist.p12.b64
   ```
   `-legacy` matters: without it the Mac's keychain can't read the `.p12`. Delete `dist.key`, `dist.p12` and the
   rest from the Codespace once the secrets are saved; never commit them.
3. Profiles → + → **App Store Connect** → App ID `com.bouldertime.app` → the certificate above → a name such as
   "BoulderTime App Store" → download it, then `base64 -w0 BoulderTime_App_Store.mobileprovision > profile.b64`.
   Make it again whenever a capability changes or the certificate is renewed (every year).
4. appstoreconnect.apple.com → Apps → + → New App: iOS, name BoulderTime, bundle id `com.bouldertime.app`, any SKU.
5. Users and Access → Integrations → App Store Connect API → + : role **App Manager**. Note the Key ID and the Issuer
   ID and download the `.p8` (only once).

**Repository secrets** (Settings → Secrets and variables → Actions → Secrets):

| Secret | What |
|---|---|
| `IOS_DIST_CERT_P12_BASE64` | contents of `dist.p12.b64` |
| `IOS_DIST_CERT_PASSWORD` | the `.p12` password |
| `IOS_PROVISIONING_PROFILE_BASE64` | contents of `profile.b64` |
| `APP_STORE_CONNECT_API_KEY_ID` | the key's Key ID |
| `APP_STORE_CONNECT_API_ISSUER_ID` | the Issuer ID |
| `APP_STORE_CONNECT_API_KEY_P8` | the whole `.p8` file, `-----BEGIN PRIVATE KEY-----` lines included |
| `IOS_GOOGLE_SERVICE_INFO_PLIST` | the Firebase settings for iOS (Notifications) |

Without the first two the run stays simulator-only; without the API key it builds and keeps the `.ipa` but doesn't
upload. The workflow checks the profile before using it (team, bundle id, App Store type, production push,
Associated Domains) and says exactly what is wrong. Each secret reaches only the step that needs it; the certificate
goes into a keychain made for the run with a random password, and keychain, profile and key are deleted at the end
even when a step fails. Runs from other people's pull requests never see secrets.

**After the first upload:** App Store Connect → the app → TestFlight. The build appears after Apple's processing
(10–30 minutes). Internal testers (people in your App Store Connect team) can install it at once from the TestFlight
app; external testers need a short "Test Information" and Apple's beta review. The export-compliance question is
already answered by `Info.plist`.

## Sign-in

Email and password only. Google sign-in is built (`AuthProvider.signInWithGoogle`, `GoogleButton`) but kept out of
the app: on the free Supabase plan Google's consent screen names `<ref>.supabase.co` instead of BoulderTime. Offering
it again on iOS also means offering **Sign in with Apple** (App Store guideline 4.8), so the two go together.

## Before the stores

- **Account deletion without the app:** <https://bouldertime.com/delete-account>, the page Google Play asks for, in
  Italian and English (flags at the top, or `?lang=en`). Both texts must stay in step with `AccountEraser`.
- **Privacy forms:** the answers for Play's Data safety and Apple's App Privacy are in `docs/store-privacy.md`.
- What is left is in `docs/roadmap.md`.
