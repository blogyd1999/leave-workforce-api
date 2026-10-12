# Adversarial Review R3: Leave and Workforce Management API PRD

Scope: the PRD only, drafted before reading r2. Prior-theme check is in the last section.
Counts: Critical 1, High 3, Medium 9, Low 5.

Verdict: the two-person flow closes the direct routes (create, grant, reset). It leaves one unilateral route to a puppet Administrator (reactivation), no route to a second Administrator at all, and an unverified Operator hand-off that defeats the "no leave decision right" rule. Fix C1 and H1 to H3 before architecture.

## Critical

### C1. Deactivate-then-reactivate (or reactivating a dormant Administrator) is a single-Administrator password reset and identity takeover that bypasses FR-31
- FR-4: "Reactivation restores login only; the Account returns to the Awaiting Password state, because its password is invalidated by deactivation." FR-4 is single-Administrator. FR-31 covers only "creating an Account with the Administrator role, granting the Administrator role, and resetting an Administrator's password". FR-28 blocks Administrator targets, but FR-4 has no such block. Nothing says deactivation strips the role.
- Attack, with Administrators A and B active: A deactivates B (the last-active-Administrator check passes because A remains), then reactivates B. B is now Awaiting Password with sessions revoked, which is exactly an Administrator password reset. A asks the Operator for B's Temporary Password (see H1). A now controls two Administrator identities, initiates a Privileged Request as A and approves it as B, and FR-31 is void. The same works with any dormant deactivated Administrator Account, so no live victim is needed.
- Fix: make deactivation and reactivation of any Account holding the Administrator role a Privileged Administrator Action. Or strip the role on deactivation so that regranting needs FR-31. State that the FR-4 reactivation path never restores the Administrator role unless it went through FR-31. Add SM-6 test cases for deactivate and reactivate of an Administrator.

## High

### H1. FR-30 has no requester verification, so the Operator hand-off is the bypass for Manager takeover and for C1
- FR-30: the Operator "can issue a Temporary Password for an Account that is Awaiting Password". The written verification procedure is required only for FR-29 ("how to verify the requester"). UJ-7 has Karan pass the password to "Dev" with no check.
- An Administrator can reset any ordinary Account (FR-28 forbids only self, Administrators and their own Direct Manager). Reset a Manager M, claim to be M, and receive M's Temporary Password. A now logs in as M and decides leave for M's reports. That negates "The Administrator role confers no right to decide Leave Requests" (AP-4, SEC-12), and A can do it alone.
- The target is never told. There are no notifications (L-3), so M finds out only when M's login fails.
- Fix:
  - FR-30 must have a written, mandatory out-of-band verification of the holder's identity, independent of the requesting Administrator.
  - The Operator's issuance must name the audit record of the reset or reactivation that caused it, and refuse to issue without one.
  - Add an alert to the target's Direct Manager or to other Administrators when a Manager's password is reset.
  - Consider a restricted-target rule for resetting any Account that decides others' leave, or require that the requesting Administrator is not the delivery contact.

### H2. There is no way to ever create a second Administrator, so UJ-8 and FR-31 are undeliverable as written
- FR-27: succeeds "only when no Administrator Account exists". FR-31: a grant needs "a different Administrator" to approve. FR-31 also says "When no second eligible Administrator exists, the action cannot proceed through the API; the only sanctioned alternative is the Operator's recovery operation (FR-29)". FR-29 only acts on an existing named Administrator.
- After bootstrap there is exactly one Administrator, and no sanctioned path leads to two. SM-6 and UJ-8 cannot be run end to end without a test-only back door. The deadlock will push implementers toward a "seed second admin" shortcut.
- Fix: add an explicit Operator Operation (or a bootstrap parameter) that creates a named second Administrator with stated reason. The Operation must be audited, Security-Class and non-concurrent, and it must be its own sanctioned exception to FR-31. Alternatively make bootstrap create two Administrators.

### H3. One Administrator can strip, lock out or silently replace every other Administrator, so US-19's "no single Administrator can ... take over another" is false
- FR-5: "removal is a single-Administrator action". FR-4: deactivation is single-Administrator. FR-28: ordinary reset has no protection for a just-demoted Administrator.
- With A and B active, A can do three things:
  - Remove B's role.
  - Reset B as an ordinary Account (it is no longer an Administrator, so FR-28 applies), which logs B out and makes B depend on the Operator.
  - Deactivate B.
  A becomes the permanent sole Administrator. FR-31 can then never run (H2), and A holds unchecked power over every non-FR-31 action.
- Fix: require two-person approval, or at least a notification and a delay, for removing or deactivating another Administrator. Add "removal of Administrator role" to the Privileged list. Document the residual case where only one Administrator remains.

## Medium

### M1. Throttle-state eviction and global ceiling are exploitable, and targeted lockout hits first-login hardest
- SEC-5: "Throttle state has a maximum size and a time to live", a per-source ceiling and "a global ceiling", and unknown emails get the same 429. Behaviour at the size cap (evict, fail open, fail closed) is not stated.
  - If it evicts, spraying many unknown emails flushes a victim's back-off and enables guessing.
  - If it fails closed, the same spray locks out real users.
  - A source that hits the global ceiling can deny login to everyone, including Administrators. FR-28 and FR-30 clear only per-account state.
- Targeted lockout and Temporary Password interplay: back-off refuses even the correct password (SEC-5, "the account is refused"). An attacker needs only about one attempt per 15 minutes to keep a new joiner from using a 24-hour Temporary Password until it expires (FR-2). L-9 accepts account lockout generally but does not mention this expiry interaction.
- Also unspecified: whether attempts made during a refusal extend the back-off, whether Awaiting Password and deactivated Accounts take the dummy-hash path (SEC-5 says "dummy password verification for unknown ones", so these two may be timing-distinguishable from unknown), and whether refresh (opaque token) is throttled per source only.
- Fix: state the cap behaviour (fail closed per source, never evict an active back-off entry), key back-off by (account, source) with a higher account-wide ceiling, aggregate IPv6 by /64, make attempts during refusal not extend the penalty, and require the dummy-verification path for every non-usable state. Add a prerequisite for Operator re-issue after a locked first login.

### M2. State after Temporary Password expiry or consumption is undefined, which can strand an Account
- FR-2: the Temporary Password "works for one login only and expires 24 hours after issue". "The session ends no later than the Temporary Password's original expiry." FR-30 issues only for an Account "that is Awaiting Password".
- If the user logs in (Temporary Password consumed) and the session dies before the password is changed (crash, expiry), or the Temporary Password expires unused, no state definition returns the Account to Awaiting Password. FR-30 then cannot reissue until an Administrator does a reset.
- Also: a reset (FR-28), deactivation, or reactivation does not say that an outstanding unused Temporary Password is voided. An unexpired one issued before deactivation could work after reactivation within its window.
- Fix: define the state machine: Awaiting Password -> (issue) -> Temporary Issued -> (login) -> Must-Change -> (change) -> Active. Expiry or session death in Must-Change returns to Awaiting Password. Every entry to Awaiting Password voids outstanding Temporary Passwords.

### M3. SEC-13 does not re-check "password state", and login and refresh races can mint sessions after a reset
- SEC-13 re-reads "active state, Administrator role, Must-Change State and session validity". It does not re-read Awaiting Password or a credential version.
- §6 lists no race between login, refresh, or Temporary Password consumption and reset, deactivation or recovery. A login that verified the old password just before a reset commits can create a session after "end all its sessions" ran. Two concurrent logins with the same Temporary Password can both pass the "one login only" check.
- Fix: add a per-account security stamp (credential version) checked on every call and on session creation, and make Temporary Password consumption a compare-and-set. Add those races to §6.

### M4. FR-31 is under-specified in ways an implementer or attacker can use
- No Initiator withdrawal ("its Initiator cannot approve or reject it"). A mistaken request lingers 24 hours and can be approved by any other Administrator.
- No limit on duplicate or repeated Privileged Requests, so an Initiator can resubmit after each rejection until someone approves.
- Request content is not stated to be immutable and bound to the approval. The approver's view of the exact payload is not a requirement; no listing or read endpoint is defined.
- For "create an Account with the Administrator role", the "target" does not exist yet, so "target can be neither Initiator nor Approver" has no meaning, and FR-1 has no role parameter.
- "Executed" for an Administrator reset has no definition, because FR-28 excludes Administrators. It is not stated that execution performs all of FR-28's effects (sessions, tokens, throttle, Awaiting Password).
- "Different Administrator" is by Account. "One person has one Account" is not enforceable; only email uniqueness is, so one person with two mailboxes can fill both roles.
- There is no step-up (password re-entry) for approval, so a stolen 15-minute Access Token of an Administrator can initiate or approve.
- Fix: add withdrawal by the Initiator, one open request per (action, target), immutable payload hashed into the approval, an approver view with full details, and a stated execution recipe. Require password re-entry for approve and reject.

### M5. §6 concurrency list omits races that the new mechanisms create
- Missing: Privileged Request approval versus demotion or deactivation of the Approver, Initiator or target; approval versus expiry at the 24-hour boundary (and the job that writes the "expired" event); two open requests for one target; submit versus manager reassignment (a request committed after a reassignment but assigned to the old manager is never moved, so nobody can decide it); an in-flight Administrator action versus the acting Administrator's own demotion or deactivation.
- FR-31 says approval "must be an active Administrator", but SEC-13's lookup is a separate read from the write transaction. The actor's role must be re-validated inside the transaction (§6 lists "last-Administrator count", not the actor's own role).
- Fix: add all these to §6 and SM-2, and state that every Administrator action revalidates the actor's role and active state in its own transaction.

### M6. "Active Administrator" is undefined for the last-Administrator rule and for approver eligibility
- FR-5: "The last active Administrator cannot be deactivated or lose the role." FR-31: "no second eligible Administrator." An Administrator in Awaiting Password or Must-Change State is not callable (SEC-11 rule 3, FR-2) but may count as "active".
- With A and C functional and B not, A and C can remove each other concurrently and both succeed because B counts, leaving no usable Administrator until FR-29.
- Fix: define "eligible Administrator" as active, with a usable password, and not in Must-Change, and use that definition in both rules.

### M7. Audit retention can be shortened in effect, and the config-change record is itself purgeable
- FR-32 floors: ordinary at least 30 days, and Security-Class "not longer than the Ordinary Retention Period" is rejected. So Security retention can be 31 days. SEC-23 trusts the Operator, but nothing stops a silent retroactive shrink from three years to 31 days: the next purge deletes the three years of Security-Class events, including the "changed from last recorded values" record.
- The start-up comparison depends on the "last recorded values", which are themselves purgeable. The purge runs as a separate process (SEC-23), but FR-32 records configuration at app start-up only. The purge process's own configuration can differ and is not recorded.
- Classification: the Administrator reads of others' requests and balances and each audit query are not Security-Class by the definition, though they are the evidence of Administrator snooping, and will expire after one year.
- Fix: a Security floor of at least one year; the purge process records its own effective configuration in each summary; a shrink must be acknowledged in config and takes effect only after a grace period; the purge role may delete only rows older than a stored cutoff; classify audited reads as Security-Class; keep the last-recorded-values record out of purge or re-emit it at each purge.

### M8. FR-3 and FR-4 give an Administrator a back door to cancel or re-route leave
- FR-3: "Clearing ... cancels the Employee's Pending requests", and reassignment "moves" them. FR-24 says an Administrator has no operation to cancel; clearing is exactly that, for any Employee, with only an audit record. Reassignment to a chosen Manager is a way to steer decisions toward a Manager the Administrator controls (see H1). This is the Q-E4 design flaw.
- Fix: for Q-E4, refuse clearing while Pending requests exist, or allow cancel only with employee notification and an additional reason code. Require a second Administrator for reassigning an Employee who has a Pending request, or at least raise the audit class and surface it to the Employee.

### M9. Manager definition conflicts with history visibility, and the Must-Change allowed-call list conflicts with refresh
- Glossary: Manager is "Direct Manager of at least one active Employee". FR-23 and FR-4 say deactivated Direct Reports still appear in the Manager's history. A Manager whose reports are all deactivated stops being a Manager and loses that view. FR-6's Direct Report list "shows each active and deactivated Direct Report" has the same issue.
- FR-2 says a Must-Change Account "can call only change-password and logout", but also that expiry "is re-checked on every call in the Must-Change State, including a token refresh", implying refresh is allowed.
- Fix: base Manager capability on any Direct Report (active or not) for read endpoints, or state it explicitly; list refresh in the allowed calls.

## Low

- L1. FR-14: a Saturday or Sunday start date in the past is allowed when the First Working Day is Today, which sits oddly with §7 "No backdated requests". Say so or require start date not before Today.
- L2. SEC-6 and SEC-24 say password change ends sessions; FR-10 keeps the current session. Align the wording.
- L3. Operator identity is "architecture decides" (FR-29, FR-30). A shared host account would make the Operator audit actor unattributable. Require a named individual identity in the record.
- L4. FR-25: audit failure rolls back business changes, but the PRD does not say whether a failed audit write for login success, lockout, or Operator Operation blocks them. State fail-closed for all Security-Class events.
- L5. Q-E3: a 24-hour expiry means an Initiator on Friday afternoon cannot get approval over a weekend; consider a longer value. Q-E2 also leaves transitive managers (the manager's manager) resettable; confirm intent.

## Where the PRD is solid
- Response-code policy (SEC-11) is consistent with FR-16, FR-17, FR-19 to FR-21: the 404-wins rule and the "evaluate on committed state" race rule give clean outcomes. The authorization ordering with 400 after 403 and 404 is well specified.
- Self-dealing guards (own balance, own manager, own roles, own deactivation, own Direct Manager reset) hold, and the transitive chain case blocks A from deactivating their own manager while reports exist (FR-4).
- The Operator reach is constrained by design: FR-30 issues only to an Awaiting Password Account, so the Operator cannot take over an active Account without an Administrator-triggered reset.
- Temporary Password handling (128 bits, hashed, not logged, one use, 24 hours, Must-Change with session cap at original expiry) is sound once M2 and M3 are fixed.
- Refresh token rotation and reuse detection, per-call lookup (L-2 acknowledged cost), the audit database-role split, and explicit L-9 collusion acceptance are all clear and honest.
- First Working Day rule (FR-14, FR-19, L-10) is coherent. The only issues are the Saturday start (L1) and the stale Pending reserve, which is acknowledged.

## Status of the earlier critical/high themes (checked against r2 after drafting)
1. Single-Administrator puppet self-dealing bypass: partly closed. Creation and grant now need two people, but C1 reopens it via reactivation, and H2 and H3 are new related holes.
2. Administrator account takeover via reset or creation: partly closed. FR-28 excludes Administrators, but C1 (reactivation) and H1 (unverified Operator hand-off, also for Managers) remain.
3. Root-of-chart cannot be deactivated: closed. FR-4 plus FR-3 clearing gives a path (clear each Direct Report, then deactivate). Residual cost: reports become Top-Most Persons and lose leave (L-1).
4. Throttle source and Temporary Password entropy: mostly closed. The source model and 128-bit passwords are specified. Residual: M1 (eviction, global ceiling, first-login lockout).
5. Audit guarantee weaknesses: mostly closed. Database roles, same-transaction audit, retention classes and purge summaries exist. Residual: M7 (shrinkable retention, purgeable config-change record, unclassified read audits) and tamper evidence still deferred to architecture.
