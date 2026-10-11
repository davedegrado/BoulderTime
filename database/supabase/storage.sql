-- Storage buckets for a hosted Supabase project. Run once, after creating the project.
-- Re-running is safe: it only updates the settings of existing buckets.
--
-- Public buckets hold images whose URLs are already shared with everyone who can see the content.
-- Video buckets are PRIVATE: the API hands out short-lived signed URLs only to viewers allowed to watch
-- (approved videos: anyone who can see the boulder; pending/rejected: the uploader and the gym's staff).

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types) values
  ('boulder-images', 'boulder-images', true, 10485760, array['image/jpeg', 'image/png', 'image/webp']),
  ('gym-images',     'gym-images',     true,  5242880, array['image/jpeg', 'image/png', 'image/webp']),
  ('avatars',        'avatars',        true,  2097152, array['image/jpeg', 'image/png', 'image/webp'])
on conflict (id) do update
  set public = excluded.public,
      file_size_limit = excluded.file_size_limit,
      allowed_mime_types = excluded.allowed_mime_types;

-- Video buckets also hold each video's poster frame (a small JPEG or WebP the app captures on the phone, next to the
-- video, as private as the video itself): without the image types here, storage refuses every poster and the app
-- falls back to drawing a frame from the video, which Android's WebView doesn't do.
-- 50 MB matches the app's limit (VideoService.MaxVideoBytes) and the Free plan's own cap on any upload.
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types) values
  ('official-beta',    'official-beta',    false, 52428800, array['video/mp4', 'video/quicktime', 'video/webm', 'image/jpeg', 'image/webp']),
  ('community-videos', 'community-videos', false, 52428800, array['video/mp4', 'video/quicktime', 'video/webm', 'image/jpeg', 'image/webp'])
on conflict (id) do update
  set public = excluded.public,
      file_size_limit = excluded.file_size_limit,
      allowed_mime_types = excluded.allowed_mime_types;

-- Uploads and deletions always go through the API with the service-role key, so no client-facing
-- storage policies are granted here: the browser can only use the signed URLs the API issues.
