#!/usr/bin/env bash
# Local PostgreSQL helper: up | down | reset | roles | verify | psql
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"
CONFIG=deploy/db/dev.env
REQUIRED=(POSTGRES_DB POSTGRES_PORT POSTGRES_USER POSTGRES_PASSWORD MIGRATOR_PASSWORD APP_RW_PASSWORD RETENTION_JOB_PASSWORD)

if [ ! -f "$CONFIG" ]; then
  echo "Missing $CONFIG. Copy deploy/db/dev.env.example to $CONFIG and replace every CHANGE_ME." >&2
  exit 1
fi
set -a
# shellcheck disable=SC1090,SC1091
. deploy/postgres.env
. "$CONFIG"
set +a
for v in "${REQUIRED[@]}"; do
  val="${!v:-}"
  if [ -z "$val" ] || [[ "$val" == *CHANGE_ME* ]]; then
    echo "$v in $CONFIG is missing or still a CHANGE_ME placeholder." >&2
    exit 1
  fi
done
export DB_SH_LOADED=1

compose() { docker compose --env-file deploy/postgres.env --env-file "$CONFIG" "$@"; }
apply_roles() { compose --profile tools run --rm -T db-roles; }

case "${1:-}" in
  up)     compose up -d --wait postgres; apply_roles ;;
  down)   compose down ;;
  reset)  compose --profile tools down --volumes --remove-orphans ;;
  roles)  compose up -d --wait postgres; apply_roles ;;
  verify) exec "$ROOT/deploy/db/verify.sh" ;;
  psql)   shift; compose exec -e PGPASSWORD="$POSTGRES_PASSWORD" postgres psql -h postgres -U "$POSTGRES_USER" -d "$POSTGRES_DB" "$@" ;;
  *)      echo "Usage: $0 {up|down|reset|roles|verify|psql [psql args]}" >&2; exit 2 ;;
esac
