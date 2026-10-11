# Build roadmap

Status reflects the code in this repository, verified by the checks listed in the last column — not by intent.
Counts come from the most recent `apply.sh` run recorded in `apply-log.txt`.

## Shipped

| Phase | Scope | Status | Verified by |
|---|---|---|---|
| 1 | Foundation: repo, frontend, backend, Supabase auth, base UI, routing, config | **Done** | build, migration, backend tests |
| 2 | Gyms, sectors, staff, invitations, gym candidates, platform admin | **Done** | role/permission tests, staff and admin screens |
| 3 | Boulders, photos, hold colours, grading systems, official grades, active/removed | **Done** | boulder, grading and upload tests |
| 4 | Attempts, completions, activity, ratings, follows | **Done** | climbing and follow tests |
| 5 | Comments, likes, grade suggestions, official beta, community videos, moderation | **Done** | community, moderation and signed-URL tests |
| 6 | Notifications, announcements, subscriptions, preferences | **Done** | notification targeting and collapse tests |
| 7 | Leaderboards | **Done** | scoring, period and ranking tests |
| 8 | Polish: images, installable app, accessibility, performance, maps, video previews | **Done** | image, map and polish tests |
| 9 | Seed data, final checks, docs, **deployment** | **Done** — live at [bouldertime.com](https://bouldertime.com) | health check, deployed stack |
| — | Italian and English throughout (ADR-018, ADR-019) | **Done** | translation coverage test |
| — | Founding gym and early partners (ADR-020) | **Done** | authorisation, uniqueness and badge tests |
| — | Security hardening (ADR-021) | **Done** | open-redirect, rate-limit and header checks |
| — | Leaderboard opt-out, reports and exclusion (ADR-022) | **Done** | visibility and moderation tests |
| — | Push notifications on the web and the installed PWA (ADR-023) | **Done** | encryption round-trip, subscription tests |
| — | Account deletion from the profile, seven-day grace (ADR-024) | **Done** | deletion, grace and erasure tests |
| — | Privacy notice and terms, acceptance recorded with its version (ADR-025, ADR-026) | **Done** | promise-matching and acceptance tests |
| — | Per-gym video allowance: climber videos off by default and hidden where off, beta cap (ADR-026) | **Done** | allowance, cap, clean-up and hidden-section tests |
| — | Boulders can go up ungraded and be graded later | **Done** | ungraded-send and scoring tests |
| — | The official beta can be a link to Instagram/YouTube instead of an upload (ADR-032) | **Done** | address-validation, allowance and swap tests |
| — | Taken-down boulders stay visible to climbers; gyms can erase one for good (ADR-035) | **Done** | visibility, history-survival and permission tests |
| — | Public account-deletion page, `/delete-account`, Italian and English (Google Play requirement) | **Done** | deletion page tests |
| — | Sign-up asks for age (at least 14) and terms on the form itself; boulder page with a smaller photo that opens full screen (ADR-041) | **Done** | sign-up, legal-acceptance and gate tests |
| — | Beta card owned by the gym, community grade presented plainly, compact gym page (ADR-042) | **Done** | beta, community-grade and gym-page tests |
| — | Home with favourite gyms first, profile with account details on request, password fields that can be shown, assorted labels | **Done** | home, password and activity tests |
| — | Reference grade scales every boulder can be voted in (ADR-043) | **Done** | consensus and ordering tests |
| — | Sector map: the gym's floor plan with its sectors drawn on it, names optional (ADR-044) | **Done** | zone, permission and map tests |
| — | Card picture chosen by cropping the photo; dark theme; in-app help and FAQ (`/aiuto`); test notification | **Done** | cropper, theme, help and push tests |
| — | Gym tags and boulder intensity, with filters (ADR-046) | **Done** | tag uniqueness, ownership, filter and validation tests |
| — | "Completed by N people" with the list of names, and the climber's most climbed sector (ADR-047) | **Done** | counting, undo, privacy and favourite-sector tests |

## Store apps (in progress)

One codebase: the website, the installable PWA and the store apps are the same React build (ADR-027).

| Milestone | Scope | Status |
|---|---|---|
| M1 | Capacitor, Android project, no service worker in the app, back button, status bar, splash, icons, APK built by CI | **Done** |
| M2 | App Links, release signing, email links that work on any device (ADR-028) | **Done** |
| M3 | Camera or gallery inside the app (ADR-029) | **Done** |
| M4 | Native push through Firebase (ADR-030) | **Done** — needs a Firebase project |
| M5 | Blocking a user (ADR-031) | **Done** |
| M6 | iOS: Xcode project built in CI, universal links, signed builds | **Done**: simulator build, signed archive and TestFlight upload from `main` (ADR-036, ADR-045) |
| M6c | iOS notifications: Firebase Messaging in the app, APNs key in Firebase (ADR-039) | **Done**: delivered on TestFlight builds; a test notification in the settings reports what Firebase answered |
| M6b | Reporting a person and suspending an account, platform-wide (ADR-037); deleting a suspended account no longer escapes it (ADR-038) | **Done** — Apple 1.2's "block abusive users **from the service**" |
| M6d | Store readiness fixes (ADR-048): coarse location, word filter, video limit and posters, deletion hides at once, 16 KB check, permission texts in English and Italian; video uploads behind a server switch, off for launch (ADR-049); report emails, zero-tolerance terms, notifications offered at sign-in, native location (ADR-050) | **Done in code**; console steps and device tests in `docs/store-checklist.md` |
| M7 | The stores themselves: Google Play, then App Store. Privacy answers ready (`docs/store-privacy.md`), deletion page live | **Google Play:** in internal testing; links verified for the store app; every build of `main` uploaded to a testing track (ADR-040). Next: closed test, then production. **App Store:** TestFlight builds from `main`; store listing and review next |

## Planned, deliberately not started

- **Videos on iOS.** Checked on devices (October 2026): short videos picked or filmed on iPhone upload and play, and
  play on Android with sound. Found and fixed (ADR-048): posters refused by storage, Android tiles without a frame,
  a 100 MB limit above what storage takes. Uploads are switched off for launch (ADR-049) and come back with a paid
  storage plan (about 10–15 paying gyms): then raise the limit, and decide on conversion of `.mov`/HEVC for desktop
  browsers that can't play it.
- **Under-14 climbers.** A child profile created and used by a parent from their own account, with no login, public
  profile, leaderboards, comments or videos of its own; proposed, waiting for a legal review and the choice of markets.
- **Google sign-in (and then Sign in with Apple).** Built but switched off: see `docs/mobile.md` → Sign-in. Comes back
  with a paid Supabase plan and its custom auth domain.

## Known limitations (deliberate, not forgotten)

- **Staff invitation emails** are not sent: invitations appear in the app after signing in with the invited address.
- **Video transcoding** is not done yet (planned), so an iPhone `.mov` (HEVC) may not play in every desktop browser
  (it does play in the Android app, tested October 2026).
- **The privacy notice and terms are drafts** awaiting a lawyer's review, and exist in Italian only.
- **No cross-gym leaderboard:** grades from different gyms are not comparable, so it is intentionally absent.
- **Supabase free tier:** no point-in-time recovery; daily backups only. Every upload is capped at 50 MB (hence the
  video limit), storage is small next to video, and inactive free projects can be paused: check the current terms on
  the plan page before opening to the public.

## How each phase is verified

Code is prepared outside the repo, then applied in a GitHub Codespace by an `apply.sh` script that builds the backend
against the real packages, generates the EF migration, runs all backend tests (Testcontainers) and the frontend
typecheck/tests/build, and commits the log. `bash scripts/dev.sh` then runs the full stack for a hands-on look.
The Android app is built by GitHub Actions (`.github/workflows/android.yml`), which attaches an installable APK to
every run.

## Temporary placeholders

None — every navigation destination is implemented.
