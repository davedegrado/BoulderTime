# Store release checklist

What the code can't do by itself before the first public release. Each item says where it is done and how to tell it
is done. Kept short on purpose: when an item is done for good, delete it.

## Project settings (once)

- [ ] **Railway:** leave `Videos__UploadsEnabled` unset (video uploads off, ADR-049). Check:
  `https://<api>/api/settings/videos` answers `"uploadsEnabled": false`.
- [x] **Railway:** `Email__ResendApiKey` (Resend → API Keys) and `Moderation__AlertEmail=support@bouldertime.com`, so
  reports reach you by email (ADR-050). Check: report a comment from a test account; the email arrives.
- [x] **Supabase → SQL editor:** run `database/supabase/storage.sql` again (done 11 October 2026). It lets the video buckets accept the
  posters (JPEG/WebP) and sets their limit to 50 MB, ready for when uploads are switched on.
- [ ] **GitHub → Settings → Variables:** `VITE_MAP_TILE_URL` (and `VITE_MAP_TILE_URL_IOS` if the key is
  origin-restricted) set to the MapTiler URL. The Android and iOS builds print a warning when it is missing.
- [ ] **Railway:** the API deployed from the same commit as the apps (the word filter and the video switch live in the
  API).

## Account for the stores' reviewers

- [x] Sign up in the app with `review@bouldertime.com` (Cloudflare → Email Routing → a rule forwarding it to you),
  confirm the email, accept the terms. New accounts are out of the leaderboards by default (ADR-054): leave it so.
- [x] **Admin → Demo gyms for store review** → Create: three unlisted demo gyms, the reviewers' account as their
  admin and following them.
- [x] With the reviewers' account, log a few attempts in a demo gym, so Home and Activity aren't empty.
- [ ] App Store Connect (App Review Information) **done**; Play Console (App access) **still to do**: these credentials, and in the notes:
  *"This account follows three demo gyms (unlisted, so regular users don't see them) and is an admin of them: climber
  features are on Home and in each gym; staff features are under Profile → Manage. Video uploads are currently
  disabled; official beta videos are added as links."*

## Tests on real devices (each on an iPhone and an Android phone, store builds)

- [ ] Explore → Map: location prompt appears with the new text (iPhone: English or Italian following the phone's
  language), the map centres near you, the list is sorted by distance. On Android 12+, choosing "Approximate" works.
- [ ] Notifications: after signing in, the app asks once whether to turn them on; "Turn on" shows the system prompt and
  the test notification arrives; closing and reopening the app doesn't ask again. Sign out: nothing arrives. Sign
  back in: asked again.
- [ ] Location on iPhone: only the system's prompt (no second "localhost" one), also after closing and reopening.
- [ ] Everyone is asked to accept the updated terms once.
- [ ] A comment with an insult is refused with the message under the box, in the app's language; an ordinary one goes through; edit
  a comment into an insult: refused.
- [ ] With uploads off: no "Choose a video" anywhere; the boulder editor's official beta opens on the link form and a
  link publishes; the help pages don't mention uploading; a boulder without videos shows no "Community videos".
- [ ] Videos already stored (if any) still play; on Android a tile without a poster shows a frame.
- [ ] Ask to delete an account: from another account its profile is gone and its comments and videos are hidden;
  cancel the deletion: they are back.
- [ ] Fonts look the same as before (Inter), also offline.

When video uploads are switched on later (`Videos__UploadsEnabled=true` on Railway, with a plan that takes the
limit): upload from camera and gallery on both phones, the tile has a poster, a video over the limit is refused at
once with its size, and a caption refused by the filter can be fixed and sent without uploading again.

## Store consoles

See `docs/store-privacy.md` for the privacy answers and `docs/mobile.md` for the build settings.

- [ ] **Google Play:** closed test with at least 12 testers for 14 days in a row (personal developer accounts created
  after 13 November 2023), then apply for production.
- [ ] **Google Play:** Data safety, account deletion URL, privacy policy URL, content rating questionnaire (user
  content and its moderation), target audience, ads: none.
- [ ] **Google Play:** after the first CI build with the 16 KB step, read its log: every library "ok" or "no native
  libraries". Play Console → the release → App bundle explorer also reports 16 KB compatibility.
- [ ] **App Store Connect:** App Privacy answers, age rating (user-generated content: yes), review notes with a demo
  account that follows a gym with boulders, support and privacy URLs, EU trader status (DSA).
- [ ] **Both:** screenshots and texts from the store kit.
