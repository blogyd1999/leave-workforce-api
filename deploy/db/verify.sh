#!/usr/bin/env bash
# Verifies the local database. Run through db.sh (re-executes itself via db.sh to load config).
set -uo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
[ -n "${DB_SH_LOADED:-}" ] || exec "$DIR/db.sh" verify

COMPOSE=(docker compose --env-file deploy/postgres.env --env-file deploy/db/dev.env)
FAILS=0
pass() { echo "PASS  $1"; }
fail() { echo "FAIL  $1"; FAILS=$((FAILS + 1)); }

# run ROLE PASSWORD SQL: over TCP (host "postgres" is the container's network address, so passwords are checked)
run() {
  "${COMPOSE[@]}" exec -T -e PGPASSWORD="$2" postgres \
    psql -h postgres -U "$1" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tA -c "$3" 2>&1
}
expect_ok()   { local out; if out=$(run "$2" "$3" "$4"); then pass "$1"; else fail "$1 ($out)"; fi; }
# expect_deny LABEL ROLE PASSWORD SQL PATTERN: must fail, and the error must match PATTERN (so it fails for the right reason)
expect_deny() {
  local out
  if out=$(run "$2" "$3" "$4"); then fail "$1 (unexpectedly allowed)"
  elif echo "$out" | grep -qi "$5"; then pass "$1"
  else fail "$1 (failed for another reason: $out)"; fi
}

ver=$(run "$POSTGRES_USER" "$POSTGRES_PASSWORD" "SHOW server_version") || ver=""
case "$ver" in 18*) pass "server version is $ver" ;; *) fail "server version is '$ver', expected 18" ;; esac

expect_ok "migrator connects"      migrator      "$MIGRATOR_PASSWORD"      "SELECT 1"
expect_ok "app_rw connects"        app_rw        "$APP_RW_PASSWORD"        "SELECT 1"
expect_ok "retention_job connects" retention_job "$RETENTION_JOB_PASSWORD" "SELECT 1"
expect_deny "migrator wrong password is refused"      migrator      "wrong-$$" "SELECT 1" "password authentication failed"
expect_deny "app_rw wrong password is refused"        app_rw        "wrong-$$" "SELECT 1" "password authentication failed"
expect_deny "retention_job wrong password is refused" retention_job "wrong-$$" "SELECT 1" "password authentication failed"

attrs=$(run "$POSTGRES_USER" "$POSTGRES_PASSWORD" "SELECT count(*) FROM pg_roles WHERE rolname IN ('migrator','app_rw','retention_job') AND rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole")
[ "$attrs" = "3" ] && pass "roles are LOGIN only, not superuser/createdb/createrole" || fail "role attributes ($attrs)"
owners=$(run "$POSTGRES_USER" "$POSTGRES_PASSWORD" "SELECT count(*) FROM pg_namespace n JOIN pg_roles r ON r.oid = n.nspowner WHERE n.nspname IN ('people','leave','audit') AND r.rolname = 'migrator'")
[ "$owners" = "3" ] && pass "schemas people, leave, audit owned by migrator" || fail "schema owners ($owners)"

expect_deny "app_rw cannot create a table in people" app_rw "$APP_RW_PASSWORD" "CREATE TABLE people.vtmp_denied (id int)" "permission denied"
expect_deny "app_rw cannot create a table in public" app_rw "$APP_RW_PASSWORD" "CREATE TABLE public.vtmp_denied (id int)" "permission denied"
expect_deny "app_rw cannot create a temporary table" app_rw "$APP_RW_PASSWORD" "CREATE TEMP TABLE vtmp_denied (id int)" "permission denied"

# PUBLIC has no access to the database, and the runtime roles have nothing on the public schema.
public_db=$(run "$POSTGRES_USER" "$POSTGRES_PASSWORD" "SELECT count(*) FROM aclexplode((SELECT datacl FROM pg_database WHERE datname = current_database())) WHERE grantee = 0")
[ "$public_db" = "0" ] && pass "PUBLIC has no privileges on the database" || fail "PUBLIC has database privileges ($public_db)"
public_schema=$(run "$POSTGRES_USER" "$POSTGRES_PASSWORD" "SELECT count(*) FROM (VALUES ('app_rw'),('retention_job')) r(n) WHERE has_schema_privilege(r.n, 'public', 'USAGE') OR has_schema_privilege(r.n, 'public', 'CREATE')")
[ "$public_schema" = "0" ] && pass "app_rw and retention_job have no rights on the public schema" || fail "runtime roles have public schema rights ($public_schema)"

# migrator can install extensions (btree_gist for AD-23); the transaction is rolled back so nothing is left behind.
expect_ok   "migrator can install btree_gist (rolled back)" migrator "$MIGRATOR_PASSWORD" "BEGIN; CREATE EXTENSION IF NOT EXISTS btree_gist; ROLLBACK;"
expect_deny "app_rw cannot install an extension"            app_rw   "$APP_RW_PASSWORD"   "CREATE EXTENSION IF NOT EXISTS btree_gist" "permission denied\|no schema has been selected"

# Scratch tables created by migrator exercise the default privileges, then are dropped.
cleanup() { run migrator "$MIGRATOR_PASSWORD" "DROP TABLE IF EXISTS people.vtmp, leave.vtmp, audit.vtmp" >/dev/null; }
trap cleanup EXIT
expect_ok "migrator creates scratch tables" migrator "$MIGRATOR_PASSWORD" \
  "CREATE TABLE people.vtmp (id int); CREATE TABLE leave.vtmp (id int); CREATE TABLE audit.vtmp (id int)"

for s in people leave; do
  expect_ok "app_rw has SELECT, INSERT, UPDATE, DELETE in $s" app_rw "$APP_RW_PASSWORD" \
    "INSERT INTO $s.vtmp VALUES (1); SELECT * FROM $s.vtmp; UPDATE $s.vtmp SET id = 2; DELETE FROM $s.vtmp"
done
expect_ok   "app_rw can INSERT and SELECT in audit"  app_rw "$APP_RW_PASSWORD" "INSERT INTO audit.vtmp VALUES (1); SELECT * FROM audit.vtmp"
expect_deny "app_rw cannot UPDATE in audit"          app_rw "$APP_RW_PASSWORD" "UPDATE audit.vtmp SET id = 2" "permission denied"
expect_deny "app_rw cannot DELETE in audit"          app_rw "$APP_RW_PASSWORD" "DELETE FROM audit.vtmp" "permission denied"
expect_ok   "retention_job can SELECT and DELETE in audit" retention_job "$RETENTION_JOB_PASSWORD" "SELECT * FROM audit.vtmp; DELETE FROM audit.vtmp"
expect_deny "retention_job cannot INSERT in audit"   retention_job "$RETENTION_JOB_PASSWORD" "INSERT INTO audit.vtmp VALUES (1)" "permission denied"
expect_deny "retention_job cannot UPDATE in audit"   retention_job "$RETENTION_JOB_PASSWORD" "UPDATE audit.vtmp SET id = 2" "permission denied"
expect_deny "retention_job cannot read people"       retention_job "$RETENTION_JOB_PASSWORD" "SELECT * FROM people.vtmp" "permission denied"

cleanup
trap - EXIT

# Rerun roles.sql and make sure the role checks still hold.
if "$DIR/db.sh" roles >/dev/null 2>&1; then pass "roles.sql reruns with exit 0"; else fail "roles.sql rerun failed"; fi
expect_ok "app_rw still connects after rerun" app_rw "$APP_RW_PASSWORD" "SELECT 1"

if [ "$FAILS" -eq 0 ]; then echo "All checks passed."; else echo "$FAILS check(s) failed."; exit 1; fi
