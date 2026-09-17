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

## ADR-007 · Gym lifecycle, staff permissions and invitations
**Gym lifecycle.** Platform admins create gyms in `DRAFT` (optionally from a gym candidate, which becomes `ACCEPTED` and
is linked). Draft and `ARCHIVED` gyms are invisible to the public and return 404, not 403, to non-members.
`ACTIVE` gyms are discoverable. Slugs are generated once and never change.

**Permission matrix** (platform admins count as OWNER everywhere):

| Action | STAFF | ADMIN | OWNER |
|---|---|---|---|
| View staff & pending invitations | ✓ | ✓ | ✓ |
| Create/edit/reorder/deactivate sectors | ✓ | ✓ | ✓ |
| Edit gym profile | | ✓ | ✓ |
| Invite / revoke STAFF or ADMIN | | ✓ | ✓ |
| Invite / revoke OWNER; change or remove owners | | | ✓ |
| Leave the gym | ✓ | ✓ | ✓ (unless last owner) |

A gym always keeps at least one owner. Every check reads `gym_staff` from the database via `GymAccess`.

**Invitations** are addressed to an email, expire after 7 days, and at most one is open per (gym, email), enforced
by a partial unique index. The invitee accepts in-app after signing in with that email, so accounts can be invited
before they exist. The first owner of a new gym is assigned through the same flow, so nobody becomes staff without
accepting. This relies on email ownership: **keep "Confirm email" enabled in Supabase Auth in production.**
Outgoing invitation emails are not sent yet; invitees see pending invitations on their home screen.

## ADR-008 · Local development stack without the Supabase CLI
**Decision.** `scripts/dev.sh` runs PostgreSQL and Supabase Auth (GoTrue, pinned image) directly with Docker using host
networking (bridge networking between containers proved unreliable in Codespaces), and the
Vite dev server proxies `/api` to the API and `/supabase/auth/v1` to GoTrue, so one forwarded port serves everything.
**Why.** The Supabase CLI's full local stack failed to initialise inside GitHub Codespaces in repeated attempts
(including with a pinned CLI and services disabled). GoTrue is the auth server hosted Supabase runs, so sign-up,
sign-in and token claims (issuer, audience, `sub`, `email`, `user_metadata`) match production; this was verified
end-to-end with supabase-js through the Vite proxy.
**Consequence.** Production and staging still use a hosted Supabase project; nothing in the app changes. Local tokens
are HS256 with a dev-only secret. Local Storage is added in Phase 3 the same way (a pinned container), not via the CLI.

## ADR-009 · Boulders, grading and photo storage
**Boulders are immutable identities.** Retracing = remove + create. `boulders.status` is `ACTIVE`/`REMOVED` with
`removed_at`; rows are never deleted and every FK into a boulder is `RESTRICT`. Removed boulders stay readable by id.
Editing exists only to correct mistakes (wrong photo, grade, sector).

**Hold colour ≠ grade.** `boulders.hold_color` is a platform palette enum (`RED`…`MIXED`) describing the physical holds.
Grades live only in `boulder_grades`. The UI renders grades as solid rectangular badges with the grade written in them,
and holds as a hold-shaped swatch followed by the word "holds"; filters and the editor use separate, labelled controls.

**Grading systems are data.** `grade_systems(gym_id, name, type, sort_order, is_active)` and
`grade_values(grade_system_id, label, rank, color_hex, is_active)`. `rank` orders difficulty (0 = easiest) for "highest
grade" and scoring. `color_hex` exists only on colour-grade values and describes the grade. Values are retired
(`is_active = false`) rather than deleted, so old boulders keep their grade.
Changing systems is ADMIN+; any STAFF member can grade boulders with active systems.

**Official grade = FK to a value.** `boulder_grades(boulder_id, grade_system_id, grade_value_id, source)` with a unique
`(boulder_id, grade_system_id)` for `source = 'Staff'`. Referencing the value (not copying its label) keeps renames
consistent and lets community suggestions (Phase 5) use the same scale.

**Photos go straight to storage.** `POST /api/gyms/{id}/boulder-photos` returns an upload ticket (signed URL, method,
headers, max size). The browser downscales to ≤1600 px JPEG and uploads directly. On create/update the API requires
the path to be under `gyms/{gymId}/boulders/` and to exist in storage. `IObjectStorage` has two implementations:
Supabase Storage (REST with the service-role key, contract taken from supabase/storage source) and a local-disk
implementation with HMAC-signed tickets, used in development and in tests so the whole flow is exercised end to end.

**System-loaded data.** `created_by_user_id` is nullable on boulders and grades: demo seeds and future imports have no
user. Bulk removal returns counts per sector so Phase 6 can send one "sector retraced" update per sector.

## ADR-010 · Climbing progress, ratings, follows and activity
**Tracking.** `boulder_attempts(user_id, boulder_id, attempts, completed, completed_at)` with `UNIQUE(user_id, boulder_id)`
and a check constraint `0 ≤ attempts ≤ 999`. Completing implies at least one attempt; un-completing clears `completed_at`.
A row with zero attempts and no completion is deleted (the API answers 204). Tracking works on removed boulders so
climbers can log or correct sessions after a retrace. Every FK into history is `RESTRICT`.

**Ratings.** `boulder_ratings(user_id, boulder_id, rating)` with `UNIQUE(user_id, boulder_id)` and `CHECK 1–5`.
Rating requires an existing attempt; clearing tracking removes the rating. Averages are computed on read.

**Follows.** `gym_follows` (with `is_favorite`, any number of favourites), `sector_follows`, `boulder_follows`, each unique
per user and independent. Every follow stores `notifications_enabled` for Phase 6 targeting. PUT/DELETE are idempotent.

**Activity is computed on read.** Stats, a 12-week completion chart, history and Home queries run against
`boulder_attempts`. **Highest grade is per grading system**, using `grade_values.rank`; grades from different systems
are never compared. The shared `BoulderReader` builds boulder cards (grades, community rating, viewer progress) with a
fixed number of queries for any list size.

**Client saving.** Attempt taps update the screen immediately and are saved once after 700 ms of inactivity (or on
leaving the page), so fast tapping never produces out-of-order writes.

## ADR-011 · Community content and moderation
**Comments** are flat. `comments(status VISIBLE|HIDDEN)`; authors edit/delete their own (delete is hard, likes cascade);
gym staff hide/unhide (kept for audit and reports). Hidden comments are shown only to their author and the gym's staff.
`comment_likes` has primary key `(comment_id, user_id)`.

**Grade suggestions** `grade_suggestions` with `UNIQUE(boulder_id, user_id, grade_system_id)`, referencing grade values.
Suggesting requires an attempt (like ratings) so the community grade reflects people who tried the boulder. Values must
be active values of this gym's active systems. Consensus is computed on read by `GradeConsensus.Pick` — most votes,
ties broken toward the median then the lower grade — isolated so the rule can change. Official grades are never modified.

**Videos live in private buckets** (`official-beta`, `community-videos`) and are only served through short-lived signed
URLs (1 h) issued per request to viewers allowed to see them: approved → anyone who can see the boulder;
pending/rejected → the uploader (and gym staff via the moderation queue). Supabase: `POST /object/sign/{bucket}/{path}`.
Local development signs read URLs with HMAC and serves them with HTTP range support; read and upload tokens are not
interchangeable.

**Official beta** `boulder_betas` — one per boulder (unique), staff only, no moderation. Replacing deletes the old file.
**Community videos** `boulder_videos(status PENDING|APPROVED|REJECTED, rejection_reason, reviewed_by, reviewed_at)`.
Any change by the author resets to PENDING; the author can always delete. **Nobody reviews their own video** — enforced
in the domain and the service, for staff and platform admins alike. Rejection requires a reason shown only to the author.
Limits: MP4/MOV/WebM, 100 MB, 3 videos per user per boulder. No server-side transcoding yet (see limitations).

**Reports** `reports(entity_type, entity_id, gym_id, reason, status)` with a partial unique index on open reports per
(reporter, entity). `gym_id` is stored at creation so staff moderate their gym's reports and platform admins all reports.
Users can only report content they can see. Resolving with `REMOVE_CONTENT` hides the comment or rejects the video in the
same step. Pending counts appear on the staff overview and the admin dashboard.

**Known limitation.** iPhone videos (HEVC in .mov) may not play in every desktop browser. Transcoding to H.264 MP4 needs a
media pipeline and is deferred.

## ADR-012 · Notifications and gym announcements
**In-app notifications** `notifications(user_id, type, title, body, related_entity_type, related_entity_id, gym_id, link,
collapse_key, count, read_at, created_at)`. Push delivery (web push / native) is a future channel on the same records.

**Targeting is centralised** in `NotificationPublisher`, which adds notifications to the same unit of work as the change
that caused them (one `SaveChanges`, so an announcement and its notifications commit together). Rules for every event:
the actor is never notified; audiences are de-duplicated (one notification per person per event); follow-level
`notifications_enabled` and the user's category switches (`notification_settings`) are respected.

| Event | Audience |
|---|---|
| Gym announcement with "notify followers" | gym followers ∪ followers of the targeted sector |
| New boulder | sector followers → "New boulder in Cave"; gym followers who don't follow that sector → "New boulder at Crimp Factory". Both collapse while unread ("5 new boulders in Cave"); muting a sector also mutes it via the gym |
| Bulk removal with "notify followers" | per sector: sector followers ∪ followers of the removed boulders in it — **one notification per sector per person** |
| Boulder corrected (grade, sector, holds, photo) | boulder followers |
| New official beta (new file, not caption edits) | boulder followers ∪ climbers projecting it |
| New comment | boulder followers — collapsed into one unread item per boulder ("3 new comments") |
| Video approved / rejected | uploader |
| Report resolved / dismissed | reporter |

New boulders notify immediately but collapse per sector (for sector followers) and per gym (for gym followers), so a
setting session yields one notification per person, whose link widens from the boulder to the gym's boulder list once
several are folded in. Edits to an announcement never re-notify.

**Collapse.** Events with a `collapse_key` update the existing unread notification (count, title, time) instead of
inserting another; once read, the next event starts a new one.

**Announcements** `gym_announcements(type, title, content, image_path, event_date, sector_id, notify_followers)`.
Events and competitions require a date. Images use the public `gym-images` bucket with the same upload-ticket flow.
Public for visible gyms; published and edited by STAFF+. Home shows the latest updates from followed gyms.

## ADR-013 · Resumable video uploads (tus)
**Problem.** A single PUT of a 30–50 MB phone video fails easily on mobile networks and through proxies (observed in
development: 6.8 MB succeeded, larger iPhone clips were cut off), and any drop restarts the upload from zero.

**Decision.** Upload tickets for videos include a `resumable` descriptor for the tus 1.0 protocol (creation + core):
endpoint, headers, metadata and chunk size. The browser uses `tus-js-client` with 6 MB chunks and automatic retries;
after an interruption it asks the server for the confirmed offset (HEAD) and continues from there.
- **Supabase Storage:** `POST/PATCH/HEAD {url}/storage/v1/upload/resumable/sign` authorised by `x-signature` = the token of
  the signed upload URL; metadata `bucketName`, `objectName`, `contentType`; Supabase requires exactly 6 MB chunks
  (supabase/storage `src/http/routes/tus`).
- **Local development:** the API implements the same subset at `/api/storage/tus`, verifying the signed ticket on every
  request, matching bucket/object/type, enforcing total size and chunk offsets, and storing the object only when the
  last byte arrives. The whole flow (including resume and wrong-offset rejection) is covered by integration tests.

Photos keep the single PUT: after client-side downscaling they are 1–2 MB. Upload tickets now last 2 hours in both
environments. The app asks users to keep the screen open while uploading, since mobile browsers pause background tabs.
