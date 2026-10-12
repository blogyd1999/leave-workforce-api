# Adversarial review: architecture spine (leave-workforce-api)

Lens: build two units one level down that each obey every AD literally yet are incompatible. Reviewed: spine, solution-structure.md, PRD section 6 and SM-2. No existing file modified.

Verdict: the spine fixes the structure well (ownership, contracts-only, error mapping, auth per call, DB roles) but is under-specified exactly where parallel builders meet: the lock protocol, the cross-module contracts that Administration flows use, the commit policy, and the creation and mutation paths for Account and balance. Not safe to hand to parallel builders until the 3 critical and the high items are closed. Each fix below is one AD tightened or added.

Severity counts: critical 3, high 8, medium 7, low 3.

---

## CRITICAL

### C1. The lock order is incomplete and self-contradictory (AD-4)
AD-4 orders Account -> LeaveBalance -> LeaveRequest, but (a) the tree advisory lock, the Administrator advisory lock, the window and bootstrap locks and the audit chain locks (AD-12) have no position in it; (b) the next sentence says every submit, cancel, approve... "locks that Employee's LeaveBalance row first", which conflicts with "Account first"; (c) `SELECT ... FOR UPDATE` on `identity.accounts` is a cross-schema operation that AD-2 forbids to Leave and Organisation, and no contract exposes it; (d) when one transaction locks several Accounts nothing fixes their order.
- Unit A (Administration.ReassignManager): takes tree advisory lock, cycle check, then `Leave.MovePending` locks the Employee's balance (and the Account via Identity).
- Unit B (Administration.DeactivateAccount): per solution-structure flow, locks Account first, then calls Organisation "count active Direct Reports" (which a builder reasonably guards with the tree lock), then Leave.CancelPendingFor locks balances. Tree-after-Account in B, Account-after-tree in A: deadlock (40P01), retried once, then 409 under load. Also two Administrators deactivating each other: A locks Account 1 then 2; B locks 2 then 1.
- Unit C (Leave.Submit): obeys the "balance first" sentence and never locks the Account, so submit vs deactivation (PRD section 6) is unserialised; Unit D (Leave.Submit by another builder) obeys "Account first" via a bespoke SQL on identity tables or a new contract. Two incompatible lock sets for the same use case.
Fix (tighten AD-4): one total order: (1) named advisory locks in fixed sequence: bootstrap < admin-window < administrator < org-tree; (2) Account rows, multiple ones always ascending by id; (3) LeaveBalance ascending by employee id; (4) LeaveRequest ascending by id; (5) audit chain locks last, ordinary before security. Add `Identity.Contracts.IAccountLocks.LockAccounts(ids)` as the only way to take Account locks, and state that a use case locks the Account of its subject Employee (and any manager involved), not only the balance. A lock helper in BuildingBlocks that takes keys and sorts them is the smallest enforcement.

### C2. Three creators of Account (and its balance row) with no single owner
Identity/Features/CreateAccount and Administration/Features/CreateAccount both exist in solution-structure; the Operator `bootstrap` creates the first Administrator and FR-31 allows "create with Administrator flag". The Total-0 balance row is created by `Leave.InitialiseBalance` only in the Administration flow. Operator references Identity, Audit, Organisation (and Leave in the table, not in the AD-14 graph).
- Unit A (Identity.CreateAccount, used by direct-grant create-with-flag in the window): creates Account and Credential, no balance, no manager link.
- Unit B (Operator bootstrap): creates the Administrator via Identity only. The Administrator has no balance row.
- Unit C (Leave.Submit/View): lazily upserts a missing balance row; Unit D (Leave.View) returns 404 for a missing row. Both comply. Set-opening-balance (FR-11) vs Adjust race then depends on which created the row, and the invariant "one balance per Account" (ER diagram `||--||`) has no constraint owner.
Fix (new AD): `Administration.CreateAccount` is the only use case that creates an Account, including the window path; Identity exposes `IAccountCommands.Create` for it and for Operator bootstrap, and bootstrap calls `Leave.InitialiseBalance` too. Leave never creates a balance lazily; a missing row is an invariant failure. `leave_balances.employee_id` is the primary key. Delete the Identity/Features/CreateAccount slice (keep GrantAdministrator).

### C3. Account state is mutated through multiple paths, and the last-Administrator rule is duplicated
Identity owns Administrator role grant/remove (`Identity/Features/RemoveAdministrator`, GrantAdministrator) and Administration owns deactivate/reactivate, which calls Identity to deactivate. FR-4/section 6: two Administrators deactivating each other, or removing each other's role, must leave one. The "last Administrator" check, the Administrator advisory lock and the count exist nowhere on `Identity.Contracts` (listed: IAccountDirectory, ICurrentAccount, ISessionControl, IAdminWindow). Operator `recover-admin` and `issue-temp-password` also write Identity but a module's types are `internal` and Operator should use contracts (the diagram draws Operator -> Identity, the module).
- Unit A (Identity.RemoveAdministrator) takes the Administrator lock and counts. Unit B (Administration.Deactivate calling `IAccountCommands.Deactivate`) counts without the lock or skips the check because "Identity handles it". Each is compliant; two Administrators removing/deactivating each other both succeed: zero Administrators (SM-2).
- Unit C (Operator recover-admin) writes via `IdentityDbContext` directly (host may reference the module project for wiring) rather than the handler, skipping session ending and temp-password voiding that Api handlers do (AD-8).
Fix (AD-5/AD-1 tightening): every mutation of Account, Credential, Session or AdminWindow state is exposed as one `Identity.Contracts` command and implemented once; the last-Administrator check lives inside the Identity command for `Deactivate`, `RemoveAdministrator`, `Reactivate`, and takes the Administrator lock itself. Hosts may reference module projects for DI registration only and may not use DbContexts or internal types; Operator subcommands call the same contract commands. Architecture test: Api and Operator reference no `*DbContext`.

---

## HIGH

### H1. Audit chain: lock ordering, throughput, sequence, purge
AD-12 computes the hash under a per-chain advisory lock held until commit. (a) Two chains: a transaction writing an Ordinary and a Security record (deactivate, password reset, expiry) in opposite orders in two slices deadlocks; AD-4 order does not cover it. (b) Every audited write in the system serialises on the ordinary chain until commit; a builder who writes the audit record early in the handler (AD-5 says "mutate, then audit" but not last) holds the lock across the Argon2 hash or the Leave locks, collapsing throughput. (c) AD-17: "only `audit.seq` uses a DB sequence": sequences are not transactional, so rolled-back transactions leave gaps; if `verify-audit` expects contiguity (Unit A) it fails after the first rollback; if Unit B tolerates gaps then truncation is undetectable. The seq per chain needs `max+1` under the lock. (d) Purge: `retention_job` has DELETE only, but the purge must read the last hash and INSERT a `ChainAnchor`; it must also take the chain lock or race writers (the oldest prefix is immutable, so no race, but the anchor rewrite is).
Fix (AD-12): audit write is always the last statement before commit; chain locks acquired ordinary then security; `seq` is per chain, assigned as previous seq + 1 under the lock, gaps are a verification failure; the purge runs as `operator_ops` or gets INSERT on `chain_anchors` and DELETE on the record tables, and takes the chain lock only to write the anchor. Say so in AD-13.

### H2. Nobody decides an event's class or how many records a cross-module flow writes
`AuditEvent` carries a class (Ordinary or Security) chosen by the caller. AD-5 says the handler writes the Audit Record; flows in Administration call Identity, Organisation and Leave commands, each of which is also "a handler".
- Unit A (Identity deactivate command) audits `account.deactivated` as Ordinary; Administration.DeactivateAccount audits the same fact as Security: two records, two classes, or one of each module's records missing.
- Unit B (Leave.CancelPendingFor) writes one record per cancelled request (actor = system), Unit C writes none, relying on the orchestrator.
- The actor shape (account id vs operator string "OS user@host" vs "operator configuration") and the details payload (free-form dictionary?) are undefined; AD-12 forbids secrets but the `canonical record bytes` are not defined.
Fix (new line in AD-12 plus Audit.Contracts): a closed catalogue `AuditActions` in Audit.Contracts mapping each action to its class (the caller cannot pass the class), an `Actor` value type (Account id or Operator label), and a rule: a command invoked through a contract writes the records for its own entity changes; the orchestrator writes only the use-case-level record (`account.deactivated`); cancelled requests are audited by Leave with the orchestrator's actor passed in.

### H3. Commit policy when the Result is a failure, and retry mechanics
AD-3: "commit once at the end of the handler"; AD-7: expected failures are `Result`. FR-7/AD-10: a failed login must increment the Credential's back-off and write the aggregate failure record, which means committing on a Failure Result. FR-8 reuse of a spent refresh token must revoke the family and still return 401. Unit A rolls back whenever the Result is a failure; Unit B commits. Also "retry once on 23505/40001/40P01": a retry needs the whole unit of work (connection, tracked entities, audit chain) re-run; if the retry is inside the handler it reuses a poisoned transaction; if it is an endpoint filter, handlers with side effects (issue-temp-password prints once to stdout, FR-30) duplicate them.
Fix (AD-3): the unit of work commits when the handler returns Success or a Failure explicitly marked `CommitOnFailure` (login failure, token reuse); otherwise rolls back. Retry is a single wrapper in BuildingBlocks around the whole unit of work (never in handlers); handlers must be safe to re-run; Operator issue-temp-password is not retried. Audit write failure always throws, which rolls back and returns 500.

### H4. Unit-of-work mechanics are not fixed
"One connection and one transaction ... shared by every module DbContext", "Save once", per-module migrations. Not stated: how contexts join (`UseTransaction` on a shared `DbConnection`), whether each context calls `SaveChanges`, in what order, whether savepoints exist (Unit A uses savepoints around a retry or a "try" step, Unit B does not; a failed statement aborts a PostgreSQL transaction, so a catch-and-continue in B breaks), and who commits: a module handler or the endpoint filter. Migrations: per-module `__EFMigrationsHistory` default table name collides unless `MigrationsHistoryTable(…, schema)` is set; migrate order across modules, and grants for new tables (`app_rw` default privileges) are unowned.
Fix (AD-3 + AD-13): `IUnitOfWork` in BuildingBlocks owns connection, transaction and commit; modules never call `Commit` or `BeginTransaction`; no savepoints (catch-and-continue is forbidden; an error aborts the whole request); every context uses its own schema for its history table; `roles.sql` uses `ALTER DEFAULT PRIVILEGES` per schema; `migrate` runs modules in a fixed order.

### H5. Pending-request move and cancel contract is undefined (`ILeaveCommands`)
Names only: `CancelPendingFor`, `MovePending`. Questions that two builders answer differently: Does a LeaveRequest snapshot its Decider (ER shows `decided_by`) or resolve the Direct Manager live through Organisation? If live, `MovePending` is a no-op and FR-21 approval racing reassignment depends only on lock order; if snapshotted, `MovePending(employeeId, newManagerId)` must update rows. Does CancelPendingFor take a manager id, an employee id, or both (clear-manager cancels the Employee's requests; deactivation of a manager that has reports is refused, so which requests)? Does it release the reservation (Reserved - days) and lock balances in C1 order? What status and reason does a system cancel get (distinct from owner cancel for FR-17 tracking)? Which actor goes into the audit?
Fix (new AD or contract note): state "Decider is resolved live from `IOrgDirectory`, never stored; `MovePending` is removed from the contract" (smallest), or "snapshotted". Specify `CancelPendingFor(employeeId, reason: ManagerCleared | Deactivated, actor)`, which releases reservations, sets status `Cancelled` with `cancelled_by = system`.

### H6. `CurrentAccount` per-call read versus in-transaction state (AD-6 vs AD-4)
The `CurrentAccount` step runs in middleware before the handler's transaction and uses an unlocked read. Handlers then lock after reading. Unit A (Leave.Approve) trusts `ICurrentAccount` (active Administrator, session valid) as the entitlement and does not re-read; deactivation or role removal committed between middleware and the handler's lock is missed (FR-21: entitlement is evaluated on committed state; SEC-24). Unit B re-reads after taking the Account lock. Also Leave must show names (AD-2 forbids joins): `IAccountDirectory` shape (single get versus batch) is unspecified, so history lists become N+1 (Unit A) or one batch call (Unit B), and deactivated names, missing ids and PII rules (AD-19) differ.
Fix (AD-6): `CurrentAccount` is for policy checks only; any handler whose result depends on the caller's active, role or relationship state re-reads it through `IAccountLocks`/`ICurrentAccount.Reload()` after taking locks. Add to Identity.Contracts `IAccountDirectory.GetMany(ids)` returning `{id, displayName, status}` including inactive accounts; single-id lookups are forbidden in list slices.

### H7. Privileged Request expiry: "written the first time observed" contradicts AD-5 and the PRD
AD-11 writes the Expired Security-Class event when a request is first observed expired, including on read; AD-5 says queries never write. PRD FR-31 says expiry is by "a system process", yet no AD defines any background worker (and the single-instance deferral means nothing runs it). Unit A makes the List query write the audit record (and an admin viewing it races another reader on the same chain lock); Unit B adds a hosted service. Expired events missing if no one ever reads.
Fix (AD-11): pick one: a hosted `PrivilegedRequestExpiry` service in Identity (single instance, takes the Administrator lock, writes Expired records in a transaction) and queries compute status on read without writing; remove "written on first observation".

### H8. PRD section 6 / SM-2 invariants that no AD makes enforceable
AD-4 names only unique email, `Total >= 0`, `Reserved <= Total`, status checks. SM-2 and section 6 require, with no AD owner:
- Reserved = sum of Pending days (cross-table within Leave; needs a rule: Reserved only changed by one `LeaveBalance` method, plus a verification SQL in the stress project).
- No overlap among Pending/Approved requests per Employee (needs an exclusion constraint with `btree_gist` on `daterange`, or lock-only; two builders choose differently, and the stress test only works if the DB enforces it).
- Direct Manager is active or absent (cross-schema; only the C1 locking and the Identity-side check protect it; no constraint possible, so name the check in Organisation.Assign and Identity.Reactivate and Deactivate).
- Distinct Initiator and Approver for executed Privileged Requests (`CHECK (initiator_id <> approver_id)` within identity; not stated).
- At least one active Administrator (count, no constraint; see C3).
- No cycle, no self link (needs `CHECK manager_id <> employee_id` and a unique index on `employee_id`).
Fix (AD-4): a list "database-enforced vs lock-enforced" invariant table naming the exact constraints above, and a rule that AD-20's stress project asserts them as SQL.

---

## MEDIUM

### M1. Endpoint ownership of Administration slices
AD-18: "each module exposing one MapXEndpoints". Administration is not a module (no tables) yet has slices with endpoints (create, deactivate, reassign, clear), and `Organisation/Features/AssignManager` + `ClearManager` also exist alongside `Administration/Features/ReassignManager` + `ClearManager`. Unit A maps `POST /accounts/{id}/manager` in Organisation (bypassing the cross-module cancel); Unit B in Administration. Policy (`Administrator`) and the audit actor come from `ICurrentAccount` in both, but only one route may exist.
Fix (AD-1/AD-18): Administration has `MapAdministrationEndpoints`; any use case that touches more than one module lives only in Administration and its single-module twin is removed (delete Organisation AssignManager/ClearManager as endpoints, keep them as contract commands).

### M2. Single-instance in-process state
Refresh-grace cache, throttle ceilings and rate-limiter state are in memory. Nothing prevents two instances (rolling deploy, Azure Container Apps default min replicas, `dotnet test` parallel factories): with two, the repeat refresh lands on another instance, looks like reuse, and revokes the family (users logged out); throttling ceilings multiply by N. A process restart inside the 10-second window has the same effect.
Fix (AD-9/AD-10): one line: "the Api deploys with max replicas 1 and no overlapping rolling revision; a replica-count guard test/document; cache loss is treated as a spent token and is acceptable". Smallest change, no new infrastructure.

### M3. Account status and password-state contract shape
"Account active", Administrator role, Awaiting Password, Must-Change Session each appear as separate columns in three places (Account, Credential, Session) and surface in `ICurrentAccount`, `IAccountDirectory`, `IOrgDirectory`. Unit A defines `bool IsActive`, Unit B `enum AccountStatus {Active, Deactivated}`; AD-17 stores enums as text, so a bool is a divergence. Organisation checks "manager is active" through one, Leave through the other.
Fix (AD-17/AD-2): one `AccountStatus` enum and `PasswordState` enum in Identity.Contracts, referenced by all other contracts; no module defines its own.

### M4. Who owns "Awaiting Password" transitions
AD-8 puts the state on Credential; transitions are triggered by Administration create (FR-1), Identity ResetPassword (FR-28), Operator issue-temp-password (FR-30), Login (consumes), ChangePassword, Deactivate/Reactivate (void; reactivation returns to what?). Unit A reactivates to `AwaitingPassword` with a fresh temp password; Unit B restores the previous state.
Fix (AD-8): a single state-transition table (event -> from -> to) inside the Credential aggregate; the only mutators are methods on it; reactivation yields AwaitingPassword with no live Temporary Password (Operator issues).

### M5. Start-up configuration audit has no owner
AD-15 writes the effective config and "change from the last recorded values" as a Security record at start-up, but "last recorded values" is stored nowhere (AD-1 requires an owner for every entity) and both Api and Operator start. Unit A reads the last Audit record's JSON details; Unit B adds a table in Identity; both Api and Operator write on start, producing duplicates and racing on the security chain lock.
Fix (AD-15): only the Api writes it at start-up, reading the last `config.recorded` record from Audit through an Audit.Contracts query; no new table.

### M6. Operator role grants versus operations
`operator_ops` is described for "bootstrap, issuance, recovery, verification" but bootstrap writes accounts, credentials, balance (C2), manager-less; recovery ends sessions; verification reads audit; `purge-audit` uses `retention_job`, but purge must insert anchors (H1); `migrate` is `migrator`. Unit A grants `operator_ops` DML on identity and leave only; bootstrap with Leave.InitialiseBalance (per C2 fix) fails at runtime.
Fix (AD-13): add a grant matrix per role and schema to AD-13.

### M7. Rate limiter versus FR-7 enumeration and CurrentAccount ordering
Pipeline order is throttle, JWT, CurrentAccount, policy. The ASP.NET rate limiter runs before authentication, so account-based limits (per email) cannot be in it; AD-10 correctly puts them in Credential. Solid, but "unknown-email ceilings use a bounded store" and "same 429 for known and unknown" requires the Login handler, not middleware, to throw 429 after the dummy hash; not stated.
Fix (AD-10): "per-account and unknown-email refusals are produced by the Login handler after the equal-cost dummy path".

---

## LOW

### L1. Operator references in solution-structure disagree with AD-14 graph
Operator references Identity, Audit, Organisation, Leave in the table, but the AD diagram has no Leave. Align with C2/C3 (Operator through Contracts only).

### L2. `canonical record bytes` and details payload serialisation
Unspecified; owned entirely by Audit so low cross-builder risk, but a catalogue (H2) should include the canonical JSON rule (sorted keys, UTC ISO 8601, UTF-8) to keep `verify-audit` reproducible across versions.

### L3. Deferred items that can diverge
Pool sizes (a transaction holds a connection while waiting on locks; pool exhaustion under stress looks like a deadlock) and stress iteration counts are deferred; add "pool size >= parallelism + 4" to AD-20. NetArchTest vs ArchUnit is harmless. The React/CORS deferral is harmless.

---

## Where the spine is solid
- AD-1/AD-2 ownership table and contracts-only references: clear, testable; no duplicate entity owners except the creation-path issue in C2.
- AD-6 per-call reading of account state and no role claims: sound; only the in-transaction re-read (H6) is missing.
- AD-7 error ordering and race-loser rule: consistent with PRD SEC-11; leaves little room.
- AD-9 refresh rotation and hashing: sound, single-instance trade-off honestly stated.
- AD-12/AD-13 audit insert-only and role split: right design; gaps are only the lock order, seq and anchor privileges (H1).
- AD-16/AD-17 time and ids: precise.
