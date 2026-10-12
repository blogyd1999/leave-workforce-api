# Recheck of the revised spine (2026-10-12)

Reviewed: revised `architecture-leave-workforce-api.md` (AD-1..AD-25) and `solution-structure.md`, against the three earlier reviews. No existing file was changed.

## Verdict
Two NEW high findings (N-H1, N-H2) block finalizing until a few sentences are added. Both are lock-order contradictions between the ADs and the listed flows. Neither needs a redesign: about four sentences in AD-4, AD-12 and companion section 4 close them. No critical finding remains. None of the 21 earlier critical/high findings is unresolved.

Earlier critical/high: 21. Resolved 16, partly 5, not resolved 0.
New: critical 0, high 2, medium 6, low 8.

## Part 1. Earlier critical and high findings

### review-rubric.md
| ID | Status | Justification |
| --- | --- | --- |
| C1 Identity needs Organisation | resolved | AD-21: "`CreateAccount`, `ResetPassword` (needs Organisation's 'own Direct Manager' check) and the manager-joined account list are `Administration` slices"; `IAccountCommands` added. |
| H1 retention_job grants | partly | AD-13 now gives `retention_job` "SELECT and DELETE ... INSERT on `chain_anchors` and on the Security-Class record table". Still no statement that `purge-audit` runs as `retention_job`, and no purge contract (see N-M2). |
| H2 commit on failure | resolved | AD-22: "a closed list of flows that set `CommitOnFailure`: failed-login back-off ..., refresh-token reuse revocation, and audited Operator refusals". |
| H3 common-password list | partly | AD-8: "bundled offline top-10,000 list (source confirmed at scaffold)". Mechanism fixed; the comparison rule (case-fold, exact match) is not stated (N-L5). |
| H4 operational envelope | resolved | AD-25 covers environments, TLS, provisioning order, manual purge and verify cadence, the runbook as an increment-1 deliverable, and backup deferral. |
| H5 audited reads | resolved | AD-5: "the only write a query may make is an Audit Record for an audited read ... through `IAuditWriter` inside a unit of work". |
| H6 diagram vs AD-1 vs companion | resolved | The diagram now draws Operator to Leave, `OrgC` to `IdentC`, contracts to BuildingBlocks. Bootstrap goes through Administration (AD-21). Small omissions remain (N-L1). |
| H7 two sources of truth for manager | resolved | AD-24: "`Organisation.ManagerLink` is the source of truth ... `MovePending` ... only updates `assigned_manager_id`". |

### review-version-verification.md
| ID | Status | Justification |
| --- | --- | --- |
| H1 FOR UPDATE mechanism | resolved | AD-4: "raw `SELECT ... FOR UPDATE` inside the shared transaction ... entities are loaded after the lock is held ... `EnableRetryOnFailure` is off", `lock_timeout` 5 s. |
| H2 HS256 | resolved | AD-6: "justified because the Api is the sole issuer and sole verifier ... `ValidAlgorithms` pinned to HS256, `MapInboundClaims = false`", kid resolver. |

### review-adversarial.md
| ID | Status | Justification |
| --- | --- | --- |
| C1 lock order | partly | AD-4 now has one total order with `IAccountDirectory.Lock`. But AD-21 and companion section 4 put advisory locks inside commands, run after Account locks (N-H1), and the "load after lock" rule cannot derive the lock keys for approve, cancel and reject (N-M1). |
| C2 three creators | resolved | AD-21: "`Administration` is the only creator of Accounts, including the first Administrator (the Operator `bootstrap` calls the same Administration flow) ... `Leave` never creates a balance lazily". |
| C3 mutation paths, last-Administrator | resolved | AD-21: "The last-Administrator check, the administrator advisory lock and the window logic live inside these commands and nowhere else. Hosts and other modules never touch the Identity `DbContext`." |
| H1 audit chain | partly | AD-12 fixes `seq` as previous plus one, chain order (Ordinary then Security) and "the last step of a handler". Orchestrated flows contradict "last" (N-H2). |
| H2 event class and count | resolved | AD-12: closed `AuditActions` fixes the class, `Actor` value, "Each command writes its own events". |
| H3 commit and retry policy | resolved | AD-22: pipeline commits; "the whole handler is re-run once in a fresh unit of work"; no savepoints. One side effect is not safe to re-run (N-M5). |
| H4 unit-of-work mechanics | partly | AD-3/AD-22 and "own history table in its own schema" fix the mechanics. Default privileges for new tables and the migrate order across modules are still unstated (N-L6). |
| H5 move and cancel contract | resolved | AD-24: `CancelPendingFor(employeeId, reason, actor)` decrements Reserved and writes `leave_request.cancelled` per request; `MovePending(employeeId, newManagerId)` defined. |
| H6 CurrentAccount vs in-transaction state | resolved | AD-6: "every state-changing handler re-locks the Account and re-validates it inside its own transaction"; AD-21 `GetMany(ids)`. |
| H7 Privileged Request expiry | resolved | AD-11: "an Api hosted service sweeps expired requests every minute ... with the System actor". |
| H8 enforceable invariants | resolved | AD-23 lists the constraints (exclusion, CHECKs, unique) and names the SM-2 queries for the rest. |

## Part 2. New defects introduced by the revision

### High

**N-H1. Advisory locks sit inside commands, after Account and Balance locks, which reverses the AD-4 order (Deactivate vs Privileged Request approval).**
- AD-4 order is advisory, then Accounts, then Balances, then Requests, then audit. AD-21 puts the administrator lock inside `IAccountCommands`.
- Companion section 4, `Administration.DeactivateAccount`: `IAccountDirectory.Lock` (rank 2), then Organisation counts reports, then `Leave.CancelPendingFor` (rank 3), then `IAccountCommands.Deactivate`, which takes the administrator advisory lock (rank 1). That is backwards.
- Privileged Request approval (AD-11) takes the administrator lock, then the Accounts. Run concurrently with deactivation, this is the classic deadlock pair (40P01). One retry, then 409, so it fails under load rather than only in theory.
- The Organisation report count needs the tree lock (rank 1) before the Account locks. The flow takes none.
- Whether the target is an Administrator is only known after reading it, so the lock cannot be conditional on the read.
- `ReactivateAccount` and `Operator recover-admin` have the same shape.
- Smallest fix:
  - Add to AD-4: "Advisory locks are taken by the orchestrating handler first, through a `BuildingBlocks` `IAdvisoryLocks` helper. Commands re-take them harmlessly because `pg_advisory_xact_lock` is re-entrant within a transaction. Any flow that can change the Administrator count (deactivate, reactivate, role change, Privileged Request approval, recover-admin) takes the administrator lock unconditionally as its first lock. Flows that touch Direct Manager links or count Direct Reports take the tree lock next."
  - Rewrite the companion section 4 Deactivate row as: administrator lock, tree lock, Account locks, Organisation report count, `CancelPendingFor`, `Deactivate`, audit flush.

**N-H2. "Each command writes its own events" mid-flow acquires audit chain locks (rank 5) before later locks, contradicting AD-4 and AD-5/AD-12 "audit last".**
- AD-12 has orchestrated flows write the orchestrator's event plus "each module's own events". So `Leave.CancelPendingFor` writes `leave_request.cancelled` (Ordinary chain lock) before `Deactivate` takes its administrator, Account and session locks.
- Reassign: `Organisation` writes its audit record, then `MovePending` takes Request locks.
- Order within and between the two chains also depends on call order, not on the fixed Ordinary-then-Security rule.
- The chain lock is then held for the rest of the flow. That is the throughput and deadlock problem H1 of the adversarial review tried to remove.
- Smallest fix, one sentence in AD-12: "`IAuditWriter.Write` only enqueues the record in the unit of work. The pipeline flushes the queue just before commit (also for `CommitOnFailure` flows), taking the Ordinary chain lock then the Security chain lock, and assigns `seq` and hashes in enqueue order." AD-5 "audit last" then holds for every flow.

### Medium

**N-M1. Lock-key derivation and flow tables vs AD-4 and AD-6.**
- AD-4 says entities are "loaded after the lock is held, never before". Approve, reject and cancel receive only a request id. The employee id needed to take the Account and Balance locks must come from an unlocked read of the request.
- Companion "Approve a request" says "lock Balance → lock Request", with no Account lock, contradicting AD-6 and AD-4.
- Builders may lock the Request first. Cancel (Request then Balance) against Approve (Balance then Request) would then deadlock.
- ClearManager, ReassignManager and ResetPassword rows omit the Account locks (caller, target, new manager). ResetPassword reads the Organisation link ("is the target the caller's Direct Manager") without the tree lock, so a concurrent assign can slip past the FR-28 rule.
- Fix: add to AD-4 "an unlocked read of immutable keys (employee id, manager id) is allowed only to derive lock keys, and the row is re-read after the locks". List the Account lock step in each companion flow row. Make ResetPassword take the tree lock first.

**N-M2. Purge and verify have no contract, and roles and connections are not mapped.**
- `Audit.Contracts` lists `IAuditWriter`, `AuditActions` and `Actor`. AD-14 forbids the Operator from using a `DbContext`. `purge-audit` and `verify-audit` therefore have no way in.
- The Operator needs three connection strings: `migrator`, `operator_ops` and `retention_job`. AD-13 says "each runtime process reads its own connection string".
- `operator_ops` audit grants (INSERT for its own events, SELECT for verify) are not stated.
- The purge must take the chain lock and keep the chain head (or the writer must read `prev_hash` from the anchor when the table is empty).
- Fix: add `IAuditMaintenance` (`Purge`, `Verify`) to `Audit.Contracts`. In AD-13 state "`purge-audit` runs as `retention_job`, other Operator commands as `operator_ops`, `migrate` as `migrator`; purge takes the chain locks and never deletes the head record".

**N-M3. Failed-login "one aggregate record per source and window" cannot be built as insert-only.**
- AD-13 gives `app_rw` INSERT and SELECT only on `audit`, and the records are hash-chained. An aggregate that counts cannot be updated.
- Nothing says who writes it or when. Carries over rubric M10.
- Fix: write the aggregate on the first failure in a window, with a window id. Write one more record only when the count crosses the configured threshold in that window. Cap at a fixed number of records per window.

**N-M4. Duplicate slices and unnamed Administration endpoints.**
- The companion keeps `Organisation/Features/AssignManager` and `ClearManager` next to `Administration/Features/ReassignManager` and `ClearManager`.
- AD-18 names a `MapXEndpoints` per module, but Administration is not a module and has no stated mapper.
- Two builders may expose the same route in two places.
- Fix: AD-18 adds `MapAdministrationEndpoints`. The Organisation slices are contract commands only (no endpoint). Rename or remove the duplicate `ClearManager`. Initial assign (no current manager) is an Administration slice too.

**N-M5. Whole-handler retry (AD-22) vs the Operator issuance side effect.**
`issue-temp-password` prints once to stdout (AD-14). A re-run after a 23505, 40001, 40P01 or 55P03 would print twice, or print a value that was rolled back. Fix: Operator commands print only after commit and are never retried. Say so in AD-22.

**N-M6. Carry-overs still open.**
- AD-15 start-up configuration record: only the Api writes it, reading the last `config.recorded` record through an `Audit.Contracts` query (adversarial M5, rubric M13).
- AD-9: the refresh-token row lock (rubric M3) is absent from the AD-4 order. Add it to rank 2, after the Account.

### Low

- **N-L1.** The diagram omits own-contract arrows (`Leave --> LeaveC`, `Org --> OrgC`, `Ident --> IdentC`, `Audit --> AuditC`) and `Admin --> BB`. Under "anything not drawn is forbidden" these are technically forbidden. Add them.
- **N-L2.** AD-21 says `Identity.Contracts` "exposes two interfaces", while the companion also lists `ICurrentAccount`. The AD-11 sweeper calls "`IAccountCommands`", which has no expire-Privileged-Request method. The companion slice is `ExpirePrivilegedRequests`. Name the method on a contract.
- **N-L3.** The exclusion constraint on `daterange(first_working_day, last_working_day, '[]')` matches the PRD FR-15 rule ("share at least one Working Day"; Wed-Fri then Sat-Tue do not overlap). The intersection of two such ranges has working-day endpoints, so the check is exact. FR-14 refuses a range with no Working Day, so the columns are never null. The constraint violation is SQLSTATE 23P01, which AD-22 and AD-7 do not map. Add 23P01 to the 409 mapping (and have the handler's pre-check name the conflicting request).
- **N-L4.** The Deferred item "lock timeouts under load" conflicts with AD-4's fixed 5 s. Reword it to "the 5 s default is an option".
- **N-L5.** AD-8: state the comparison rule for the common-password list (case-folded exact match).
- **N-L6.** AD-13: add "`ALTER DEFAULT PRIVILEGES` per schema for `app_rw`; `migrate` runs modules in a fixed order".
- **N-L7.** AD-18: say the CORS default is deny-all with an options allow list. Name `IRefreshGraceCache` and `IThrottleStore` seams.
- **N-L8.** AD-23 does not state that `leave_balances.employee_id` is the primary key. Add it, since the "one balance per Account" invariant has no owner otherwise. `canonical record bytes` for `verify-audit` are still undefined (owned by Audit, low risk).

## Specific checks requested
- **Circular module dependencies, diagram vs AD-1:** none. The drawn arrows are acyclic and match the AD-1 direction (Administration, Leave, Organisation, Identity, Audit and BuildingBlocks). Only the omitted arrows in N-L1.
- **Companion project table vs spine:** consistent on references and owners. Gaps are N-L2 (interface list, sweeper method) and N-M4 (duplicate slices).
- **Administration sole creator vs Operator bootstrap:** consistent. AD-21 routes bootstrap through the Administration flow. The flow needs an `Actor` parameter instead of `CurrentAccount` for the Operator caller (minor, covered by the AD-12 `Actor` type).
- **Audit write last vs CommitOnFailure:** compatible once N-H2 is fixed. The queue flush also runs on `CommitOnFailure` outcomes.
- **retention_job vs purge needs:** the SELECT, DELETE and INSERT grants now cover the anchor and the summary record. The remaining gaps are the contract, the connection per role and the chain lock (N-M2).
- **Deferred list:** nothing else lets two builders diverge, apart from the items fixed above (N-L4, N-L7). The architecture-test library, the snake_case mapping and the common-password source are harmless.

## Blocking summary
Blocks finalizing: N-H1 and N-H2 only. Fix them with the two AD-4/AD-12 sentences and the rewritten companion Deactivate row, then apply N-M1 to N-M5 as one-line edits. The rest can ship as notes.
