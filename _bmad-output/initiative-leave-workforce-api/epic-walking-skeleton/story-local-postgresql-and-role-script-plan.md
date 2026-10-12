---
title: 'Local PostgreSQL and role script'
type: 'chore'
ticket: '2'
created: '2026-10-12'
status: 'built'
baseline_revision: 'a9ec6111ad1574d7fffd3d3d2917e0850153e628'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md'
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/solution-structure.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** There is no local database. Later entries need PostgreSQL 18 with the three roles of AD-13, kept least-privilege and without credentials in source control.

**Approach:** Add a Docker Compose setup for PostgreSQL 18 with persistent storage, and an idempotent `deploy/db/roles.sql` that creates the roles migrator, app_rw and retention_job, the three schemas owned by migrator, and per-schema default privileges. Add a small helper script and a verification script, and document setup in the README.

## Boundaries & Constraints

**Always:** Pin the image once in `deploy/postgres.env` (`postgres:18`). Passwords come only from environment variables read from a gitignored `deploy/db/dev.env`; the committed `dev.env.example` holds placeholders, and startup refuses missing or placeholder values. Bind the port to 127.0.0.1 only. Roles are LOGIN only, not superuser, not createdb, not createrole. migrator owns the schemas `people`, `leave` and `audit` and may create objects; app_rw gets SELECT, INSERT, UPDATE, DELETE by default on tables in `people` and `leave`, and only SELECT and INSERT by default in `audit`; retention_job gets only SELECT and DELETE by default in `audit`; PUBLIC has no access to the database, and migrator also has USAGE and CREATE on the `public` schema so it can install extensions such as btree_gist (approved by the human during review). The script is safe to rerun. Per-table audit grants beyond these defaults belong to entries 2.1 and 8.3.

**Never:** No migrations or tables, no connection from the API (entry 1.3), no authentication, no Dockerfile for the app, no CI, no committed secrets, no use of the `postgres` superuser by the application, no changes to the architecture.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First start | No volume, `dev.env` filled in | PostgreSQL 18 healthy, roles and schemas created | No error expected |
| Rerun roles | Roles and schemas exist | Exits 0; privileges unchanged; passwords re-applied | No error expected |
| Stop and start | Containers stopped then started | Data survives in the volume | No error expected |
| Reset | `reset` command | Volume removed, fresh database and roles on the next start | Asks for no secrets in output |
| Missing or placeholder config | No `dev.env` or a `CHANGE_ME` value | Refuses to start with a clear message | Non-zero exit |
| Least privilege | app_rw creates a table, or deletes or updates in `audit` | Permission denied | Verification reports pass |
| Wrong password | Any role with a bad password | Connection refused | Verification reports pass |

</frozen-after-approval>

## Code Map

- Greenfield for infrastructure: no `compose.yaml`, `deploy/` or database files exist; `.gitignore` and `README.md` exist and are extended.
- Spine AD-13 (roles and grants), AD-25 (provisioning order: `roles.sql` first), AD-17 (schemas people, leave, audit), AD-15 (secrets only from env); solution-structure section 6 and 8.
- Entry 1.3 will read the image tag from `deploy/postgres.env` for its Testcontainers fixture and apply `roles.sql` to that container, so `roles.sql` must run with plain `psql` and environment variables, not Compose-only features.
- The Postgres 18 image keeps data under `/var/lib/postgresql`; mount the volume there. Local sockets in the image are trusted, so password checks must connect over TCP.
- Environment: Docker 27.5 with Compose 2.32 through Colima, running; `psql` runs inside the image.

## Tasks & Acceptance

**Execution:**
- [x] `deploy/postgres.env` -- `POSTGRES_IMAGE=postgres:18` -- the single image pin
- [x] `deploy/db/dev.env.example`, `.gitignore` -- placeholders for the superuser, migrator, app_rw and retention_job passwords, database name and port; ignore `deploy/db/dev.env` -- keep credentials out of git
- [x] `compose.yaml` -- `postgres` service (pinned image, env file, 127.0.0.1 port, named volume, healthcheck) and a one-shot `db-roles` service running `psql` on `roles.sql` after the database is healthy -- repeatable setup
- [x] `deploy/db/roles.sql` -- idempotent roles, database privileges, schemas owned by migrator, default privileges per the boundaries -- AD-13
- [x] `deploy/db/db.sh` -- `up`, `down`, `reset`, `roles`, `verify`, `psql`; passes both env files to Compose and refuses missing or placeholder config -- one documented entry point
- [x] `deploy/db/verify.sh` -- checks the version starts with 18, each role connects by password over TCP, a wrong password fails, app_rw cannot create objects, and the default privileges behave as the boundaries state using temporary tables; reruns `roles.sql` and checks it still passes -- covers every matrix row
- [x] `README.md` -- prerequisites, required environment variables, start, stop, reset, rerun roles, verify, and a connection-string example with the password from the environment; note the API does not connect until entry 1.3 -- documentation

**Acceptance Criteria:**
- Given a filled `dev.env`, when `deploy/db/db.sh up` runs, then PostgreSQL 18 is healthy and the roles and schemas exist.
- Given the running database, when `deploy/db/verify.sh` runs, then it reports every check as passed and exits 0.
- Given the running database, when `roles.sql` is run again, then it exits 0 and verification still passes.
- Given `deploy/db/db.sh down` then `up`, then the data persists; given `reset` then `up`, then the database is fresh.
- Given the repository, when `git status` is read after setup, then no file with a password is tracked or untracked-and-unignored.
- Given the solution, when `dotnet build LeaveWorkforce.slnx` runs, then it still succeeds with 0 warnings.

## Implementation Notes

- Added `deploy/postgres.env` (single `postgres:18` pin), `deploy/db/dev.env.example`, `compose.yaml` (`postgres` plus a one-shot `db-roles` under the `tools` profile, run by `db.sh`), `deploy/db/roles.sql`, `deploy/db/db.sh`, `deploy/db/verify.sh`, a README section, and `deploy/db/dev.env` in `.gitignore`. A local gitignored `dev.env` with random passwords exists on this machine.
- `roles.sql` creates the roles (LOGIN only), revokes PUBLIC on the database and the `public` schema, creates `people`, `leave` and `audit` owned by migrator, and sets default privileges as the frozen boundaries state. It also grants migrator CREATE on the database and, with the human's approval during review, USAGE and CREATE on the `public` schema so it can install `btree_gist` (AD-23). Sequences get no grants.
- Passwords are plain-simple values because the env file is read by both the shell and Compose; the example says so. The Postgres 18 image treats local sockets as trusted, so every check connects over TCP.
- Verification: `verify.sh` runs 29 checks, including PUBLIC having no access and the extension install, and exits 0 on a fresh database and after rerunning `roles.sql`. Lifecycle rows were run by hand: data persisted across down and up; reset gave a fresh database; a missing `dev.env` and a `CHANGE_ME` value both exit 1 with a clear message. These lifecycle rows are verified by those commands, not by a committed test.
- Not in scope and not done: migrations, tables, any API connection (entry 1.3), CI.

## Plan Change Log

## Review Triage Log

Quick review, pass 1: 7 findings (high 0, medium 3, low 3, rejected 1).

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| `roles.sql` guard for missing passwords: `\getenv` on an unset variable gave a syntax error, and `\quit 1` ignores its argument (psql warned and exited 0) | medium | patch | Reproduced both. Variables are pre-set empty and the guard now raises an exception; the unset-env run exits 3 with the friendly message. |
| Healthcheck uses the unix socket, which the image's first-run server serves before TCP | medium | patch | Healthcheck now uses `pg_isready -h 127.0.0.1`; two consecutive reset-then-up runs both exit 0. |
| `expect_deny` passed on any non-zero exit | medium | patch | It now matches the error text. The stricter check showed the extension denial for app_rw is "no schema has been selected", which is accepted explicitly. |
| No check that PUBLIC has no access | low | patch | Added checks for PUBLIC on the database and for runtime roles on the `public` schema. |
| README connection string hardcoded port and database and did not load the env file | low | patch | Uses `POSTGRES_PORT` and `POSTGRES_DB` and shows how to load `dev.env`. |
| `down` and `reset` need a complete `dev.env` | low | rejected | Unlikely in everyday use and `docker volume rm leave-workforce_pgdata` is a one-line workaround. |
| Migrator could not install `btree_gist` (found in my own verification, not by the lens) | medium | patch with human approval | `CREATE EXTENSION` failed with no schema selected; the human approved granting migrator USAGE and CREATE on `public`. |

## Design Notes

`roles.sql` reads passwords with psql's `\getenv` and applies them with `format('%L')` through `\gexec`, so it needs no shell templating and is idempotent: create the role only if missing, always alter it afterwards. Default privileges are set `FOR ROLE migrator`, which is the owner of the objects migrations will create. Sequences get no grants because identifiers are generated in the application.

## Verification

**Commands:**
- `deploy/db/db.sh up` -- expected: database healthy and roles applied
- `deploy/db/verify.sh` -- expected: all checks passed, exit 0
- `deploy/db/db.sh roles` then `deploy/db/verify.sh` -- expected: exit 0 both times
- `deploy/db/db.sh down`, `deploy/db/db.sh up`, then a check that data created before still exists -- expected: it does
- `dotnet build LeaveWorkforce.slnx` -- expected: 0 warnings, 0 errors
