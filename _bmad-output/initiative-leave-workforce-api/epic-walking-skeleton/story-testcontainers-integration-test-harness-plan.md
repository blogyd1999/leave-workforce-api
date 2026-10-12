---
title: 'Testcontainers integration-test harness'
type: 'chore'
ticket: '3'
created: '2026-10-12'
status: 'built'
baseline_revision: '89222f3c6f122ff7e7e23051044b59a3c489166c'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md'
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/epic-walking-skeleton/story-local-postgresql-and-role-script-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Later entries need a real PostgreSQL 18 with the three roles for integration and concurrency tests, started the same way everywhere. Today the only test host has no database.

**Approach:** Add a reusable xunit.v3 fixture in `Api.IntegrationTests` that starts a disposable Testcontainers PostgreSQL from the image pinned in `deploy/postgres.env`, applies `deploy/db/roles.sql` with test-only random credentials, and a WebApplicationFactory that sets `ConnectionStrings:App`. Prove it with focused tests of the container, the roles and the approved permissions.

## Boundaries & Constraints

**Always:** Read the image tag from `deploy/postgres.env`; never hard-code a version. Apply the existing `roles.sql` unchanged through plain `psql` with the password environment variables. Generate every credential randomly per run, test-only, and never write one to test output or logs. One container per collection, with automatic cleanup. Keep the fixture and factory public so `Concurrency.Tests` can use them. If Docker is unavailable, fail the tests with a clear message. Follow the existing style: xunit.v3, `TestContext.Current.CancellationToken`, `using var`, central package versions.

**Never:** No change to `roles.sql` or to any permission to make a test pass, no migrations or tables beyond scratch tables that tests drop, no code in the Api that reads `ConnectionStrings:App`, no EF Core, no concurrency tests (entry 1.7), no new image pin, no skipping a test when Docker is missing.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Container starts | Docker running | Container from the pinned image starts; `server_version` begins with 18 | Docker missing: tests fail with a clear message |
| Roles exist | `roles.sql` applied | migrator, app_rw and retention_job exist, LOGIN only, not superuser, createdb or createrole | A failing script fails the fixture, with no secrets in the message |
| Permissions | Each role runs the checks of entry 1.2 | Allowed and denied exactly as the 1.2 boundaries state, denials matched by error text | A wrong password is refused |
| Rerun roles | `roles.sql` applied a second time | No error; roles still work | Not applicable |
| Health through factory | Factory with `ConnectionStrings:App` set | `GET /health` returns 200 `{"status":"up"}`; the Api does not connect | Not applicable |
| Cleanup | Test run ends | The container is removed | Verified by listing containers after the run |
| Secrets | A test run | Credentials are random and never printed | Not applicable |

</frozen-after-approval>

## Code Map

- `tests/Api.IntegrationTests/` -- existing xunit.v3 project (Mvc.Testing, TestHost) with `HealthEndpointTests`, `ProblemDetailsMapperTests`, `ResultAndErrorTests`; add `Infrastructure/` and `Database/` folders here.
- `tests/Concurrency.Tests/` -- already references `Api.IntegrationTests`; entry 1.7 will use the fixture, so make its types public.
- `deploy/postgres.env` -- `POSTGRES_IMAGE=postgres:18`; `deploy/db/roles.sql` -- reads `MIGRATOR_PASSWORD`, `APP_RW_PASSWORD`, `RETENTION_JOB_PASSWORD` with `\getenv`, so the container needs those variables and psql must run inside it.
- `deploy/db/verify.sh` -- the permission checks to port to C#; connections must use TCP so passwords are checked (local sockets are trusted).
- `Directory.Packages.props` -- central versions; add `Testcontainers.PostgreSql` 4.15.0 and `Npgsql` 10.0.x matching the provider planned for EF Core (resolve from NuGet).
- Repository root is found by walking up to `LeaveWorkforce.slnx`. `DOCKER_HOST` points at Colima; Testcontainers honours it.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props`, `tests/Api.IntegrationTests/LeaveWorkforce.Api.IntegrationTests.csproj` -- add Testcontainers.PostgreSql and Npgsql -- package choices
- [x] `tests/Api.IntegrationTests/Infrastructure/PostgresFixture.cs` -- collection fixture: read the pin, start the container with random credentials, copy and run `roles.sql`, expose per-role connection strings and an admin connection, dispose reliably; plus the collection definition -- reusable harness
- [x] `tests/Api.IntegrationTests/Infrastructure/ApiFactory.cs` -- WebApplicationFactory<Program> that sets `ConnectionStrings:App` to the app_rw connection string -- the factory
- [x] `tests/Api.IntegrationTests/Database/` -- tests for container version, roles, permissions (ported from `verify.sh`), roles rerun, and health through the factory -- covers every matrix row
- [x] `README.md` -- how to run the integration tests, the Docker and Colima settings, and the cleanup guarantee -- documentation

**Acceptance Criteria:**
- Given Docker is running, when the tests run, then a container from the pinned image starts, `server_version` begins with 18, and the three roles exist.
- Given the fixture, when the permission tests run, then every allowed and denied case matches the entry 1.2 boundaries and no test weakens a permission.
- Given the factory, when `GET /health` is called, then it returns 200 `{"status":"up"}` with `ConnectionStrings:App` set.
- Given the tests have finished, when containers are listed, then none from the run remains.
- Given the test output, when it is read, then no password appears.
- Given the solution, when `dotnet build LeaveWorkforce.slnx` runs, then it has 0 warnings and Concurrency.Tests still builds.

## Implementation Notes

- Packages: `Testcontainers.PostgreSql` 4.15.0 and `Npgsql` 10.0.3 (the version already in the local NuGet cache for the EF Core provider), both in `Directory.Packages.props` and referenced from `Api.IntegrationTests`.
- Added `Infrastructure/PostgresFixture.cs` (public; reads `POSTGRES_IMAGE` from `deploy/postgres.env`, finds the repo root via `LeaveWorkforce.slnx`, random per-run credentials, copies the unchanged `roles.sql` into the container and runs it with `psql` there, exposes per-role, admin and wrong-password TCP connection strings with pooling off, and the `Postgres` collection), `Infrastructure/ApiFactory.cs` (sets `ConnectionStrings:App` to the app_rw connection string with the application name `leaveworkforce-api-test`), and `Database/PostgresHarnessTests.cs` (11 tests covering every matrix row). README gained an "Integration tests" section with the Colima `DOCKER_HOST` setting.
- Deviations worth knowing: a TCP-based wait strategy (`pg_isready -h 127.0.0.1`) because the image first runs a socket-only temporary server, and a host-side connect retry of up to 30 seconds because under Colima the mapped port can lag behind readiness.
- Verification: full suite 32 of 32 passes (twice in a row after the patches); no test container remains after the Ryuk reaper finishes (~15 s); a scan of the test output found no secret; with `DOCKER_HOST` pointed at a missing socket the 11 database tests fail with a clear message (not skipped) and the 21 others pass. `dotnet build` has 0 warnings. Architecture.Tests and Concurrency.Tests still report zero tests (exit 8) until entries 1.6 and 1.7.
- Not done on purpose: any Api code reading `ConnectionStrings:App`, migrations, concurrency tests, any change to `roles.sql` or to a permission.

## Plan Change Log

## Review Triage Log

Quick review, pass 1: 7 findings (high 0, medium 1, low 2, rejected 4).

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| Health test counted `app_rw` sessions, which can be flaky because Terminate does not wait for the backend to exit | medium | patch | The factory now gives its connection string a unique application name and the test counts sessions with that name, so other tests cannot affect it. |
| Nothing verified that `ConnectionStrings:App` reaches the host configuration | low | patch | The test reads it from the host's configuration and asserts the app_rw user and application name without printing the value. |
| Container might not be disposed if the fixture fails part-way | low | patch | `InitializeAsync` now disposes the container and rethrows when startup fails. |
| README admits the Ryuk reaper may appear, versus "none remains" | false | rejected | Listing containers about 15 seconds after the run showed none. |
| Secret redaction covers only some paths | low | rejected | Npgsql messages never include the password and credentials are random per run; no demonstrated leak. |
| Wrong-password test matches SQLSTATE 28P01 rather than text | false | rejected | A stable SQLSTATE is equivalent or stronger than message text. |
| Extension test has no explicit assertion | low | rejected | It fails if `CREATE EXTENSION` throws, which is the exact failure found in entry 1.2. |

## Design Notes

`roles.sql` reads its passwords from the environment of the psql process, and processes started with `exec` inherit the container's environment, so the fixture sets the three variables on the container and runs `psql -f` inside it; it copies `roles.sql` in rather than rewriting it. The tests connect from the host over the mapped TCP port so password checks are real.

## Verification

**Commands:**
- `dotnet build LeaveWorkforce.slnx` -- expected: 0 warnings, 0 errors
- `dotnet test --solution LeaveWorkforce.slnx` -- expected: Api.IntegrationTests passes; Architecture.Tests and Concurrency.Tests report zero tests (exit 8) until entries 1.6 and 1.7
- `docker ps -a --filter label=org.testcontainers=true` after the run -- expected: no leftover container
