-- Roles, schemas and default privileges (AD-13). Idempotent; run as the superuser with plain psql.
-- Reads MIGRATOR_PASSWORD, APP_RW_PASSWORD and RETENTION_JOB_PASSWORD from the environment.
\set ON_ERROR_STOP on
\set migrator_pw ''
\set app_rw_pw ''
\set retention_pw ''
\getenv migrator_pw MIGRATOR_PASSWORD
\getenv app_rw_pw APP_RW_PASSWORD
\getenv retention_pw RETENTION_JOB_PASSWORD

SELECT (:'migrator_pw' <> '' AND :'app_rw_pw' <> '' AND :'retention_pw' <> '') AS passwords_present \gset
\if :passwords_present
\else
  DO $$ BEGIN RAISE EXCEPTION 'roles.sql: MIGRATOR_PASSWORD, APP_RW_PASSWORD and RETENTION_JOB_PASSWORD must all be set'; END $$;
\endif

-- Create each role only if missing, then always (re)apply attributes and password.
SELECT format('CREATE ROLE %I LOGIN', r)
FROM (VALUES ('migrator'), ('app_rw'), ('retention_job')) AS t(r)
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) \gexec

SELECT format('ALTER ROLE %I WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L', r, p)
FROM (VALUES ('migrator', :'migrator_pw'), ('app_rw', :'app_rw_pw'), ('retention_job', :'retention_pw')) AS t(r, p) \gexec

-- Database: no access for PUBLIC; the three roles may connect; migrator may create schemas/extensions.
SELECT format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database()) \gexec
SELECT format('GRANT CONNECT ON DATABASE %I TO migrator, app_rw, retention_job', current_database()) \gexec
SELECT format('GRANT CREATE ON DATABASE %I TO migrator', current_database()) \gexec
REVOKE ALL ON SCHEMA public FROM PUBLIC;
-- migrator installs extensions such as btree_gist (AD-23); nobody else gets anything on public.
GRANT USAGE, CREATE ON SCHEMA public TO migrator;

-- Schemas owned by migrator.
CREATE SCHEMA IF NOT EXISTS people AUTHORIZATION migrator;
CREATE SCHEMA IF NOT EXISTS leave AUTHORIZATION migrator;
CREATE SCHEMA IF NOT EXISTS audit AUTHORIZATION migrator;
ALTER SCHEMA people OWNER TO migrator;
ALTER SCHEMA leave OWNER TO migrator;
ALTER SCHEMA audit OWNER TO migrator;
REVOKE ALL ON SCHEMA people, leave, audit FROM PUBLIC;

GRANT USAGE ON SCHEMA people, leave, audit TO app_rw;
GRANT USAGE ON SCHEMA audit TO retention_job;

-- Default privileges on objects that migrator creates. No sequence grants (ids are generated in the app).
ALTER DEFAULT PRIVILEGES FOR ROLE migrator IN SCHEMA people GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE migrator IN SCHEMA leave  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO app_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE migrator IN SCHEMA audit  GRANT SELECT, INSERT ON TABLES TO app_rw;
ALTER DEFAULT PRIVILEGES FOR ROLE migrator IN SCHEMA audit  GRANT SELECT, DELETE ON TABLES TO retention_job;
-- Per-table audit grants beyond these defaults belong to entries 2.1 and 8.3.
