# Rubric review: architecture spine vs good-spine checklist

Reviewed: architecture-leave-workforce-api.md (draft) and solution-structure.md against prd-leave-workforce-api.md (final).
Verdict: NOT READY. One critical structural defect, seven high findings, and the operational/environmental envelope is largely silent. Fixes are small; none needs a redesign.
Counts: critical 1, high 7, medium 13, low 9 (30 total).

## Critical

**C1. AD-1 / companion sections 1-2: required behaviour needs Identity to depend on Organisation, which AD-1 forbids.**
FR-28 forbids an Administrator resetting their own Direct Manager, so `Identity/Features/ResetPassword` must read the Organisation link. FR-6 lists Accounts "including manager", which Identity cannot read either. AD-1 says a lower module never references a higher one (Identity is below Organisation). Related defects: `CreateAccount` is a slice in both `Identity` and `Administration`; `Identity.Contracts` lists only `IAccountDirectory`, `ICurrentAccount`, `ISessionControl` and `IAdminWindow`. It has no command interface for create, deactivate, reactivate, credential reset or role grant, so Administration cannot reach Identity for FR-1 and FR-4.
Fix: move `ResetPassword` and the manager-joined `ListAccounts` into Administration. Keep a single `CreateAccount` in Administration. Add `IAccountCommands` to `Identity.Contracts` (create, deactivate, reactivate, reset credential and void Temporary Password, grant and remove role). Add to AD-1: "any use case needing facts from two modules lives in Administration".

## High

**H1. AD-12 vs AD-13: the retention purge cannot run as designed.**
`retention_job` has DELETE only on `audit`. A purge must SELECT the last purged hash, INSERT a `ChainAnchor` row and INSERT the Security-Class purge summary (FR-25). `purge-audit` is also an Operator subcommand, but only `retention_job` is named for it.
Fix: grant `retention_job` SELECT, DELETE, and INSERT on `chain_anchors` plus the Security chain only. State that `purge-audit` runs as `retention_job`. State who computes the anchor, and make purge and anchor one transaction.

**H2. AD-3 / AD-5: no rule for committing state when a handler returns a failure Result.**
AD-3 says "commit once at the end". Several required effects must persist while the call returns an error:
- refresh-token reuse revokes the family and returns 401 (FR-8)
- failed-login back-off and lockout counters (SEC-5)
- refused bootstrap, recovery and issuance attempts must be audited (FR-25, FR-27, FR-29)
- the Expired event (AD-11)
Fix: AD-3 should say that a Result may mark `CommitOnFailure`, or that these effects run in a second short transaction. Name each case that does so.

**H3. SEC-2 common-password list is undecided.**
PRD section 11 explicitly delegates the screening mechanism to architecture. AD-8 checks length only, so every implementer would choose their own list.
Fix: add to AD-8 a named offline list (for example the top-N list shipped as an embedded resource, loaded at start-up) and the comparison rule (case-folded exact match).

**H4. The operational and environmental envelope is mostly silent.**
Only a three-box environments diagram exists. Nothing decides or defers the following:
- the environment set and per-environment configuration (which config source for Api and Operator, the same one)
- TLS termination and HTTPS enforcement (SEC-15)
- provisioning order (create database, `roles.sql`, `migrate`, `bootstrap`) and who holds superuser
- how the Operator reaches production
- scheduling of `purge-audit` and `verify-audit` (the retention process has no trigger)
- backup, restore and migration-rollback policy (the audit chain makes restore ordering matter)
- JWT key-rotation procedure
- the FR-29 written recovery procedure (a first-increment deliverable with no owner)
- health, readiness and graceful shutdown
Fix: add one AD "Environments and operations" covering local, test and a deployed environment. Move the remainder into Deferred explicitly, each with a named owner.

**H5. AD-5 conflicts with audited reads; AD-11 writes on read.**
AD-5 says queries "never write". FR-13, FR-24 and FR-26 require an Audit Record for each Administrator read (balance, requests, audit query). AD-11 writes the Expired event "when first observed", which can occur on a GET list.
Fix: AD-5 should say audited reads are handlers (lock, audit insert, commit). They use the same `IAuditWriter` path and the same transaction. Mark them in the slice naming.

**H6. Dependency diagram, AD-1 and the companion disagree.**
The diagram says "anything not drawn is forbidden". The following are not drawn but are needed:
- Operator to Leave (companion lists it): `migrate` needs `LeaveDbContext`, and `bootstrap` must initialise the Total-0 balance (FR-11). The ERD says every Account has a LeaveBalance.
- `Organisation.Contracts` to `Identity.Contracts`
- every Contracts project to BuildingBlocks
- Administration to BuildingBlocks
The Operator use cases (bootstrap, issue, recover) have no slice home: Identity's `Features` list has none, and `Operator` is a console project while handlers are `internal`.
Fix: redraw the diagram from the companion table. Put the Operator use cases as slices under `Identity/Features` (and `Administration` for bootstrap, since it creates Account, link and balance) exposed via Contracts. Also route bootstrap through the same Account-creation path as FR-1 so there is not a second creation code path.

**H7. Two sources of truth for the assigned manager.**
`Leave.Contracts.MovePending` implies each LeaveRequest stores a manager id. FR-21 and FR-23 decide by the current `ManagerLink`. AD-1 and the ERD do not say which is authoritative.
Fix: add an AD line: "authority is always the current ManagerLink via `IOrgDirectory`. The request stores `decided_by` only." Drop `MovePending`, or state it is a no-op on reassignment.

## Medium

**M1. AD-4 lock order is incomplete.**
It names only Account, LeaveBalance and LeaveRequest. Not covered:
- advisory keys (tree, Administrator count, Single-Administrator Window, bootstrap, two audit chains)
- `ManagerLink`
- the order between two Accounts (two Administrators deactivating each other)
- the inverse order: the FR-31 approval takes the Administrator lock then Accounts, while deactivation takes Account then the Administrator-count lock. That is a deadlock pair, only mitigated by the retry.
- how Leave and Organisation lock an Account that lives in another schema (submit vs deactivation; assign manager vs deactivating that manager). No `IAccountLock` contract is named.
Fix: one total order (advisory keys by fixed rank, then Accounts by ascending id, Balance, Request, audit chain lock last) and a contract method to lock Accounts.

**M2. Isolation level is not stated.**
AD-4 retries SQLSTATE 40001, which occurs only under repeatable read or serializable. Fix: state READ COMMITTED with explicit locks (and 40001 is therefore rare), or state otherwise.

**M3. AD-9 grace-window race.**
A repeat of a token arriving before the first rotation commits is not yet in the cache and would be treated as reuse. That wrongly revokes the family.
Fix: lock the refresh-token row. The second caller waits, then is served the successor from cache or database.

**M4. AD-14 versus the Azure "Operator job" box.**
A job's stdout lands in platform logs. That breaks SEC-22/FR-30 ("channel not logged"), and the interactive confirmation cannot run in a job.
Fix: require an attached TTY and refuse otherwise. State that Azure use is an interactive exec, not a job.

**M5. SEC-15 and SEC-17 have no governing AD.**
HTTPS-only (HSTS, redirect, forwarded-proto trust), OWASP security headers and the CORS policy are absent. CORS origins are put under Deferred "React front end".
Fix: add them to AD-18. Default CORS to deny-all with an options-driven allow list, so the deferral is safe.

**M6. ERD, AD-1 and companion section 5 disagree.**
- The ERD has one AUDIT_RECORD but section 5 has two tables.
- The ERD omits AdminWindow.
- The ERD omits the Privileged Request Initiator and Approver relationships.
- AD-1 lists ThrottleState as an entity, but no table exists (it lives on `credentials`).
- AD-17 gives `audit.seq` a database sequence, whose gaps (rolled-back inserts) break contiguous-seq verification of AD-12.
Fix: align the ERD with the schema list, say "ThrottleState = columns on Credential", and compute `seq` as max+1 under the chain lock (or state that verification tolerates gaps).

**M7. SEC-12 self-dealing and restricted-target rules have no owner.**
These are repeated in FR-1, FR-3, FR-4, FR-5, FR-11, FR-12 and FR-28, and SM-3 tests every case. AD-6 binds SEC-12 but says nothing of them.
Fix: add an AD-6 or AD-5 line placing these checks in the orchestrating handler, evaluated at the "entitlement" step (403) of AD-7.

**M8. AD-7: malformed or typed-wrong JSON.**
Framework body-binding failures produce a 400 before entitlement, which leaks resource-existence information (SEC-11 rule 6 comes after 404). Fix: bind to string or `JsonElement` fields, or catch binding errors and defer them to the handler's validation step.

**M9. AD-20 versus Deferred.**
AD-20 requires architecture tests "in CI", but "CI and release pipeline" is deferred. Fix: define a minimal gate (one build-and-test command, run locally and in any CI).

**M10. AD-12 failed-login aggregation is underspecified.**
It does not say who aggregates, over what window, or where the state lives (in-memory is lost on restart). It also does not state the FR-25 rules: no raw unknown-email identifier, 256-character field cap, bounded count per source and window.
Fix: state the mechanism and these constraints.

**M11. Increment order (companion section 6).**
Increment 2 (create Account) and increment 1 (bootstrap) need `Leave.InitialiseBalance` and the `leave_balances` table, but Leave arrives in increment 3. The FR-29 written procedure deliverable is also missing from increment 1.
Fix: pull the Leave balance table and contract into increment 1. Add the procedure to increment 1.

**M12. AD-11 expiry.**
FR-31 says "expires by a system process". The lazy design is acceptable, but the Expired event is lost if the observing transaction rolls back (an approval returning 409). Concurrent observers can duplicate it.
Fix: write it under the request row lock and commit on failure (see H2). Name the actor "system process".

**M13. AD-15 configuration.**
It does not say where the "last recorded values" are read from (presumably the Security chain through `Audit.Contracts`). It does not say that Api and Operator read the same configuration source, which matters because the purge uses the retention values.
Fix: add one sentence each.

## Low

- **L1. AD-12:** tail truncation or whole-chain rewrite by a database superuser is undetectable without an external anchor (deferred to Azure). Record it as a known limitation.
- **L2. PRD binding gaps:** SEC-9, SEC-20 and the PRD section 6 Performance target (p95 under 500 ms at 200 Employees) are bound by no AD. SEC-9 is satisfied via AD-6. Add them to Binds.
- **L3. Scale-out seam:** name the interfaces behind the in-process refresh cache and throttle store (for example `IRefreshGraceCache`, `IThrottleStore`) so the deferral cannot cause divergent edits.
- **L4. AD-3 and the PRD section 6 splittability goal:** a shared cross-module transaction makes a later split non-free. State this as an accepted trade-off.
- **L5. AD-17:** the normalisation function for the email unique column is undefined (trim plus invariant lower-case; Unicode form).
- **L6. AD-19:** "no names or emails in logs" is not mechanically enforceable. Add the SM-7 captured-log scan to AD-20.
- **L7. Spine tree:** it has no `docs/` or runbook location. AD-2 says "every module is two projects", yet Administration is one. Clarify that Administration is not a module.
- **L8. AD-10:** the ASP.NET rate limiter counts requests, whereas SEC-5 specifies failed attempts. Say that the per-source limiter counts failures only (custom partition on failure).
- **L9. Deferred "managed identity for database":** this conflicts with AD-13 connection strings from secrets. Note it as a future AD amendment.

## Checks requested

**FR/SEC coverage.**
All of FR-1 to FR-32 have an owner in the capability map. Gaps: the Direct Report list (FR-6 second half, Organisation) is not named in the map, and the audited reads of FR-13, FR-24 and FR-26 are not governed (H5).
SEC items with no or partial governing AD:
- SEC-2 (common-password list): H3
- SEC-9: not bound (L2)
- SEC-15 (HTTPS): M5
- SEC-17 (CORS, headers): M5
- SEC-12 (self-dealing): bound to AD-6 but its rule does not say so (M7)
- SEC-20: not bound (L2)

**Deferred items.**
Nothing can make two units diverge except these:
- CORS origins, deferred though SEC-17 needs a default now (M5)
- CI (M9)
- scale-out seams (L3)
- the omitted dimensions in H4

**Dependency diagram.**
There is no cycle among the drawn arrows. Leave reaches Identity and Organisation only via Contracts. Administration reaches Leave, Organisation, Identity and Audit contracts, but Identity lacks command contracts (C1). Operator needs Leave (H6). Operator also needs Organisation for FR-29 reactivation (the diagram has it).

**PRD binding requirements.**
- Satisfiable as written: sections 5/6 correctness-under-concurrency (with M1/M2), FR-27 (with H2), FR-29, FR-30 (with M4), SEC-11, SEC-13, SEC-22, SEC-24.
- FR-25: needs H5 and M10.
- FR-32: needs M13.
- FR-31/SEC-23: need M12 and H1.

**Tree.**
The spine tree and the companion agree on projects and layout. Two differences: the spine tree places Contracts under `src/Modules`, and the companion lists no test projects. Neither affects behaviour. The reference sets differ (H6).
