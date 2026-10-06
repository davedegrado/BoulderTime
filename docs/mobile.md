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

## Not yet

Signed release builds, Google Play, iOS, native push, universal links and blocking users come in the next
milestones; see ADR-027.
