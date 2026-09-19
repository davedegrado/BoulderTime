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

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types) values
  ('official-beta',    'official-beta',    false, 104857600, array['video/mp4', 'video/quicktime', 'video/webm']),
  ('community-videos', 'community-videos', false, 104857600, array['video/mp4', 'video/quicktime', 'video/webm'])
on conflict (id) do update
  set public = excluded.public,
      file_size_limit = excluded.file_size_limit,
      allowed_mime_types = excluded.allowed_mime_types;

-- Uploads and deletions always go through the API with the service-role key, so no client-facing
-- storage policies are granted here: the browser can only use the signed URLs the API issues.
