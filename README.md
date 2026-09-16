<p align="center"><img src="frontend/public/assets/logo-horizontal.svg" alt="BoulderTime" height="72"></p>

# BoulderTime

## 1. What BoulderTime is

A platform for indoor bouldering gyms and their climbers. Gyms publish and manage their boulders, grades, beta and
announcements. Climbers discover gyms, follow gyms, sectors and individual boulders, log attempts and sends, rate
problems, suggest grades, watch beta and keep a climbing history that survives retracing.

One account can be a climber, staff at several gyms and a platform administrator at the same time.

> **Build status:** Phase 1 (Foundation) of 9. See [`docs/roadmap.md`](docs/roadmap.md) for what's done and verified.

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

## 3. Prerequisites

- Node.js 20+ and npm
- .NET 8 SDK
- Docker (for the local Supabase stack and backend integration tests)
- Supabase CLI (`npm i -g supabase` or see supabase.com/docs/guides/cli) — for local development
- `dotnet-ef`: `dotnet tool install --global dotnet-ef --version 8.*`

## 4. Supabase setup

**Option A — local (recommended for development)**

    cd database
    supabase init          # first time only; creates database/supabase/config.toml
    supabase start         # prints API URL, anon/publishable key, DB URL

Defaults: API `http://127.0.0.1:54321`, Postgres `127.0.0.1:54322` (user/password `postgres`).

**Option B — hosted project**

1. Create a project at supabase.com.
2. *Project Settings → API*: copy the project URL and the publishable (anon) key.
3. *Project Settings → Database*: copy the **direct** or **session pooler** connection string.
4. *Project Settings → JWT Keys*: new projects sign tokens asymmetrically, which the API verifies via JWKS
   automatically. Only if you are still on the legacy secret, copy it into `Supabase__JwtSecret`.

## 5. Environment variables

| File | Variables |
|---|---|
| `frontend/.env.local` (copy from `frontend/.env.example`) | `VITE_SUPABASE_URL`, `VITE_SUPABASE_ANON_KEY`, `VITE_API_BASE_URL` |
| `backend/.env` (copy from `backend/.env.example`) or `dotnet user-secrets` | `ConnectionStrings__Database`, `Supabase__Url`, `Supabase__JwtSecret` (optional), `Cors__AllowedOrigins__0` |

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

## 9. Running migrations

    cd backend
    # first time only — generates the InitialCreate migration (see note below)
    dotnet ef migrations add InitialCreate \
      --project src/BoulderTime.Infrastructure --startup-project src/BoulderTime.Api \
      --output-dir Persistence/Migrations

    dotnet run --project src/BoulderTime.Api -- migrate

> **Note:** Phase 1 was built in a sandbox without access to the NuGet feed, so the `InitialCreate` migration has not
> been generated and committed yet. It will be committed at the start of Phase 2; until then, run the command above.

## 10. Seeding demo data

Demo data (gyms, sectors, boulders, grading systems, users, attempts, videos, announcements) arrives with the
domains it depends on, starting in Phase 2: `dotnet run --project src/BoulderTime.Api -- seed`.

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

Buckets and access policies (avatars, gym images, boulder photos, official beta, community videos) are created in
Phase 3 via `database/supabase/storage.sql`. Uploads will use short-lived signed URLs issued by the API; binaries are
never stored in PostgreSQL.

## 13. Development workflow

- Work phase by phase (`docs/roadmap.md`). Each phase ends with typecheck, tests and docs updated.
- Change the model → add an EF migration → commit model, migration and snapshot together.
- Backend tests: `cd backend && dotnet test` (Docker must be running; tests start their own PostgreSQL).
- Architecture-affecting decisions get an entry in `docs/decisions.md` before implementation.
- Brand assets: `docs/brand.md`.

## 14. Production deployment considerations

- **Frontend:** static build (`npm run build`) on any CDN host. Configure SPA fallback to `index.html`. Set `VITE_*` at build time.
- **API:** container or App Service–style host running .NET 8 behind HTTPS. Set `ASPNETCORE_ENVIRONMENT=Production`,
  connection string, `Supabase__Url`, and `Cors__AllowedOrigins__*` to the exact frontend origin(s).
- **Migrations:** run `-- migrate` as a release step, not at app startup, then `hardening.sql`.
- **Database connections:** use Supabase's session pooler or direct connection; size the Npgsql pool to your plan's limits.
- **Secrets:** use the host's secret store. Rotate the Supabase secret key if it is ever exposed.
- **Auth keys:** stay on asymmetric JWT signing keys; the API refreshes JWKS every 10 minutes and on unknown key ids, so rotation needs no redeploy.
- **Observability:** every error response carries a `traceId` that matches server logs.
