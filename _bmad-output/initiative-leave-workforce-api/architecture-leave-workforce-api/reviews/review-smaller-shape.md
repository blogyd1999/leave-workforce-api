# Review: smaller-shape update (People merge, no Contracts projects, three DB roles, chains deferred)

Reviewed: spine and companion `solution-structure.md` as updated 2026-10-12, against the final PRD.

## Verdict

The shrink holds. There is no circularity and no critical finding. Three high findings are real gaps that would make two builders diverge or leave a race open. Each takes a few sentences to fix and none needs a redesign. They should be closed before the spine is finalized, so strictly they block finalizing, but only as spine edits.

Counts: critical 0, high 3, medium 7, low 5.

## Answers to the seven checks

1. Direction: no circularity. `People` references `Leave.Contracts` and `Audit.Contracts`. `Leave` references only `Audit.Contracts`. The dotted line is dependency inversion and adds no compile-time reference. Leave does need three things from People: manager identity, active state, and names (see H2 and M5). The Administrator role is never a Leave decision. It is only a policy and visibility flag, supplied by the caller context, not by a port. Leave is clean as long as the `CurrentAccount` type lives in BuildingBlocks (M5).
2. "Internal except Contracts" does not yet let the hosts reach what they need (H1).
3. Lock order: the listed flows do not deadlock, and I found no cycle. They are incomplete, which leaves a real race (H3, M4).
4. Chains in increment 7: nothing in FR-25, FR-26, SEC-19 or SM-4 needs a chain. SM-4 only needs an audit-write failure to roll back. SEC-23 says tamper evidence must be defined before the audit module is built. AD-12 defines it only loosely, and the audit schema is built in 1b without the hash columns. That is a hole, rated M2 and not blocking.
5. Three roles work. `app_rw` can run the Api and every Operator command except `migrate` and `purge-audit`. The database enforces INSERT and SELECT only on `audit`. The catch is the audit class lock (M1). `IAuditRetention` is also visible too widely (M7).
6. Leftover contradictions: no project-list, schema or role-name mismatch. The project count, six sources and three tests, is consistent. The contradictions that remain are listed under H1, H2, M3 and L1 to L3. Every FR and SEC has an owner. FR-32 start-up audit, FR-25 purge and the audited reads have an owner module but no increment (M3).
7. Increments 1a and 1b are buildable in order, with the testability caveats in M6.

## High

### H1 (high). Hosts cannot reach internal handlers or the DbContext-only pieces
- AD-14 and the companion say the Operator "acts through public Contracts and People handlers". But handlers live in `People/Features/...` and are `internal`, and AD-2 says only the `Contracts` namespace is public.
- The Api also needs `AddPeople`, `MapPeopleEndpoints`, a `CurrentAccount` reader (the per-call read of Account, Credential and Session, all People tables), the expiry-sweeper entry point, and a migration entry point per module. `migrate` must run each module's EF migrations, and the `DbContext`s are internal.
- AD-18 requires a public `MapXEndpoints` per module. That conflicts with the architecture test "only Contracts namespaces are public".
- The companion says only "`People.Contracts`: what the hosts and the sweeper need". Two builders will invent different surfaces.
- Smallest fix: add one sentence to AD-2 and AD-14 enumerating the host-facing surface, all in `<Module>.Contracts` or in one `<Module>Module` static class in that namespace.
  - Every module exposes `Add<Module>(...)`, `Map<Module>Endpoints(...)` (not Audit) and `IModuleMigrator`.
  - `People.Contracts` exposes `ICurrentAccountReader`, `IOperatorOperations` (`Bootstrap`, `IssueTemporaryPassword`, `RecoverAdministrator`) and `IPrivilegedRequestExpiry`.
  - Add one line to the architecture tests: the allowed public types outside those names are none.

### H2 (high). AD-4 and AD-6 require every handler to re-lock the Account, but Leave cannot touch People tables
- AD-6 says every state-changing handler re-locks the Account and re-validates it. AD-4 puts Account rows at step 2, before the balance at step 3.
- Submit, cancel, approve and reject are Leave handlers (the companion even says Approve is "Leave only"). Leave has no Account access, and the ports (`IRelationshipDirectory`, `IAccountNames`) have no lock or active-state method.
- FR-14 requires that a request "cannot be submitted by an Account that is being deactivated". FR-14 also requires a refusal when the Direct Manager is inactive. FR-19 and FR-21 require the current Direct Manager and active state evaluated on committed data.
- Smallest fix: add one port method, `IAccountState.LockAndGet(ids ascending)`, declared in `Leave.Contracts` and implemented by People. It takes `FOR UPDATE` on the Account rows and returns active state and Direct Manager. Leave handlers call it first (step 2), then lock the balance (step 3).
  - This keeps the order Account, Balance, Request, Audit, and Deactivate already runs in that order.
  - State the rule in AD-24: caller Account, owner Account and, for submit, nothing else. A Manager with active Direct Reports cannot be deactivated, so the manager's active state is read, not locked.

### H3 (high). A manager link can be written without the tree lock, which breaks the SM-2 invariant "every active Employee's Direct Manager is active"
- The CreateAccount flow (with an optional manager) takes no tree lock and does not lock the manager Account.
- Race: Deactivate(M) takes the administrator and tree locks and counts active Direct Reports as 0. Concurrently, Create(E, manager M) reads M as active and inserts the link. Both commit, leaving an active E under an inactive M. This is the PRD §6 "assigning a manager versus deactivating that manager" race, reached through a different handler.
- ReactivateAccount is also missing from the flows table. FR-4 requires its manager-active check. Reactivate(E) versus Deactivate(M) is a PRD §6 race.
- Smallest fix: AD-4 gets one sentence: any flow that writes or relies on a manager link (create with manager, assign, reassign, clear, reactivate, deactivate) takes the tree lock and locks the manager Account in ascending id with the others. Add Reactivate and the manager lock to the companion section 4 table.

## Medium

### M1 (medium). The audit per-class lock cannot be a row lock under `app_rw`
- PostgreSQL requires UPDATE privilege for `SELECT ... FOR UPDATE`. `app_rw` has only INSERT and SELECT on `audit`, so a row lock on the head record fails.
- AD-12 says "a per-class lock" without saying what it is.
- Fix: state that class locks are `pg_advisory_xact_lock` keys (Ordinary, then Security), taken at flush. The seq is `max(seq)+1` read after the lock, backed by a unique index on `seq`. No extra privilege is needed.

### M2 (medium). Tamper evidence is "defined" only loosely before the audit module is built (SEC-23)
- AD-12 names the algorithm, but the canonical record encoding, the genesis value and the head-locking step are not defined. The audit tables are built in 1b without `prev_hash` and `hash`.
- Adding NOT NULL columns later forces a backfill of every record already written.
- Smallest fix, either of:
  - (a) in 1b create the `prev_hash` and `hash` columns and have the writer compute the hash while it already holds the class lock (a few lines), leaving only `verify` and the anchor-aware purge for 7; or
  - (b) add to AD-12 the canonical bytes (fixed field order, UTF-8, UTC ISO timestamps), the genesis value, and a statement that increment 7 starts the chain at an anchor with a documented "chained from seq N" marker.
- Option (a) is smaller in total. Deferral does not block finalizing once either is recorded.

### M3 (medium). Several PRD-required pieces have no increment
- `purge-audit` and `verify-audit` (FR-25 retention, SEC-23) are not in 1b and not named in 7.
- The FR-32 start-up configuration audit is not in any increment.
- "Audited reads" are placed in 7, but the Administrator reads of a balance (FR-13, increment 3) and of requests (FR-24, increment 6) must be audited when those endpoints ship.
- Fix: put purge, verify (a stub until chains arrive) and the FR-32 start-up audit in increment 7, and require the audited-read behaviour with each read endpoint.

### M4 (medium). The companion flows table omits locks that AD-6 and AD-4 require
- ResetPassword locks "Account" only. It must lock both caller and target in ascending id, and check the target is not an Administrator. It does not change the administrator count, so no administrator lock is needed.
- AssignManager must lock employee and manager Accounts in ascending id.
- `MovePending` updates request rows without taking the balance lock. There is no deadlock, but make it lock the Employee's balance first for uniformity.
- Reactivate and recover-admin (with role restore) should be listed. Both take the administrator lock.

### M5 (medium). Where `CurrentAccount` lives is unstated
- Leave handlers need the caller's id, Administrator flag and session. They cannot see `People.Contracts`.
- Fix: define the `CurrentAccount` record in BuildingBlocks. `ICurrentAccountReader` in `People.Contracts` fills it.

### M6 (medium). Testability of 1b and the late concurrency tests
- `bootstrap` and `issue-temp-password` refuse a non-interactive stdout (AD-14). Tests need a seam, for example an injected `ITerminal`, to run them. No Operator test project or host is named.
- Increment 2 stubs `CancelPendingFor` and `MovePending`, so lock-order bugs would not show until the increment 4 stress project. AD-20 puts `Concurrency.Tests` in 7.
- Fix: create `Concurrency.Tests` in 1a with the bootstrap race. Add the race for each increment as it lands (SM-8, SM-2). Add the `ITerminal` seam to AD-14.

### M7 (medium). `IAuditRetention.Purge` is visible to every module and the Api
- The database role blocks it for `app_rw`, so the data is safe. Code-level separation relies on convention.
- Fix: one architecture test, "only Operator references `IAuditRetention`", or put it in a separate namespace such as `Audit.Contracts.Retention`.

## Low

- L1. `admin_window`: who inserts the single row is not stated. State "seeded by `migrate`, `closed = false`".
- L2. AD-14 and the spine's Operator list include `verify-audit`, but it does nothing useful until increment 7. Mark it as a stub until then.
- L3. The Approve row in the companion does not name the caller's own re-validation (H2). It should read "lock caller Account (via port) → Balance → Request".
- L4. AD-22 re-runs a handler once after a 55P03 lock timeout. Worst-case wait is about 10 seconds. Acceptable, but state it.
- L5. The unkeyed SHA-256 chain detects accidental or partial edits and not a database owner who recomputes the whole chain. The Deferred list already names immutable anchoring. Add a one-line note to AD-12.

## Changes that must not be undone
The fixes above must not reintroduce a DbContext reference outside its module, a role claim, or a second path that changes Account state (AD-21). The earlier-review fixes still hold: lock order, advisory-lock-first, `CommitOnFailure`, and the Operator not being an HTTP host.
