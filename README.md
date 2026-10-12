# leave-workforce-api
Implimenting using BMAD

## Build and run

Requires the .NET SDK pinned in `global.json` (10.0.103).

```sh
dotnet build LeaveWorkforce.slnx
dotnet run --project src/Api --urls http://127.0.0.1:5199
curl -i http://127.0.0.1:5199/health   # 200 {"status":"up"}
dotnet test --solution LeaveWorkforce.slnx
```

The connection string is read from `ConnectionStrings:App` (for example the `ConnectionStrings__App` environment variable); do not commit secrets.

## Local PostgreSQL

Requires Docker with Compose v2 (Colima works). The image is pinned once in `deploy/postgres.env` (`postgres:18`). The API does not connect to the database until entry 1.3.

```sh
cp deploy/db/dev.env.example deploy/db/dev.env   # then replace every CHANGE_ME; the file is gitignored
```

`deploy/db/dev.env` must set `POSTGRES_DB`, `POSTGRES_PORT`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `MIGRATOR_PASSWORD`, `APP_RW_PASSWORD` and `RETENTION_JOB_PASSWORD` (simple values; placeholders are refused). The port is bound to 127.0.0.1 only.

```sh
deploy/db/db.sh up       # start PostgreSQL 18 and apply deploy/db/roles.sql
deploy/db/db.sh down     # stop; data stays in the volume
deploy/db/db.sh reset    # stop and delete the volume; the next `up` is a fresh database
deploy/db/db.sh roles    # rerun roles.sql (idempotent; re-applies passwords)
deploy/db/db.sh verify   # check version, roles, wrong passwords and privileges
deploy/db/db.sh psql     # psql as the superuser inside the container
```

Connection string example, with the password taken from the environment:

```sh
set -a; . deploy/db/dev.env; set +a   # loads the values below into your shell
export ConnectionStrings__App="Host=127.0.0.1;Port=$POSTGRES_PORT;Database=$POSTGRES_DB;Username=app_rw;Password=$APP_RW_PASSWORD"
```

## Integration tests

`dotnet test --project tests/Api.IntegrationTests` needs a running Docker daemon; there is no skip when it is missing, the tests fail with a message saying so. With Colima, set `DOCKER_HOST=unix://$HOME/.colima/docker.sock` (Testcontainers honours it).

The `PostgresFixture` (one per `Postgres` collection) starts a disposable container from the image pinned in `deploy/postgres.env`, applies the unchanged `deploy/db/roles.sql` with `psql` inside the container, and exposes connection strings for the superuser and the three roles over the mapped TCP port. Every password is random per run and is never printed. `ApiFactory` is a `WebApplicationFactory<Program>` that sets `ConnectionStrings:App` to the `app_rw` connection string. Both types are public so `Concurrency.Tests` can use them.

Cleanup is automatic: the fixture disposes the container, and the Testcontainers reaper (Ryuk) removes it even if the test process is killed. Check with `docker ps -a --filter label=org.testcontainers=true`; only the long-lived Ryuk container itself may remain, and it removes itself when idle.

## Unit of work, advisory locks and paging

`BuildingBlocks` holds the request pipeline pieces (AD-3, AD-4, AD-18, AD-22). `UnitOfWorkRunner.ExecuteAsync` opens one connection and one READ COMMITTED transaction with `SET LOCAL lock_timeout` (`UnitOfWorkOptions.LockTimeout`, default 5 seconds), runs the handler, and commits only on a success `Result`; a failure `Result` or exception rolls back. On SQLSTATE 23505, 23P01, 40001, 40P01 or 55P03 (anywhere in the exception chain) the handler is re-run once in a fresh transaction; a second failure returns a Conflict (`lock_timeout` for 55P03, otherwise `concurrency_conflict`). A handler enlists an EF Core `DbContext` with `scope.EnlistAsync(context, ct)` and never commits.

`scope.Locks.AcquireAsync(ct, keys)` takes `pg_advisory_xact_lock` on `AdvisoryLockKey` values in the fixed order Administrator, then Tree. Re-taking a held key is harmless; asking for a lower key after a higher one, or locking outside an active unit of work, throws `InvalidOperationException`. Locks end with the transaction.

`PageRequest.Create(page, pageSize)` defaults to page 1, size 25, caps size at 100, and returns a Validation error for a page or size below 1. `Paged<T>` serialises as `{ "items": [...], "total": n }`.

## Architecture tests

`tests/Architecture.Tests` enforces AD-1 and AD-2 with ArchUnitNET: the `src` project reference graph (read from the `.csproj` files), type dependencies pointing only downwards (People, Leave, Audit, BuildingBlocks) and across modules only into `LeaveWorkforce.<Module>.Contracts`, only `Contracts` types being public in a module, `DbContext` types being internal and unused outside their module, and `LeaveWorkforce.<Name>` assembly and namespace naming. Each rule is a function in `ArchitectureRules` that is also run on deliberately bad fixture types (`Fixtures/`) to prove it can fail. The top-level `Program` entry point of the two hosts is exempt from the namespace rule because the compiler places it in the global namespace.

## Concurrency tests

`tests/Concurrency.Tests` needs Docker like the integration tests (same `DOCKER_HOST` note) and reuses `PostgresFixture` through its own `Concurrency` collection. `ParallelRunner.RunAsync(operations, timeout, ct)` creates every operation, releases them together through one start gate, and returns one `Outcome<T>` per operation in order: the value, the exception, or `TimedOut` (default 30 seconds; still-running operations are cancelled). It does not throw because an operation failed or hung. Scratch tables are created by `migrator`, used by `app_rw` and dropped afterwards.
