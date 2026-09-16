# Build roadmap

| Phase | Scope | Status |
|---|---|---|
| 1 | Foundation: repo, frontend, backend, Supabase auth, base UI, routing, config | **Done** (backend compile + migration pending NuGet access, see below) |
| 2 | Gyms, sectors, staff, invitations, gym candidates, platform admin | Next |
| 3 | Boulders, photos, hold colours, grading systems, official grades, active/removed | |
| 4 | Attempts, completions, activity, ratings, follows | |
| 5 | Comments, likes, grade suggestions, official beta, community videos, moderation | |
| 6 | Notifications, announcements, subscriptions, preferences | |
| 7 | Leaderboards | |
| 8 | Polish: responsive UX, accessibility, states, images, performance | |
| 9 | Tests, seed data, docs, deployment | |

## Phase 1 verification status

| Check | Result |
|---|---|
| Frontend `tsc` strict typecheck | ✅ passes |
| Frontend unit tests (Vitest) | ✅ 7 passing |
| Frontend production build | ✅ passes |
| Backend compile | ⚠️ All source compiled with warnings-as-errors against local stand-ins for EF Core / JwtBearer / IdentityModel types; not yet compiled against the real NuGet packages (package feed unreachable from the build sandbox) |
| Backend tests | ⚠️ Written, not yet executed (need NuGet + Docker) |
| `InitialCreate` migration | ⚠️ Not yet generated (needs `dotnet ef`, see README) |

## Temporary placeholders (must be gone by end of Phase 8)

| Route | Replaced in |
|---|---|
| `/explore` | Phase 2 |
| `/activity` | Phase 4 |
| `/notifications` | Phase 6 |
