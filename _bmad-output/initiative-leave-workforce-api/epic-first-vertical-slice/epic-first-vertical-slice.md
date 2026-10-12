---
type: epic
title: "A person can be bootstrapped, issued a password and sign in"
parent: initiative-leave-workforce-api
covers: ["CAP-1", "CAP-2", "CAP-12", "CAP-10", "CAP-11"]
assignee: ""
risk: high
---

# A person can be bootstrapped, issued a password and sign in

## Description

The Operator bootstraps the first Administrator and issues Temporary Passwords outside the API, and a person logs in, replaces the temporary password, refreshes and logs out. This is the architecture's increment 1b. It also owns the shared foundations later epics build on: the audit writer, the Leave skeleton with its declared contracts and ports, the auth pipeline with the Administrator policy, and the test support helpers.

## Outcome

The first Administrator can sign in end to end on a freshly migrated database with no password in any log, response or audit record; the signal is the spec's CAP-1 and CAP-2 success.

## Requirements

- FR-2 (CAP-2): Temporary password and first login, with the Must-Change Session.
- FR-7 (CAP-2): Log in, with the generic 401 and the 429 throttling.
- FR-8 (CAP-2): Refresh with rotation and reuse detection.
- FR-9 (CAP-2): Log out ends the session.
- FR-10 (CAP-2): Change own password.
- FR-27 (CAP-1): Bootstrap the first Administrator through an Operator Operation.
- FR-29 (CAP-1): Recover Administrator access through an Operator Operation (the optional reactivate and role-restore are epic 3).
- FR-30 (CAP-1): Issue a Temporary Password through an Operator Operation.
- FR-32 (CAP-12): Operator-controlled Company Time Zone and retention periods.
- FR-25 (CAP-11): Audit writer, closed catalogue and the events this epic produces (completion in epic 8).
- SEC-1 (CAP-2): Password hashing.
- SEC-2 (CAP-2): Password policy and common-password check.
- SEC-3 (CAP-2): Access token lifetime and claims.
- SEC-4 (CAP-2): Refresh token rules.
- SEC-5 (CAP-2): Throttling and back-off.
- SEC-6 (CAP-2): Sessions end on logout, change and reset.
- SEC-7 (CAP-10): Authentication on every endpoint, deny by default (pipeline part; matrix in epic 8).
- SEC-8 (CAP-10): Server-side authorization by role and relationship (pipeline part).
- SEC-13 (CAP-10): Account state re-read on every call (pipeline part).
- SEC-19 (CAP-11): Audit records are insert-only and secret-free.
- SEC-22 (CAP-1): Temporary Password generation, storage and exposure rules.
- SEC-23 (CAP-11): Application role cannot update or delete audit; separate migration role.
- SEC-24 (CAP-2): Session binding of access tokens.

## Done when

1. On an empty database, `migrate` then `bootstrap` creates exactly one Administrator in the Awaiting Password state, prints a Temporary Password once, and a repeat or concurrent run changes nothing and is audited.
2. `issue-temp-password` and `recover-admin` run as specified, including the recipient-verified confirmation, the required reason, and refusal when output is redirected, and `docs/operator-runbook.md` exists.
3. A person logs in with a Temporary Password, changes it in the Must-Change Session, refreshes with rotation and reuse detection, logs out, and every refusal returns the generic 401 or 429; this runs end to end in the closing slice suite.
4. A scan in the closing slice suite shows no password or Temporary Password in any API response, log line, exception or audit row, and app_rw cannot update or delete audit rows.
5. Invalid time zone or retention configuration stops start-up and the effective values are recorded as a Security-Class audit event.
6. The work is merged to the main branch with all tests green.

## Boundaries

The Audit, Leave skeleton and People modules as far as authentication goes, the Operator host, the auth pipeline and the shared test support. Not the HTTP create-account endpoint, manager links, Privileged Requests or leave rules (epics 3 to 7), and not the audit query, purge or chain verification (epic 8). The internal CreateAccount handler exists here because bootstrap needs it; the HTTP endpoint and its rules belong to epic 3.

## References

- parent — initiative-leave-workforce-api.md, section Done when
- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, CAP-1, CAP-2, CAP-10, CAP-11, CAP-12
- prd — _bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md, sections 4.1, 4.2, 4.7 and 5
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md, AD-4, AD-6, AD-8 to AD-15, AD-21, AD-22, AD-24, AD-25
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/solution-structure.md, sections 7 and 8

## Notes

- Decision: one epic for the whole first slice, 17 entries, because one owner works one lane through People, Audit and Operator (2026-10-12).
- Decision: this epic owns the Leave ports and ILeaveCommands declarations and the shared test support, so epics 3 and 4 can run in parallel (2026-10-12).
- Decision: recover-admin's optional reactivate and role-restore move to epic 3 entry 9 because they need deactivation and manager links (2026-10-12).
- Tracer bullet: entries 3, 6, 7 and 8, bootstrap an Administrator and log in.
- Decision: sequencing is audit and Leave skeleton first, then People, the Operator host and bootstrap, then the auth pipeline and login, refresh before change-password and logout, throttling after all three throttled endpoints exist, and the Operator issue and recover commands last because they end sessions and clear throttle state (2026-10-12).
- Unknown: which top-10,000 common-password list, and its licence, to bundle; entry 4 waits on it.
- Assumption: CAP-10 and CAP-11 are shared with epic 8; this epic delivers the auth pipeline and the insert-only audit writer, epic 8 completes the authorization matrix, audit query, purge and verification.
