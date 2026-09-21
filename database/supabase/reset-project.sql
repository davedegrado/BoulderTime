-- Preparing an EXISTING Supabase project for BoulderTime.
--
-- Safe to run on a project that holds an earlier, unrelated app: BoulderTime keeps its tables in the `bouldertime`
-- schema, so nothing here is required — it only removes leftovers that would otherwise confuse you later.
--
-- READ BEFORE RUNNING: this deletes data. Run the blocks you actually want, not the whole file blindly.

-- 1. Old application tables in `public` (BoulderTime never uses this schema).
--    Inspect first:
--      select table_name from information_schema.tables where table_schema = 'public' order by table_name;
--    Then, when you are sure nothing there matters:
-- drop schema public cascade;
-- create schema public;
-- grant usage on schema public to anon, authenticated, service_role;

-- 2. Old storage buckets and their files. Inspect first:
--      select id, name, public from storage.buckets order by id;
--    Then delete the ones you don't want (BoulderTime's own buckets are created by storage.sql):
-- delete from storage.objects where bucket_id = 'old-bucket-name';
-- delete from storage.buckets where id = 'old-bucket-name';

-- 3. Old accounts. Every account that signs in becomes a BoulderTime profile, so remove the ones you don't want.
--    Inspect first:
--      select id, email, created_at, last_sign_in_at from auth.users order by created_at;
--    Deleting users is safer from the dashboard (Authentication → Users), which also cleans up identities.

-- 4. BoulderTime's own data, if you ever want a clean slate (the API recreates the schema on the next migrate):
-- drop schema if exists bouldertime cascade;

-- 5. Demo climbers created by `dotnet run -- seed`. They have no Supabase account and can be removed at any time:
--      select count(*) from bouldertime.users where email like '%@demo.bouldertime.invalid';
--    Their content (attempts, ratings, comments) is removed with them:
-- delete from bouldertime.users where email like '%@demo.bouldertime.invalid';

-- After the cleanup, in order:
--   1) apply the migrations (Railway's pre-deploy step does it, or run `dotnet run --project src/BoulderTime.Api -- migrate`
--      from backend/ with ConnectionStrings__Database pointing at this project)
--   2) SQL Editor: paste and run database/supabase/hardening.sql
--   3) SQL Editor: paste and run database/supabase/storage.sql
--   (psql works too if you have it installed; nothing requires it)
