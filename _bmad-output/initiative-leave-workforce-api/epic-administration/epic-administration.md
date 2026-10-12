---
type: epic
title: "Administrators run accounts, managers and Administrator roles"
parent: initiative-leave-workforce-api
covers: ["CAP-3", "CAP-4", "CAP-5", "CAP-1"]
assignee: ""
risk: high
---

# Administrators run accounts, managers and Administrator roles

## Description

Administrators create, list, deactivate and reactivate Accounts, manage reporting lines, reset ordinary passwords, and add Administrators under the Single-Administrator Window and Privileged Request rules. This is the architecture's increment 2. The Leave side effects of manager changes and deactivation are recording no-ops here and become real in epic 5 entry 6.

## Outcome

An Administrator can run the organisation through the API with no way to orphan an Employee or lose the last Administrator; the CAP-3, CAP-4 and CAP-5 success criteria are met once epic 5 entry 6 completes the request side effects.

## Requirements

- FR-1 (CAP-3): Create an Account (HTTP endpoint and rules).
- FR-3 (CAP-5): Assign, reassign or clear a Direct Manager.
- FR-4 (CAP-3): Deactivate and reactivate an Account.
- FR-5 (CAP-4): Manage the Administrator role.
- FR-6 (CAP-3): View Accounts and Direct Reports.
- FR-28 (CAP-3): Reset an ordinary Account's password.
- FR-29 (CAP-1): Completion only: optional reactivate and role-restore in recover-admin (owned by epic 2).
- FR-31 (CAP-4): Creating or granting an Administrator.
- SEC-10 (CAP-5): A Manager sees only their own Direct Reports.
- SEC-12 (CAP-4): Administrator separation of duties.

## Done when

1. An Administrator creates and lists Accounts, and looks one up; duplicate emails, self-manager, over-long fields and unauthorised callers are refused with the codes of SEC-11, and a new Account has a zero balance and no returned credential.
2. Manager assignment, reassignment and clearing work with cycle refusal, tree locking and audit, a Manager sees only their own Direct Reports, and clearing and reassignment call the Leave side-effect contracts.
3. Deactivate and reactivate follow FR-4 including the active-Direct-Report refusal, the last-Administrator rule and the return to Awaiting Password, and ordinary password reset refuses the restricted targets with 403.
4. The Single-Administrator Window, one direct grant, role removal, Privileged Requests with distinct Initiator and Approver, expiry and the sweeper behave as FR-31 states, and recover-admin can reactivate or restore a role.
5. A concurrency test over deactivation, reassignment, mutual role removal and concurrent approvals ends with the SM-2 invariants intact.
6. The work is merged to the main branch with all tests green.

## Boundaries

The People module's administrative use cases and endpoints. Not login or the Operator host (epic 2), and not the real implementation of the Pending-request move and cancel in Leave (epic 5, entry 6); until then the calls are recording no-ops.

## References

- parent — initiative-leave-workforce-api.md, section Done when
- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, CAP-3, CAP-4, CAP-5
- prd — _bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md, sections 4.1 and 4.2
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md, AD-3, AD-4, AD-11, AD-21, AD-23, AD-24
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/solution-structure.md, section 4

## Notes

- Decision: Leave.MovePending and Leave.CancelPendingFor are declared in epic 2 entry 2, called here as recording no-ops, and implemented in epic 5 entry 6 (2026-10-12).
- Decision: the people.admin_window table and its seed row are owned by entry 4; restoring a previously held Administrator role through recover-admin does not change the window (2026-10-12).
- Tracer bullet: entry 1, create an Account through the API.
- Decision: sequencing is 1, then manager entries 2 and 3, then the role entries 4 and 5, then deactivation 6 after the role rules, then reset 7 and Privileged Requests 8, then recover options 9 and the race tests 10 (2026-10-12).
- Assumption: entries 1 to 8 all edit the one People account service from epic 2 entry 3, so entries 4, 5 and 6 are chained after the manager entries to keep the edits serial.
