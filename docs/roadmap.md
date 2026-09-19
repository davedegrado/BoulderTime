# Build roadmap

Status reflects the code in this repository, verified by the checks listed in the last column — not by intent.
Counts come from the most recent `apply.sh` run recorded in `apply-log.txt`.

| Phase | Scope | Status | Verified by |
|---|---|---|---|
| 1 | Foundation: repo, frontend, backend, Supabase auth, base UI, routing, config | **Done** | build, migration, backend tests |
| 2 | Gyms, sectors, staff, invitations, gym candidates, platform admin | **Done** — backend and screens | role/permission tests, staff and admin screens |
| 3 | Boulders, photos, hold colours, grading systems, official grades, active/removed | **Done** | boulder, grading and upload tests |
| 4 | Attempts, completions, activity, ratings, follows | **Done** | climbing and follow tests |
| 5 | Comments, likes, grade suggestions, official beta, community videos, moderation | **Done** | community, moderation and signed-URL tests |
| 6 | Notifications, announcements, subscriptions, preferences | **Done** | notification targeting and collapse tests |
| 7 | Leaderboards | **Done** | scoring, period and ranking tests |
| 8 | Polish: images, installable app, accessibility, performance, maps, video previews | **Done** | image, map and polish tests |
| — | Italian and English throughout (ADR-018, ADR-019) | **Done** | translation coverage test |
| — | Founding gym and early partners (ADR-020) | **Done** | authorisation, uniqueness and badge tests |
| — | Security hardening (ADR-021) | **Done** | open-redirect, rate-limit and header checks |
| 9 | Seed data, final checks, docs, deployment | Next | — |

## Known limitations (deliberate, not forgotten)

- **Push notifications** to a closed app are not implemented; the notification records and preferences that feed them are.
- **Staff invitation emails** are not sent: invitations appear in the app after signing in with the invited address.
- **Video transcoding** is not done, so an iPhone `.mov` (HEVC) may not play in every desktop browser.
- **No cross-gym leaderboard:** grades from different gyms are not comparable, so it is intentionally absent.
- **OpenStreetMap tiles** are for light use; production must set `VITE_MAP_TILE_URL` to a tile provider.

## How each phase is verified

Code is prepared outside the repo, then applied in a GitHub Codespace by an `apply.sh` script that builds the backend
against the real packages, generates the EF migration, runs all backend tests (Testcontainers) and the frontend
typecheck/tests/build, and commits the log. `bash scripts/dev.sh` then runs the full stack for a hands-on look.

## Temporary placeholders

None — every navigation destination is implemented.
