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

## ADR-014 · Video thumbnails and browsing many videos
**Browsing.** Approved community videos are paged by the API (12 per page, newest approval first) and shown as a
horizontal thumbnail rail (swipe or arrow buttons, "Show more" tile). Players are only created when a video is opened,
in a full-screen viewer with previous/next (keyboard arrows and Escape on desktop); reaching the end loads the next page
and advances automatically. The viewer's own pending/rejected videos are returned separately (`mineInReview`) and shown
with their status, so they never mix with published videos.

**Thumbnails are captured on the uploading device**: a frame ~1 s in (or a third of short clips), max 480 px JPEG,
uploaded with a small single-PUT ticket (`*_THUMBNAIL` kinds, JPEG/WebP ≤ 1 MB) into the same PRIVATE bucket and boulder
folder as the video, and served through signed URLs like the video. `thumbnail_path` is optional: if the browser can't
decode the file (e.g. some HEVC .mov on desktop) or capture times out, the video uploads without one and the UI shows a
placeholder. Replacing or deleting a video deletes its thumbnail. Server-side frame extraction would need a media
pipeline and remains deferred together with transcoding.

## ADR-015 · Per-gym leaderboards computed on read
**Scope.** Leaderboards are per gym. Grades of different gyms (or a colour grade vs a Font grade) aren't honestly
comparable, so there is no global ranking.

**Metrics.** `POINTS` (difficulty-weighted), `COMPLETED` (number of sends), `HIGHEST` (hardest send in the gym's primary
scale). **Periods:** `WEEK` (from Monday, UTC), `MONTH`, `YEAR`, `ALL`. A send counts in the period of its completion
date, including boulders removed since. Only public profiles appear (the viewer always sees their own position).

**Scoring is isolated** behind `IClimbScoring`. Default `RankBasedScoring`: a send is worth
`10 + 90 × rank / (scale size − 1)` points in the gym's primary system (first active system), falling back to the next
system if the boulder has no grade in it. Normalising by scale size keeps a 6-colour scale and a 23-step Font scale on
the same 10–100 range. Attempts don't change points (flash/redpoint aren't tracked).

**Ranking.** Primary value per metric, then points/sends, then who got there first. Equal primary values share a
position (1, 2, 2, 4). The API returns the top N (default 50, max 100) plus the viewer's own entry when outside it.

**No stored leaderboards.** Computed from `boulder_attempts` on request (cached 60 s on the client). If a gym's volume
makes this slow, the same service can read from a materialised view without changing the API.

## ADR-016 · Polish: images, installable app, performance, accessibility
**Images.** Avatars (`avatars`, `users/{id}/avatar/…`, square 512 px) and gym logo/cover (`gym-images`,
`gyms/{id}/logo|cover/…`; logo square 512 px, cover ≤1600 px; ADMIN+) use upload tickets and are prepared on the device.
The API checks each object exists under the owner's folder, stores the public URL plus the storage path, and deletes
replaced files. Boulder photos now also get a ≤480 px `.thumb.` version generated on the device; lists use it and the
detail page uses the full photo. Thumbnails are best-effort (the boulder saves without one) and can never be used as
the main photo.

**Installable app.** `vite-plugin-pwa` generates a service worker in production builds only (not the dev server):
precached app shell (~1.1 MB, large store icons excluded), cache-first public images (immutable paths, 30 days), and
navigation fallback to the shell. **API responses are deliberately not cached**: they are personal, and a shared phone
must never show one account's data to another. An offline banner explains that already-loaded content still shows.

**Performance.** Staff, admin and rarely used screens are lazy-loaded route chunks; the tus upload client loads only
when a video upload starts. Main chunk 534 → 474 KB (130 KB gzipped).

**Accessibility & robustness.** After client-side navigation the document title follows the page heading and focus
moves to the main region (announced by screen readers). The video viewer traps focus and restores it on close.
Touch targets are at least 44 px. Unexpected render errors and failed chunk loads show a recoverable error screen
instead of a blank page.

## ADR-017 · Gym map and video previews
**Gym locations.** `gyms.latitude/longitude` (WGS84, optional, both or neither, indexed). Staff (ADMIN+) set them in gym
settings: "Find from address" calls the API, which geocodes through `IGeocoder` (OpenStreetMap Nominatim: ≤1 request/s,
identified User-Agent, optional contact email; `Geocoding:Provider=None` disables it), then they drag the pin onto the
entrance. The result is only a suggestion; nothing is geocoded automatically or in bulk.

**Explore.** A Leaflet map centred on the browser's position (asked once; last position remembered on the device; Italy
when denied). Pins come from `GET /api/gyms/map?south&west&north&east` (active gyms with coordinates, max 500, handles
the antimeridian). With a position, `GET /api/gyms?lat&lng` orders results by distance (equirectangular ordering in
SQL, Haversine `distanceKm` on the page) with unlocated gyms last. Leaflet loads lazily, only where a map is shown.

**Tiles.** Default OpenStreetMap tiles are for light use only. Production must set `VITE_MAP_TILE_URL` and
`VITE_MAP_ATTRIBUTION` to a tile provider (e.g. MapTiler, Stadia, Carto).

**Video previews.** (1) The uploader plays the chosen file before sending. (2) Thumbnail capture now briefly plays the
muted inline video before seeking, which iOS Safari requires to decode frames, and rejects black frames. (3) Videos
without a thumbnail show a live frame (`#t=0.5`, metadata preload, loaded only when near the viewport) instead of an icon.

## ADR-018 · Italian and English (i18n)
**Italian is the default**; English is chosen in the profile. The choice is stored on the device
(`localStorage`) and on the account (`users.language`), so notifications reach each person in their own language.
The app sends `Accept-Language`, which also seeds the language when an account is first created.

**Frontend.** A small provider; `t("English source text")` and `plural(count, one, other)` are plain functions, not
hooks, so any module can translate without restructuring — the provider remounts the tree when the language changes
(a rare action). **English source text doubles as the key**, so a missing entry degrades to readable English instead of
an identifier; missing entries are logged in development. `src/i18n/it.ts` holds the Italian wording. Labels that used
to be constant maps (hold colours, report reasons, announcement types, leaderboard metrics) are read at render time.
Dates, times and relative times use the active locale.

**Backend.** `Translations` holds the Italian version of every message the API returns, matched on the English text
with `{0}` for runtime values (limits, names). Translation happens once, in the error handler, using the caller's
`Accept-Language`; anything unmatched stays English. **Notifications are written per recipient in that recipient's
language** (`NotificationTexts`): the publisher renders the text once per language present among the recipients, so an
English staff member's announcement arrives in Italian for Italian climbers, including collapsed ones
("2 nuovi blocchi in Cave"). Boulder changes travel as codes, not English phrases, so each reader sees their own wording.

**Staff and admin areas** are translated too: screens, toasts, confirmations and shared labels (roles, gym status,
suggestion status), which are read at render time so they follow the language in use. Wording follows what Italian
gyms say: *tracciare/ritracciare* for setting and resetting, *settore*, *presa*, *grado*, *blocco*.

## ADR-019 · Translating names that come from gym data
Grading system names and grade labels live in the database, but some of them are generated by BoulderTime itself
(the preset system names "Colour"/"V-scale" and the colour grade labels White…Black). `i18n/data.ts` translates exactly
those and leaves everything a gym typed untouched, so "Parete Rossa" stays as written. It is kept apart from the
interface dictionary because the same word needs a different form in each: a grade label is the colour itself
("Rosso"), while a hold colour is an adjective agreeing with "prese" ("prese rosse").

Boulder photos no longer force the camera: the file input dropped `capture`, so staff who photographed the wall
earlier can upload from the phone's library. A test asserts the attribute is absent.

## ADR-020 · Founding gym and early partners
Two distinctions that belong to the **gym**, are granted only by platform admins, and are never stored on users.

**Founding gym** — `gyms.is_founding_gym`. A historical distinction held by exactly one gym: a partial unique index
(`WHERE is_founding_gym`) makes a second one impossible even if application code were wrong, and designating a gym
while another holds it returns 409 naming the current holder, so the distinction is never moved by accident.

**Early partner** — table `early_partnerships(gym_id, started_at, ended_at, note, granted_by)`. Several gyms can hold
it at once and it is a *period*, not a flag: ending it keeps the row, so history survives. A partial unique index
(`WHERE ended_at IS NULL`) allows only one running partnership per gym. Billing is out of scope.

**Badges on people are derived, never stored:** `User → GymStaff → Gym → distinction`. Following a distinguished gym
grants nothing; leaving the staff, or the gym losing the status, removes the badge with no extra bookkeeping.
Wording goes through i18n ("Palestra Fondatrice" / "Fondatrice" where space is tight, "Early Partner" in both languages).

## ADR-021 · Security hardening (pre-phase-9 audit)
- **Open redirect fixed.** The post-sign-in destination accepted `/\evil.example`, which browsers treat like
  `//evil.example` and follow off-site. Only plain same-origin paths are accepted now; backslashes, protocol-relative
  paths and control characters are refused, with tests.
- **Rate limits** (`RateLimiting:*`): 300 requests/minute per signed-in user or IP, and 60/minute on endpoints that
  create content or start uploads. Disabled in the test environment.
- **Security headers** on every API response: `nosniff`, `DENY` framing, `no-referrer`, `same-site` resource policy and
  a restrictive permissions policy.
- **Storage bucket SQL is versioned again** (`database/supabase/storage.sql`): `.gitignore` had excluded the whole
  `database/supabase/` folder, so a file the deployment depends on was missing from the repository.
- **Dependency audit:** the remaining advisories are in dev-only tooling (Vite dev server on Windows, Vitest UI server),
  neither of which ships or runs in production; fixing them requires major upgrades and is scheduled separately.
  The one advisory affecting a shipped library (react-router open redirect) is mitigated by the check above.

## ADR-022 · Who appears in leaderboards
Two independent switches, both stored on the user: `leaderboard_opt_out` (the climber's own choice) and
`leaderboard_excluded_at` (set by a BoulderTime administrator). Either one removes the person from every board.

**Hidden means absent, not zero.** A zero-point row would still publish the name next to an implausible-looking
result; the row simply isn't there.

**Gyms report, BoulderTime decides.** `leaderboard_reports` holds a gym's flag with a reason; staff of that gym can
create one (never on themselves, one open report per gym and climber) but cannot exclude anyone — a gym judging its
own members would turn a ranking dispute into a membership dispute.

**An excluded climber is told, without a reason.** Their own profile says they don't appear and points at
support@bouldertime.com. Saying nothing would read as a broken app and send the complaint to the gym; saying why
would start an argument with staff who only reported. Nobody else sees the exclusion.

Gym profiles also carry optional Instagram and Facebook links. Handles and full links are both accepted, anything
that isn't a link to that network is refused, so the profile can't be used to send climbers elsewhere.

## ADR-023 · Push notifications
Phone notifications use Web Push, so there is no app store and no third-party notification service: the message goes
from our API to the browser vendor's push service, encrypted end to end. The push service sees ciphertext only.

**No external library.** VAPID signing (RFC 8292) and payload encryption (RFC 8291, aes128gcm) are implemented on
.NET's own cryptography, about 80 lines, and verified by a test that decrypts a real message the way a browser would
and checks another device's keys cannot. One less dependency to trust in the path that handles user data.

**Only gym and sector news is pushed:** new boulders, retraces, announcements and events. Comments, likes and
moderation outcomes stay in the app — a buzzing phone is a cost, and those don't repay it. The existing per-category
preferences decide both, so turning a category off silences it everywhere.

**Sending happens outside the request** through a bounded queue and a background dispatcher: setting twenty boulders
must not wait for twenty push services, and a lost notification is better than a stuck request. Devices that report
themselves gone, or fail five times, are removed.

**On iPhone, push works only once the app is on the home screen** (an Apple restriction). The settings screen says so
instead of showing a switch that would do nothing.

## ADR-024 · Deleting an account
The person deletes their own account from the profile; nobody has to write an email and wait.

**Two frictions on purpose.** The address has to be typed out (a plain "are you sure?" is tapped by accident), and
the data survives **seven days** before being erased, so a decision taken in a bad moment can be undone. The account
leaves the leaderboards immediately: being on the way out shouldn't leave you ranked.

**Erasure anonymises rather than cascades.** Everything personal is deleted — name, email, photo, attempts, sends,
ratings, grade suggestions, comments, likes, community videos including the stored files, follows, notifications,
registered devices, staff memberships — and the Supabase sign-in is removed so the account cannot come back. The user
row survives as a nameless placeholder because the boulders that person set and the official beta they filmed belong
to the gym, and deleting the row would take the gym's own content with it.

**The only owner of a gym can't leave** without handing ownership over: otherwise a gym would be stranded with no one
able to manage it.

A daily background sweep does the erasing, three minutes after start and then every 24 hours: a deadline nobody
checks is not a promise.

## ADR-025 · Privacy notice and terms
Both are written against what the app does, not from a template, and both live in the repository
(`frontend/src/pages/legal/`) so they change with the code rather than drifting from it. A test asserts that the
promises they make are ones the app keeps: the seven-day deletion window, the 14-year age limit, the leaderboard
opt-out, and the list of services that process data.

**Controller:** Davide Luisi, Modena, reachable at support@bouldertime.com. Still a person rather than a company,
which is accurate today and takes five minutes to change later.

**Videos rest on consent**, and the uploader declares they have the consent of anyone recognisable. Anyone filmed can
ask for removal without giving a reason. This is the part of the app most likely to cause real harm, so it is stated
in both documents and in the sign-up flow.

**Retention is what the app enforces:** account data until deletion plus seven days, rejected videos 30 days,
technical logs about 90. Promising shorter periods than the code delivers would be worse than promising nothing.

**A tick, recorded with its version.** The first time someone signs in they must tick "I have read and accept"
before the app opens; the acceptance is stored with the version of the documents they saw
(`users.accepted_legal_version` and `accepted_legal_at`). The server refuses an acceptance for any version other than
the current one, so an app showing old text cannot record agreement to new terms, and raising
`LegalDocuments.CurrentVersion` asks everyone again — which is why bumping it is a decision, not a side effect of
editing a typo. This replaces the earlier plan of an implicit line at sign-up: an app holding videos of people who
never signed up needs to be able to show what the uploader agreed to, and the account does not exist yet at sign-up,
so there would be nowhere to record it. Minors under 14 are not allowed to register; there is no age
verification, because none is meaningful and claiming one would be a lie.

**These are drafts.** They need a professional's review before the app is opened to the public.

## ADR-026 · How much video a gym may keep
Video is the one cost that grows without anyone deciding to, so the allowance is per gym and set by BoulderTime.

**Climber videos are off for a new gym.** The feature stays visible on the boulder, locked, with a line saying it
isn't open at this gym yet: a gym being shown the app should see what is coming, and a climber should not wonder
where the button went. The API refuses the upload too — a hidden button is not a limit.

**Official beta has a per-gym cap**, 20 by default, `null` for no limit. Replacing the beta of a boulder that already
has one is always allowed, because it costs no extra storage. The cap is checked before an upload ticket is issued,
so nothing lands in storage that the gym may not keep.

**Removing a boulder deletes its official beta**, file and thumbnail. The video showed a route that is no longer on
the wall, it is the heaviest thing stored for that boulder, and deleting it frees a slot in the gym's allowance.
Climbers' own videos are left alone: they belong to the people who filmed them.

The numbers behind this: a 30-second beta is 20–40 MB, and Supabase's free tier holds 1 GB. Five gyms uploading
twenty videos a week would fill it in a month, so the allowance exists to make that a decision rather than a surprise.

## ADR-027 · Store apps with Capacitor
The store apps wrap the same React build as the website. No second frontend, no rewrite: Capacitor copies
`frontend/dist` into native projects that live under `frontend/android` (and `frontend/ios` later).

**First milestone: an installable Android app** built by GitHub Actions, so neither Android Studio nor a Mac is
needed. It ships the web app unchanged plus four native behaviours: no service worker inside the app, Android's back
button, status bar and splash screen.

**Native code is reached through one module** (`lib/native.ts`). Plugins are imported only when running natively, so
the website and the PWA don't load them; the only cost on the web is the few lines that answer "am I in an app?".

**Links that leave the app use the website's address.** Inside the app the page is `https://localhost`, which an email
cannot point at; confirmation and reset links therefore go to `https://bouldertime.com`. Universal links (next
milestone) will make those open the app directly. The same reasoning fixed one real bug: resumable upload endpoints
were resolved against the page address and would have pointed at `localhost` inside the app.

**Next milestones, in order:** universal and app links; native push through Firebase (Web Push does not work inside
an app); blocking users, which Apple requires for apps with user content; signed release builds and the stores. The
native camera plugin is not needed: see ADR-029.

## ADR-028 · Links that open the app, and email links that work anywhere
**Email links carry a token, not a code.** Supabase's default confirmation link ends in a PKCE code that can only be
exchanged in the browser where sign-up started. With an app that breaks easily: sign up in the app, open the email,
the link lands in the browser and the sign-in fails. The templates now point to `/auth/confirm?token_hash=…`, which
the server verifies on its own (`verifyOtp`), so the link works in the app, in any browser and on any device, and it
goes straight to bouldertime.com, which is what App Links need. `/auth/callback` stays for OAuth.

**App Links over a custom scheme.** `https://bouldertime.com` links, verified through `assetlinks.json`, rather than
`bouldertime://`: one link works for everyone — in the app if installed, on the website otherwise — and no other app
can claim it. Only our hosts are followed inside the app; anything else handed to it is ignored.

**The signing key is checked against the published file on every build.** A mismatch would not fail anything visible;
links would just start opening in the browser. Making the build fail turns that into something someone notices.

## ADR-029 · Camera or gallery inside the app
ADR-027 assumed the upload fields would offer camera and library inside the app as they do in a browser. On Android
they don't: Capacitor's WebView opens the gallery for a plain file field and the camera for one with `capture`, never a
choice. Rather than add the camera plugin, the app asks first and opens the matching field (`MediaInput`). It covers
photos and videos alike, keeps the browser behaviour untouched, and the same component serves the web, the PWA and both
apps. Camera permission is requested by Capacitor on first use.

Two Android declarations are needed for the camera to actually open, and both fail by quietly showing the gallery:
`<queries>` for the capture intents (Android 11+ hides other apps otherwise, so Capacitor's check finds no camera app)
and a `file_paths.xml` entry for the folder the photo is written to. A test asserts both, because regenerating the
Android project would drop them without any error.

## ADR-030 · Notifications inside the store apps
Web Push does not reach a store app: there is no service worker inside one. The apps therefore go through Firebase
Cloud Messaging, which also carries iOS by way of Apple, so there is one sender to write rather than two.

**One queue, two senders.** `IPushSender.Handles(platform)` picks the sender for each device; the dispatcher, the
preferences and which notifications are worth a buzz are unchanged. A device row now says whether it is a browser or
an app, and `Address` holds either the push endpoint or the Firebase token — the keys stay empty for an app, which
needs no payload encryption.

**No Firebase library.** As with Web Push, the service account signs a short-lived JWT with .NET's own RSA, Google
exchanges it for an access token (cached until just before it expires), and that token sends the message. The signing
is verified against the service account's public key, including the case where the key's line breaks arrive as the
two characters `\n`, which is how they come out of an environment variable.

**A missing or broken service account turns app notifications off** and logs it, rather than stopping the API: the
rest of BoulderTime does not depend on them.

**The device's column keeps its old name.** The property is `Address` now that it holds either a push endpoint or a
Firebase token, but the column stays `endpoint`: renaming it would drop and recreate the column, losing every
registration and then failing the unique index on the empty values left behind — and Railway applies migrations
before switching to the new version, so a column the running version still reads must not vanish under it. The new
`platform` column defaults to Web, which is what every existing row was.

**A build without Firebase must not offer notifications.** `FirebaseMessaging.getInstance()` throws when the app has
no `google-services.json`, and Capacitor rethrows whatever a plugin throws, which closes the app — nothing in
JavaScript can catch it. So CI sets `VITE_PUSH_NATIVE` from the same condition that writes the file, and the app only
reaches the plugin when that says yes; otherwise the settings screen says this version cannot do notifications.

**The message sets no `click_action`.** That field names an activity to start, and a notification whose action no
activity declares does nothing at all when tapped — which is what a stray Flutter convention in the payload caused.
Left out, Firebase opens the app, Capacitor feeds the launch intent through `onNewIntent` (cold start included) and
the app reads the page to open from the message's data. The payload is built by a function a test reads, so the
shape is asserted rather than assumed.

**Notifications carry their own icon.** Android draws a notification icon from its alpha channel alone, and the app
icon has none to speak of, so it arrives as a filled square — which is what the first real notification showed. The
status-bar icon is the mark cut out of a transparent canvas, at every density, tinted with BoulderTime's orange.

**`google-services.json` is not committed.** The repository is public and the file names the Firebase project; CI
writes it from a secret, and a build without that secret produces an app without notifications and says so.

## ADR-031 · Blocking someone
Apple requires apps with user content to let people block abusive users, and it is worth having anyway: reporting
asks someone else to act and takes time, blocking is the reader's own decision and takes effect at once.

**Both directions.** A block hides what each writes from the other. Hiding one way only would leave the blocked
person replying to someone who has stopped reading, which is the situation the block was meant to end.

**Silent, and personal.** The blocked person is not told and sees nothing change; everyone else still sees both
people's comments and videos. Blocking removes nothing — it is a setting, not moderation.

**Gym staff still moderate what they blocked.** Filtering their view too would make blocking a moderator a way to
put content beyond reach.

The list of blocked people lives in the profile, so a block made months ago can still be found and undone.
