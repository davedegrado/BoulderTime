-- BoulderTime: keep application tables unreachable from Supabase's public Data API roles.
-- Idempotent. Run as the postgres role after `migrate`.

create schema if not exists bouldertime;

revoke all on schema bouldertime from public, anon, authenticated;
revoke all on all tables    in schema bouldertime from public, anon, authenticated;
revoke all on all sequences in schema bouldertime from public, anon, authenticated;
revoke all on all functions in schema bouldertime from public, anon, authenticated;

alter default privileges in schema bouldertime revoke all on tables    from public, anon, authenticated;
alter default privileges in schema bouldertime revoke all on sequences from public, anon, authenticated;
alter default privileges in schema bouldertime revoke all on functions from public, anon, authenticated;
