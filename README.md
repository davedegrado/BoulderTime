<p align="center"><img src="frontend/public/assets/logo-horizontal.png" alt="BoulderTime" height="72"></p>

# BoulderTime

## 1. What BoulderTime is

A platform for indoor bouldering gyms and their climbers. Gyms publish and manage their boulders, grades, beta and
announcements. Climbers discover gyms, follow gyms, sectors and individual boulders, log attempts and sends, rate
problems, suggest grades, watch beta and keep a climbing history that survives retracing.

One account can be a climber, staff at several gyms and a platform administrator at the same time.

> **Build status:** phases 1–8 are complete and verified, together with the founding gym and early-partner
> distinctions and the pre-deployment security hardening. **Phase 9 (seed data, final checks, deployment) is next.**
> Status per phase, and the deliberate limitations, are in [`docs/roadmap.md`](docs/roadmap.md).

## 2. Architecture

React SPA → ASP.NET Core API → PostgreSQL, with Supabase providing Postgres, Auth and Storage. All business logic
and authorization live in the API; the browser uses Supabase only to sign in. Full diagram and layering:
[`docs/architecture.md`](docs/architecture.md). Key decisions: [`docs/decisions.md`](docs/decisions.md).

```
/frontend   React + TypeScript + Vite
/backend    ASP.NET Core 8 Web API (Domain / Application / Infrastructure / Api / Tests)
/database   Supabase-side SQL (schema hardening, storage policies)
/docs       Architecture, decisions, roadmap, brand
```

## See it running (GitHub Codespaces, works from a phone)

Open a Codespace on this repository, then in its terminal:

    bash scripts/dev.sh

It starts PostgreSQL and Supabase Auth in Docker, applies migrations, loads demo gyms, runs the API and the frontend,
and prints a link. (`stop` keeps data, `reset` deletes it.)
Create an account in the app, then run `bash scripts/dev.sh promote` to unlock the staff and admin areas.

## 3. Prerequisites

- Node.js 20+ and npm
- .NET 8 SDK
- Docker (for the local database, auth and the backend integration tests)
- `dotnet-ef`: `dotnet tool install --global dotnet-ef --version 8.*` — only needed to add migrations

The Supabase CLI is not required: `scripts/dev.sh` runs PostgreSQL and Supabase Auth (GoTrue) directly in Docker
(see ADR-008 in [`docs/decisions.md`](docs/decisions.md)).

## 4. Supabase setup

**Option A — local (recommended for development)**

    bash scripts/dev.sh

Starts PostgreSQL (`127.0.0.1:54322`, user/password `postgres`) and Supabase Auth (`127.0.0.1:9999`) in Docker,
applies migrations, seeds demo data and writes `frontend/.env.local` for you. Nothing else to configure.

**Option B — hosted project**

Reusing an existing project is fine: BoulderTime keeps its tables in the `bouldertime` schema and never touches
`public`. `database/supabase/reset-project.sql` lists the optional cleanups (old tables, old buckets, old accounts —
every account that signs in becomes a BoulderTime profile). Pick a European region: it cannot be changed later.

1. Create a project at supabase.com (or reuse one).
2. *Project Settings → API*: copy the project URL and the publishable (anon) key.
3. *Project Settings → Database*: copy the **direct** or **session pooler** connection string.
4. *Project Settings → JWT Keys*: new projects sign tokens asymmetrically, which the API verifies via JWKS
   automatically. Only if you are still on the legacy secret, copy it into `Supabase__JwtSecret`.

## 5. Environment variables

| File | Variables |
|---|---|
| `frontend/.env.local` (copy from `frontend/.env.example`) | `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY`, `VITE_API_BASE_URL`, `VITE_MAP_TILE_URL` and `VITE_MAP_ATTRIBUTION` (optional; required in production, see §14) |
| `backend/.env` (copy from `backend/.env.example`) or `dotnet user-secrets` | `ConnectionStrings__Database`, `Supabase__Url`, `Supabase__JwtSecret` (optional), `Cors__AllowedOrigins__0`, `Storage__Provider`, `Supabase__ServiceRoleKey` (Supabase storage), `Geocoding__*`, `RateLimiting__*` |

Anything prefixed `VITE_` is public. The service-role/secret key, database password and JWT secret are server-only and
must never appear in the frontend or in git.

## 6. Database setup

Application tables live in the `bouldertime` schema, deliberately not exposed through Supabase's Data API. After
migrating, apply the hardening script once:

    psql "postgresql://postgres:postgres@127.0.0.1:54322/postgres" -f database/supabase/hardening.sql

Details: [`database/README.md`](database/README.md).

## 7. Running the backend

    cd backend
    set -a; . ./.env; set +a         # or use dotnet user-secrets
    dotnet run --project src/BoulderTime.Api

API on `http://localhost:5080`, Swagger UI at `/swagger` (Development only), health at `/api/health`.

## 8. Running the frontend

    cd frontend
    npm install
    cp .env.example .env.local       # fill in values
    npm run dev                      # http://localhost:5173

Other scripts: `npm run typecheck`, `npm test`, `npm run build`.

The installable app (service worker, offline shell) is only active in production builds. To try it locally:
`npm run build && npm run preview`.

## 9. Running migrations

All migrations are generated and committed (`backend/src/BoulderTime.Infrastructure/Persistence/Migrations`), from
`InitialCreate` through the founding-gym and early-partner tables. Applying them:

    cd backend
    dotnet run --project src/BoulderTime.Api -- migrate

After changing the model, add a migration and commit model, migration and snapshot together:

    dotnet ef migrations add <Name> \
      --project src/BoulderTime.Infrastructure --startup-project src/BoulderTime.Api \
      --output-dir Persistence/Migrations

## 10. Seeding demo data

    dotnet run --project src/BoulderTime.Api -- seed [--owner you@example.com]

Creates four Italian demo gyms with sectors, grading systems, photos and boulders (active and removed), six demo
climbers with sends spread over the past weeks, projects, ratings, grade suggestions, comments, follows, two
announcements per gym and one report waiting in the moderation queue. Re-running changes nothing.

`--owner` makes an existing user OWNER of all of them. In development, `bash scripts/dev.sh promote` makes every real
account a platform admin and owner of the demo gyms. Demo climbers are skipped, and your own climbing history is left
untouched unless you add `--with-history`.

**Demo climbers have no Supabase account** — nobody can sign in as them. Their email ends in
`@demo.bouldertime.invalid`, so they can be removed in one query (see `database/supabase/reset-project.sql`).

## 11. Authentication setup

- **Email/password:** enabled by default. In production keep *Confirm email* on.
- **Google:** create an OAuth client in Google Cloud Console (type *Web application*), add
  `https://<project-ref>.supabase.co/auth/v1/callback` (or `http://127.0.0.1:54321/auth/v1/callback` locally) as an
  authorized redirect URI, then enable Google under *Authentication → Providers* with the client id/secret.
  Locally, configure `[auth.external.google]` in `database/supabase/config.toml`.
- **Redirect URLs:** add `http://localhost:5173/auth/callback` and your production `https://<domain>/auth/callback` under
  *Authentication → URL Configuration*.
- **Platform admins:** the user signs in once, then an operator runs
  `dotnet run --project src/BoulderTime.Api -- grant-platform-admin you@example.com`.

## 12. Storage setup

Run `database/supabase/storage.sql` once in the hosted project to create the buckets with their size and type
limits: **public** for boulder images, gym images and avatars; **private** for official beta and community videos.
Set `Storage__Provider=Supabase` and `Supabase__ServiceRoleKey` on the API.

Uploads work the same way in every environment: the API checks permissions and returns a short-lived signed upload
URL; the browser shrinks the photo and uploads it directly; the API then verifies the object exists before saving
the boulder. Videos are larger, so they upload in resumable 6 MB chunks (tus) and are read back through short-lived
signed URLs issued only to viewers allowed to watch them. Binaries are never stored in PostgreSQL. In development
(`Storage__Provider=Local`) the API itself plays the role of Storage using a local folder, with the same signed links.

## 13. Platform distinctions

Two badges that belong to a gym and are granted only by BoulderTime administrators (*Admin → Partners*, choosing the
gym by search):

- **Founding gym** — the single gym that launched BoulderTime with us. A partial unique index makes a second one
  impossible; designating one while another holds it is refused rather than silently moved.
- **Early partner** — the early-adopter programme. Several gyms at a time, stored as a period (start, optional end,
  note), so ending one keeps the history.

Staff of a distinguished gym show the badge on their profile; following such a gym grants nothing. The badge is
derived from the staff role, so it disappears by itself when the role or the gym's status ends (ADR-020).

## 14. Security

Verified in the pre-deployment audit (ADR-021): no secrets in the repository or its git history, every write endpoint
authenticated, authorisation enforced in the API (hiding a button is never the control), request rate limits
(`RateLimiting__*`), security headers on every response, signed and ownership-checked uploads, and a post-sign-in
redirect that only accepts same-origin paths. Remaining dependency advisories are in dev-only tooling that never ships.

## 15. Development workflow

- Work phase by phase (`docs/roadmap.md`). Each phase ends with typecheck, tests and docs updated.
- Change the model → add an EF migration → commit model, migration and snapshot together.
- User-visible text goes through `t()`; a test fails if a string has no Italian wording (`frontend/src/i18n`).
- Backend tests: `cd backend && dotnet test` (Docker must be running; tests start their own PostgreSQL).
- Architecture-affecting decisions get an entry in `docs/decisions.md` before implementation.
- Brand assets: `docs/brand.md`.

## 16. Production deployment considerations

- **Frontend:** static build (`npm run build`) on any CDN host. Configure SPA fallback to `index.html`. Set `VITE_*` at build time.
- **API:** container or App Service–style host running .NET 8 behind HTTPS. Set `ASPNETCORE_ENVIRONMENT=Production`,
  connection string, `Supabase__Url`, and `Cors__AllowedOrigins__*` to the exact frontend origin(s).
- **Migrations:** run `-- migrate` as a release step, not at app startup, then `hardening.sql`.
- **Database connections:** use Supabase's session pooler or direct connection; size the Npgsql pool to your plan's limits.
- **Secrets:** use the host's secret store. Rotate the Supabase secret key if it is ever exposed.
- **Auth keys:** stay on asymmetric JWT signing keys; the API refreshes JWKS every 10 minutes and on unknown key ids, so rotation needs no redeploy.
- **Storage:** run `database/supabase/storage.sql` once; video buckets must stay private.
- **Maps:** OpenStreetMap's tiles are for light use only — set `VITE_MAP_TILE_URL` and `VITE_MAP_ATTRIBUTION` to a tile
  provider before going public.
- **Observability:** every error response carries a `traceId` that matches server logs.

Known limitations (push notifications, staff invitation emails, video transcoding, no cross-gym leaderboard) are listed
in [`docs/roadmap.md`](docs/roadmap.md).
