# Build roadmap

| Phase | Scope | Status |
|---|---|---|
| 1 | Foundation: repo, frontend, backend, Supabase auth, base UI, routing, config | **Done** — verified in Codespaces (build, migration, 19/19 tests) |
| 2 | Gyms, sectors, staff, invitations, gym candidates, platform admin | **Backend done**; screens next |
| 3 | Boulders, photos, hold colours, grading systems, official grades, active/removed | |
| 4 | Attempts, completions, activity, ratings, follows | **Done** |
| 5 | Comments, likes, grade suggestions, official beta, community videos, moderation | **Done** |
| 6 | Notifications, announcements, subscriptions, preferences | **Done** |
| 7 | Leaderboards | **Done** |
| 8 | Polish: responsive UX, accessibility, states, images, performance | **Done** |
| 9 | Tests, seed data, docs, deployment | Next |

## How each phase is verified

Code is prepared outside the repo, then applied in a GitHub Codespace by an `apply.sh` script that builds the backend
against the real packages, generates the EF migration, runs all backend tests (Testcontainers) and the frontend
typecheck/tests/build, and commits the log. `bash scripts/dev.sh` then runs the full stack for a hands-on look.

## Temporary placeholders

None — every navigation destination is implemented.
