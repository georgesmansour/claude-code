-- Provisions a natively-installed PostgreSQL for local work: one login role and two
-- databases, so the development environment and the production rehearsal never share data.
--
--   invitation_dev   ← Visual Studio (F5), via appsettings.Development.json
--   invitation_prod  ← the local container stack, via .env
--
-- Run it once, as the postgres superuser, from a shell that has psql on PATH:
--
--   psql -U postgres -f backend/db/setup-local-postgres.sql
--
-- CHANGE THE PASSWORD BELOW before running, and use the same value in both connection
-- strings. This file is committed, so it must never hold a real password.
--
-- Neither database needs a schema: the API runs EF migrations and seeding on startup, so
-- an empty database provisions itself on first connect. To load your real data instead,
-- see "Restoring your data" in backend/docs/DOCKER.md.

-- ── Login role ───────────────────────────────────────────────────────────────
-- Not a superuser: the application only ever needs to own its own two databases.
DO
$$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'invitation') THEN
        CREATE ROLE invitation LOGIN PASSWORD 'CHANGE_ME_BEFORE_RUNNING';
    END IF;
END
$$;

-- ── Databases ────────────────────────────────────────────────────────────────
-- CREATE DATABASE cannot run inside a transaction or a DO block, so these are plain
-- statements. Re-running the script errors here if they already exist — that is harmless,
-- and safer than dropping databases automatically.
CREATE DATABASE invitation_dev  OWNER invitation ENCODING 'UTF8';
CREATE DATABASE invitation_prod OWNER invitation ENCODING 'UTF8';

-- ── Schema privileges ────────────────────────────────────────────────────────
-- Since PostgreSQL 15 the public schema is no longer writable by every user, so the
-- owner must be granted CREATE explicitly or EF migrations fail with "permission denied
-- for schema public" on the very first migration.
\connect invitation_dev
GRANT ALL ON SCHEMA public TO invitation;

\connect invitation_prod
GRANT ALL ON SCHEMA public TO invitation;
