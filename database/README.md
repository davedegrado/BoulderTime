# Database

BoulderTime runs on PostgreSQL provided by Supabase. Ownership is split deliberately:

| Area | Owner | Where |
|---|---|---|
| `bouldertime` schema (all application tables) | EF Core migrations | `backend/src/BoulderTime.Infrastructure/Persistence/Migrations` |
| `auth` schema (credentials, OAuth identities) | Supabase Auth | managed by Supabase |
| `storage` schema + buckets (from Phase 3) | SQL in this folder | `database/supabase/` |
| Demo seed data (from Phase 2, expanded through Phase 9) | Backend seeder | `dotnet run -- seed` |

## Why a separate schema

Supabase auto-publishes the `public` schema through its Data API to anyone holding the anon key. BoulderTime's
authorization lives in the ASP.NET Core API, so application tables live in `bouldertime`, which is not exposed.
`supabase/hardening.sql` additionally revokes access from the `anon` and `authenticated` roles as defense in depth.
Do **not** add `bouldertime` to *Settings → API → Exposed schemas*.

## Applying

    cd backend
    dotnet run --project src/BoulderTime.Api -- migrate
    psql "$DATABASE_URL" -f ../database/supabase/hardening.sql

`hardening.sql` is idempotent; run it after every migration that adds tables (or once — the default privileges cover future tables).
