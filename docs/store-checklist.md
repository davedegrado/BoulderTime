# Store release checklist

What the code can't do by itself before the first public release. Each item says where it is done and how to tell it
is done. Kept short on purpose: when an item is done for good, delete it.

## Project settings (once)

- [ ] **Supabase → SQL editor:** run `database/supabase/storage.sql` again. It lets the video buckets accept the
  posters (JPEG/WebP) and sets their limit to 50 MB. Check: Storage → `community-videos` → bucket settings list
  `image/jpeg`; a video uploaded afterwards has a poster on Android.
- [ ] **Supabase → Storage → Settings:** the global upload limit is at least 50 MB (the Free plan's maximum).
- [ ] **GitHub → Settings → Variables:** `VITE_MAP_TILE_URL` (and `VITE_MAP_TILE_URL_IOS` if the key is
  origin-restricted) set to the MapTiler URL. The Android and iOS builds print a warning when it is missing.
- [ ] **Railway:** the API deployed from the same commit as the apps (the word filter and the 50 MB limit are checked
  by the API).

## Tests on real devices (each on an iPhone and an Android phone, store builds)

- [ ] Explore → Map: location prompt appears with the new text (iPhone: English or Italian following the phone's
  language), the map centres near you, the list is sorted by distance. On Android 12+, choosing "Approximate" works.
- [ ] Notifications: off by default after a fresh install; turning them on works and the test notification arrives.
  Sign out, then send something that would notify that account: nothing arrives. Sign back in: the switch is off.
- [ ] A comment with an insult is refused with the message in the app's language; an ordinary one goes through; edit
  a comment into an insult: refused.
- [ ] Upload a video under 50 MB from the camera and from the gallery: it uploads, the tile shows a poster on both
  phones (after the bucket change above). Pick a video over 50 MB: refused at once, with its size and the limit.
- [ ] Older videos without a poster: on Android the tile shows a frame instead of an empty box.
- [ ] Caption refused by the filter after the upload: fix the caption and send again; it doesn't upload again.
- [ ] Ask to delete an account: from another account its profile is gone and its comments and videos are hidden;
  cancel the deletion: they are back.
- [ ] Fonts look the same as before (Inter), also offline.

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
