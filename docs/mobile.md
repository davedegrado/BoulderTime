# Store apps (Capacitor)

The Android and iOS apps wrap the same build as the website. `frontend/` stays the only frontend: Capacitor copies
`frontend/dist` into `frontend/android` (and later `frontend/ios`) and adds a thin native shell.

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

- **API (Railway):** add `Cors__AllowedOrigins__1=https://localhost` so the Android app may call the API.
  iOS will add `capacitor://localhost`.
- **MapTiler:** add `localhost` to the key's allowed origins, or the map stays blank inside the app.

## Versions

`frontend/package.json` `version` is the version people see (`0.1.0`). The build number is the GitHub Actions run
number, so every build is higher than the previous one without editing a file.

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

## Not yet

Google Play, iOS (and its universal links), native push and blocking users come in the next milestones; see ADR-027.
