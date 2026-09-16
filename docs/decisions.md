# Architecture decision records

Decisions that shape the data model or long-term architecture. Newest at the bottom.

## ADR-001 · One account, roles as relationships
**Decision.** A single `users` row per person. Gym roles are rows in `gym_staff` (Phase 2); following is
`gym_follows`/`sector_follows`/`boulder_follows`; platform administration is a boolean on `users`.
**Why.** The brief requires one person to be climber, staff at several gyms and admin at once.
**Consequence.** Authorization checks are "does a relationship exist for (user, gym, role)?", evaluated server-side.

## ADR-002 · User id is the Supabase Auth `sub`; lazy provisioning; no FK into `auth`
**Decision.** `users.id` equals the Supabase user id. The row is created on the first authenticated API call.
There is no foreign key from `bouldertime.users` to `auth.users`.
**Why.** No sync jobs or database triggers into Supabase-owned schemas, and the identity provider stays swappable.
**Consequence.** A user exists in BoulderTime only after they've called the API once. Staff invitations (Phase 2) are
therefore addressed to an **email**, and matched when the invitee signs in, so admins can invite people who haven't joined yet.
Display name and avatar are user-owned after creation and are not overwritten from token metadata. Email is kept in sync.

## ADR-003 · Application tables in a private `bouldertime` schema
**Decision.** EF Core creates everything in `bouldertime`, not `public`. Supabase's Data API is not given access.
**Why.** Supabase exposes `public` to the anon key by default. Business rules live in ASP.NET, so direct table
access from the browser would bypass them. This is structural rather than relying on every table having correct RLS.
**Consequence.** The frontend uses Supabase only for authentication and (Phase 3) signed-URL uploads.

## ADR-004 · Platform admins are granted by an operator CLI
**Decision.** `dotnet run -- grant-platform-admin <email>`; no HTTP endpoint and no config-based email allow-list.
**Why.** An email allow-list could be claimed by anyone who registers that address on a project without enforced
email confirmation. A CLI requires database access, which is the right trust boundary.

## ADR-005 · Integration tests against real PostgreSQL
**Decision.** Testcontainers PostgreSQL rather than the EF in-memory provider.
**Why.** Core rules (one rating per user/boulder, one grade suggestion per user/system/boulder, unique follows,
concurrent provisioning) are enforced by unique constraints the in-memory provider ignores. Requires Docker.

## ADR-006 · Planned domain model for Phases 2–5 (recorded now so the foundation doesn't contradict it)
- **Boulders are immutable identities.** Retracing = mark old boulder `REMOVED` (+ `removed_at`) and create a new one. No versioning table. Removed boulders are never deleted; attempts, ratings and comments keep pointing at them.
- **Hold colour ≠ grade.** `boulders.hold_color` is a physical attribute. Grades live only in `boulder_grades(boulder_id, grade_system_id, value, source)`.
- **Grade systems are data, not columns.** `grade_systems(gym_id, name, type, is_active)` with ordered `grade_system_values(grade_system_id, value, sort_order, color_hex?)`. Ordering enables "highest grade" and leaderboard scoring. A colour-based system's `color_hex` describes the *grade* colour and is never reused for holds.
- **Official vs community grading.** `boulder_grades.source ∈ {STAFF}` for official grades; community input lives in `grade_suggestions` with `UNIQUE(boulder_id, user_id, grade_system_id)` and is aggregated on read.
- **One tracking row per user/boulder.** `boulder_attempts` with `UNIQUE(user_id, boulder_id)`; ratings likewise, and rating requires an existing attempt row with `attempts > 0`.
- **Notifications fan out per event, not per entity.** A bulk removal produces one `sector_retraced` event per sector.
