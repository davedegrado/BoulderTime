-- Prepares a plain PostgreSQL for Supabase Auth (GoTrue), mirroring supabase/auth hack/init_postgres.sql.
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'supabase_auth_admin') THEN
    CREATE USER supabase_auth_admin NOINHERIT CREATEROLE LOGIN NOREPLICATION PASSWORD 'postgres';
  END IF;
END $$;
CREATE SCHEMA IF NOT EXISTS auth AUTHORIZATION supabase_auth_admin;
GRANT CREATE ON DATABASE postgres TO supabase_auth_admin;
ALTER USER supabase_auth_admin SET search_path = 'auth';
