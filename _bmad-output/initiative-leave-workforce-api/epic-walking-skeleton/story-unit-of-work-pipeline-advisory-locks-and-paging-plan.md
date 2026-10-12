---
title: 'Unit-of-work pipeline, advisory locks and paging'
type: 'feature'
ticket: '5'
created: '2026-10-12'
status: 'built'
baseline_revision: '24566e338debef1170ddedf9a06ce6c1ff121085'
route: 'full'
route_source: 'auto'
risk: 'high'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md'
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/epic-walking-skeleton/story-testcontainers-integration-test-harness-plan.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every state-changing handler needs one connection and one transaction that the pipeline owns, a safe way to take the advisory locks of AD-4, one retry on transient database conflicts, and the paging rules of AD-18. None exists, and getting them wrong breaks every later invariant.

**Approach:** Add to `BuildingBlocks` a unit-of-work runner that begins, commits or rolls back a READ COMMITTED transaction with a lock timeout and retries the handler once on the AD-22 SQLSTATEs, an advisory-lock helper bound to that transaction, a way to enlist an EF Core DbContext in it, and paging types. Prove them against the existing PostgreSQL harness.

## Boundaries & Constraints

**Always:** The runner, not the handler, begins and commits. A failure Result or an exception rolls back; nothing commits on failure in this entry. READ COMMITTED, with `SET LOCAL lock_timeout` from an option defaulting to 5 seconds. Retry exactly once, in a fresh transaction, on SQLSTATE 23505, 23P01, 40001, 40P01 or 55P03, found anywhere in the exception chain; after the second failure return a Conflict Error: code `lock_timeout` for 55P03, a new `concurrency_conflict` for the others, with no database detail. Other exceptions are not retried. Advisory locks use `pg_advisory_xact_lock` on named keys in the fixed order Administrator then Tree; a call takes its keys in that order, re-taking a held key is harmless, and asking for a lower key after a higher one is an `InvalidOperationException`; taking a lock outside a transaction is an `InvalidOperationException`. Locks end with the transaction on success, failure, cancellation and disposal. `PageRequest` defaults to page 1, size 25, caps size at 100, and refuses page or size below 1 with a Validation Error; `Paged<T>` serialises as `items` and `total`.

**Never:** No leave, account or audit logic, no authentication, no CommitOnFailure flag or audit flush hook (epic 2 entry 1), no row-lock helper, no savepoints, no endpoint filter or generic mediator framework, no change to `roles.sql` or any permission, no migrations (the test creates and drops its own scratch table), no registration or database connection in the Api.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Success | Handler returns a success Result | Changes are committed | No error expected |
| Failure Result | Handler returns a failure | Changes are rolled back; the failure is returned | No error expected |
| Exception | Handler throws a non-retryable exception | Rolled back; the exception propagates; no retry | Not applicable |
| Retryable once | Handler fails with a listed SQLSTATE on the first attempt | Handler runs again in a fresh transaction and succeeds | No error expected |
| Retryable twice | Listed SQLSTATE on both attempts | Conflict Error returned: `lock_timeout` for 55P03, else `concurrency_conflict` | 409 through the mapper |
| Serialise | Two operations take the same advisory key | The second waits until the first ends | Lock timeout gives the retry path |
| Lock order | Tree requested after Administrator held, or Administrator after Tree | Allowed in order; backwards throws | InvalidOperationException |
| Lock re-take | Same key taken twice | No error, no deadlock | Not applicable |
| Outside transaction | Lock requested with no active unit of work | Refused | InvalidOperationException |
| Cancellation | Token cancelled while waiting for a lock | Operation cancelled; no advisory lock remains | OperationCanceledException |
| Paging | Size null, 500, 0; page 0 | 25, 100, Validation Error, Validation Error | 400 through the mapper |

</frozen-after-approval>

## Code Map

- `src/BuildingBlocks/` -- holds `Results/` (Result, Error, ErrorCodes with `lock_timeout`) and `Http/` (mapper); add `UnitOfWork/` and `Paging/` here; it references the ASP.NET Core framework only, so add `Npgsql` and `Microsoft.EntityFrameworkCore.Relational`.
- `tests/Api.IntegrationTests/Infrastructure/PostgresFixture.cs` -- the existing collection fixture with per-role TCP connection strings (pooling off) and migrator/app_rw roles; use app_rw for the runner and migrator to create a scratch table in schema `leave`.
- `tests/Api.IntegrationTests/Database/PostgresHarnessTests.cs` -- style to follow (`Collection(PostgresCollection.Name)`, `TestContext.Current.CancellationToken`).
- `Directory.Packages.props` -- central versions; add Npgsql EF Core provider 10.0.3 and EF Core 10.0.x at least 10.0.4 (resolve from NuGet); Npgsql 10.0.3 and Testcontainers are already pinned.
- Spine AD-3, AD-4, AD-22, AD-18; EF Core 10 has `Database.UseTransactionAsync` and `SetDbConnection`; `EnableRetryOnFailure` stays off.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props`, `src/BuildingBlocks/*.csproj`, `tests/Api.IntegrationTests/*.csproj` -- add the packages -- the first EF Core and Npgsql dependencies
- [x] `src/BuildingBlocks/UnitOfWork/` -- the runner with its scope (connection, transaction, enlist a DbContext, advisory locks), options, the retry classification and the Conflict mapping, plus `AdvisoryLockKey` and the lock helper -- AD-3, AD-4, AD-22
- [x] `src/BuildingBlocks/Results/ErrorCodes.cs` -- add `concurrency_conflict` -- the stable code
- [x] `src/BuildingBlocks/Paging/` -- `PageRequest` and `Paged<T>` -- AD-18
- [x] `tests/Api.IntegrationTests/UnitOfWork/` and `tests/Api.IntegrationTests/Paging/` -- tests for every matrix row using a test-only DbContext on a scratch table -- verification
- [x] `README.md` -- short note on the pipeline, locks and paging -- documentation

**Acceptance Criteria:**
- Given a handler that writes with a test-only DbContext, when it returns success the write is visible, and when it returns a failure or throws the write is gone.
- Given a handler that fails once with a listed SQLSTATE, when run, then it runs twice and succeeds; given it fails twice with 55P03, then the result is Conflict with `lock_timeout`.
- Given two operations on one advisory key, when run together, then they never overlap.
- Given any end of the unit of work, when `pg_locks` is read afterwards, then no advisory lock remains.
- Given paging inputs, when created, then they behave as the matrix says.
- Given the solution, when built, then there are 0 warnings and the existing tests still pass.

## Implementation Notes

- Risk was raised from the ticket's medium to high during planning: every later handler depends on these transaction and lock semantics.
- Packages (first EF Core and Npgsql dependencies in `BuildingBlocks`): `Npgsql` 10.0.3, `Microsoft.EntityFrameworkCore.Relational` 10.0.4 (the minimum the Npgsql EF Core provider 10.0.3 needs; the latest 10.0.x on NuGet is newer), and the provider `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 in the test project only.
- `src/BuildingBlocks/UnitOfWork/`: `UnitOfWorkRunner` (`ExecuteAsync` for `Result` and `Result<T>`; one READ COMMITTED transaction per attempt; transaction-local `lock_timeout` via `set_config`; commit on success, rollback on failure Result or exception; retry once on SQLSTATE 23505, 23P01, 40001, 40P01 or 55P03 found in the exception chain; final failure is a Conflict with `lock_timeout` for 55P03 and `concurrency_conflict` for the rest, no database detail), `IUnitOfWorkScope` (connection, transaction, `EnlistAsync(DbContext)` through `SetDbConnection` and `UseTransactionAsync`, `Locks`), `IAdvisoryLocks` with `AdvisoryLockKey` (Administrator = 1, Tree = 2, stored in a namespaced bigint space), `UnitOfWorkOptions` (lock timeout, 5 seconds by default).
- Lock rules: keys in a call are taken in ascending order; a held key is skipped; asking for a lower key after a higher one is an `InvalidOperationException`; after the scope ends, locks and enlistment are refused. Locks are transaction-scoped, so they end on commit, rollback, exception, cancellation and disposal; tests read `pg_locks` after each.
- `src/BuildingBlocks/Paging/`: `PageRequest.Create(int?, int?)` and `Paged<T>` (`items`, `total`). `ErrorCodes` gained `concurrency_conflict` and `invalid_paging` (the Validation Error needs a stable code).
- Not registered in DI and not used by the Api: the Api still has no database connection. `CommitOnFailure` and the audit flush hook arrive with epic 2 entry 1.
- Tests (`tests/Api.IntegrationTests/UnitOfWork` and `Paging`, 31 new): every matrix row against the Testcontainers PostgreSQL, using a test-only DbContext on a `leave.uow_scratch` table that migrator creates and drops. Final: 63 of 63 pass (twice in a row); the build has 0 warnings; no leftover container; Architecture.Tests and Concurrency.Tests still report zero tests (exit 8) until entries 1.6 and 1.7.
- The `pg_locks` checks are database-wide, which is safe because test collections run serially and no other advisory-lock user shares the container.

## Plan Change Log

## Review Triage Log

Quick review, pass 1: 7 findings (high 0, medium 1, low 3, rejected 3).

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| `PageRequest.Offset` overflows for a huge page and would give a 500 | medium | patch | `Create` now refuses a page whose offset would exceed `int.MaxValue` with a Validation Error; a test covers `int.MaxValue`. |
| A handler returning null gave a bare `NullReferenceException` | low | patch | The runner now throws a clear `InvalidOperationException` before committing; a test checks it is not retried. |
| The second-failure theory omitted 23505 | low | patch | Added the case. |
| The plan authorises only `concurrency_conflict`, but `invalid_paging` was added | false | rejected | `Error` requires a non-blank code and the plan asks for a Validation Error, so a code is unavoidable. |
| `Connection` and `Transaction` remain reachable on a scope after it ends | low | rejected | Needs a handler to hold the scope past the runner; locks and enlistment are refused after the end and a stricter guard adds complexity. |
| The overlap test shares a plain bool between concurrent handlers | false | rejected | It is read only after both tasks complete, and the shared counter already uses `Interlocked`. |
| Plan checkboxes and notes empty | n/a | n/a | Bookkeeping, done with this commit. |

## Design Notes

Retry classification walks `InnerException` because EF Core wraps `PostgresException` in `DbUpdateException`. Lock-order checking keeps the set of keys held in the current scope; it is a guard against programmer error, not a replacement for the database.

## Verification

**Commands:**
- `dotnet build LeaveWorkforce.slnx` -- expected: 0 warnings, 0 errors
- `dotnet test --solution LeaveWorkforce.slnx` -- expected: all Api.IntegrationTests pass; Architecture.Tests and Concurrency.Tests report zero tests (exit 8) until entries 1.6 and 1.7
- `docker ps -a --filter label=org.testcontainers=true` after the run -- expected: no leftover container
