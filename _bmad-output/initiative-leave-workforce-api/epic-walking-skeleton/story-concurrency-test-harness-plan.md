---
title: 'Concurrency test harness'
type: 'chore'
ticket: '7'
created: '2026-10-12'
status: 'built'
baseline_revision: '079f8a8d320025ebb9a4079566ae4146556219c1'
route: 'full'
route_source: 'auto'
risk: 'low'
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

**Problem:** Epics 3 to 8 must prove that races leave the invariants intact, but the project has no shared way to start several operations at the same moment against PostgreSQL and see how each one ended.

**Approach:** Add a small helper to `Concurrency.Tests` that starts a set of operations together behind a start gate, waits for all of them, and reports every outcome without throwing. Reuse the existing PostgreSQL fixture unchanged. Prove the helper with sample tests, including two operations racing on a real database.

## Boundaries & Constraints

**Always:** Start every operation only after all are created, released together by one gate. Report one outcome per operation, in order: the value, the exception if it failed, or that it timed out. A failing or hung operation must not hide the others and must not make the helper throw. Apply a timeout (30 seconds by default) that cancels what is still running and reports it as timed out. Reuse `PostgresFixture` from `Api.IntegrationTests` exactly as it is, through a collection fixture defined in `Concurrency.Tests`. Credentials stay the fixture's random test-only values. Scratch tables are created by migrator, used by app_rw, and dropped afterwards. Follow the existing xunit.v3 style.

**Never:** No leave, account or audit logic, no stress iteration counts or invariant queries (epic 8 entry 7 sets them), no change to the fixture, `roles.sql` or any permission, no migrations, no skipped tests, no generic test framework beyond the one helper.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Parallel start | N operations | None runs its body before all are created; all are released together | Not applicable |
| Both outcomes reported | Two operations that succeed | Two successful outcomes with their values, in order | Not applicable |
| Failure reported | One operation throws | Its exception is in its outcome; the other outcome is intact; the helper does not throw | Not applicable |
| Timeout | One operation never finishes | After the timeout it is cancelled and reported as timed out; the others are reported normally | Not applicable |
| Real parallelism | Two operations on PostgreSQL that wait for each other | Both finish on different backend processes | A serial run would time out |
| Database race | Two inserts of the same unique key | One winner, one loser reported with SQLSTATE 23505, one row stored | Not applicable |
| Cleanup | Test run ends | Scratch table dropped, container removed | Verified by listing containers |

</frozen-after-approval>

## Code Map

- `tests/Concurrency.Tests/` -- xunit.v3 shell with no tests; already references `Api.IntegrationTests` (added in entry 1.1), which holds the public `PostgresFixture` and the `Postgres` collection name.
- `tests/Api.IntegrationTests/Infrastructure/PostgresFixture.cs` -- starts the pinned container, applies `roles.sql`, exposes `ConnectionString(role)` for migrator and app_rw with pooling off; do not change it. xunit.v3 discovers collection definitions per test assembly, so define a new collection with `ICollectionFixture<PostgresFixture>` here.
- `tests/Api.IntegrationTests/UnitOfWork/UnitOfWorkTests.cs` -- the scratch-table pattern (migrator creates a table in schema `leave`, app_rw uses it, migrator drops it) to copy.
- Spine AD-4 and AD-20 (Concurrency.Tests covers the PRD section 6 races later).

## Tasks & Acceptance

**Execution:**
- [x] `tests/Concurrency.Tests/Infrastructure/ParallelRunner.cs` -- the helper and the outcome type -- the harness
- [x] `tests/Concurrency.Tests/Infrastructure/ConcurrencyCollection.cs` -- collection definition using the shared fixture -- reuse the container
- [x] `tests/Concurrency.Tests/ParallelRunnerTests.cs` -- database-free tests of the helper (gate, both outcomes, failure, timeout) -- helper behaviour
- [x] `tests/Concurrency.Tests/DatabaseRaceSampleTests.cs` -- two samples on PostgreSQL: separate backends, and the unique-key race -- the ticket's sample
- [x] `README.md` -- one short note on running concurrency tests -- documentation

**Acceptance Criteria:**
- Given two operations, when the helper runs them, then both outcomes are returned in order.
- Given one operation that throws, when run, then the helper returns normally with that exception in its outcome and the other outcome intact.
- Given an operation that never finishes, when the timeout passes, then it is reported as timed out and the call returns.
- Given two database operations that wait for each other, when run, then both finish on different backend processes.
- Given two inserts of the same unique key, when run together, then exactly one succeeds, the other is reported with SQLSTATE 23505, and one row exists.
- Given the solution, when `dotnet build LeaveWorkforce.slnx` and `dotnet test --solution LeaveWorkforce.slnx` run, then the build has 0 warnings, every project passes, and `dotnet test` no longer exits 8.

## Implementation Notes

- Added under `tests/Concurrency.Tests/`: `Infrastructure/ParallelRunner.cs` (`ParallelRunner.RunAsync` and `Outcome<T>`), `Infrastructure/ConcurrencyCollection.cs` (the `Concurrency` collection over the unchanged `PostgresFixture` from `Api.IntegrationTests`), `ParallelRunnerTests.cs` (helper tests, no database) and `DatabaseRaceSampleTests.cs` (two PostgreSQL samples). README gained a "Concurrency tests" section and its `dotnet test` line lost the obsolete "exits 8" comment.
- How the helper behaves: every operation starts waiting on one start gate as soon as it is created and none runs its body until all exist; one outcome per operation is returned in order (value, exception, or timed out); a failing or hung operation never makes the helper throw; one timer (30 seconds by default) decides the timeout: whatever has not finished when it fires is reported as timed out however it ends afterwards, after a 2-second grace for cooperative operations; an operation that cancels itself before the timeout is a failure with its exception, not a timeout; if the caller's token is cancelled the operations are cancelled and the call throws `OperationCanceledException`.
- Samples on PostgreSQL: two operations that wait for each other finish on different backend PIDs; two inserts of one unique key give one winner, one loser with SQLSTATE 23505, and one stored row (scratch table `leave.race_scratch`, created by migrator, used by app_rw, dropped afterwards).
- Review and my own verification changed the helper after the first implementation: the original two competing timers and the way cancellation was classified were replaced by the single-timer design above. A new caller-cancellation test was flaky (it failed on 5 of 5 and then 2 of 3 runs) because deciding "cancelled while running" through a registration callback depended on callback order, since `Task.Delay`'s own callback resumes the awaiting code first; I replaced it with the deterministic rule that a cancelled caller token always propagates. The final version passed 6 consecutive runs of `Concurrency.Tests` and 4 consecutive full-solution runs.
- Result: 94 of 94 tests pass (Architecture 19, Api.IntegrationTests 63, Concurrency 12), `dotnet test` exits 0 for the whole solution for the first time since entry 1.1, the build has 0 warnings, and no container remains after the reaper finishes. `PostgresFixture`, `roles.sql`, permissions and migrations are untouched.

## Plan Change Log

## Review Triage Log

Quick review, pass 1: 9 findings (medium 4, low 4, rejected or n/a 1).

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| An operation's own cancellation was reported as timed out, hiding its exception | medium | patch | Cancellation is a timeout only for operations still running when the runner's timer fires; otherwise it is a failure carrying an exception; a test covers it. |
| A failure that followed the timeout cancellation was reported as a failure, not a timeout | medium | patch | The runner snapshots what is unfinished when the timer fires and reports those as timed out however they end; a test covers an operation that wraps the cancellation in another exception. |
| Two competing timers made the grace period non-deterministic | medium | patch | One timer only. |
| The "all created before release" test passed even with no gate | medium | patch | Replaced by a test using a list that hands out its elements slowly; every body must start after the last element was handed over. |
| A late caller cancellation could discard computed outcomes | low | patch | Settled as: a cancelled caller token always propagates. My first attempt (a flag set from a registration callback) was flaky because callback order is not guaranteed, so I used the simple rule. |
| No test for an operation's own cancellation | low | patch | Added with the first fix. |
| Null `operations` not validated | low | patch | One-line guard and a test. |
| README line had trailing whitespace | low | patch | Fixed. |
| Timing-dependent tests; plan checkboxes and notes empty | n/a | rejected | Timeouts are inherent to testing a timeout; bookkeeping done with this commit. |

## Design Notes

The start gate is a `TaskCompletionSource` that every operation awaits before it runs its body; the helper releases it after creating all operations, so contention begins together. The timeout uses a linked cancellation token so a hung operation can be reported without abandoning the others.

## Verification

**Commands:**
- `dotnet build LeaveWorkforce.slnx` -- expected: 0 warnings, 0 errors
- `dotnet test --solution LeaveWorkforce.slnx` -- expected: all three test projects pass and the overall exit code is 0
- `docker ps -a --filter label=org.testcontainers=true` after the run -- expected: no leftover container
