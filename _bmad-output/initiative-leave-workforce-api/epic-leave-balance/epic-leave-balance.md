---
type: epic
title: "Employees see and Administrators manage leave balances"
parent: initiative-leave-workforce-api
covers: ["CAP-6"]
assignee: ""
risk: medium
---

# Employees see and Administrators manage leave balances

## Description

Employees view their own balance and Administrators set and adjust it with a reason, with adjustments serialised against concurrent work. This is the architecture's increment 3.

## Outcome

Balances are explainable and cannot go negative or below Reserved; the signal is the spec's CAP-6 success.

## Requirements

- FR-11 (CAP-6): Set the opening balance once.
- FR-12 (CAP-6): Adjust a balance as a signed delta.
- FR-13 (CAP-6): View balance.
- SEC-9 (CAP-6): An Employee reads only their own balance.

## Done when

1. An Employee sees Total, Reserved and Available through an own-only endpoint and never another Employee's balance, and a Manager never sees a report's Total or Reserved.
2. An Administrator sets the opening balance once and adjusts by deltas with a reason, bounds, the self-guard and audit.
3. An adjustment that would put Total below Reserved is refused with 409.
4. Concurrent set and adjust operations never lose an update.
5. An Administrator reading another Employee's balance writes one audit record per call.
6. The work is merged to the main branch with all tests green.

## Boundaries

The Leave module's balance use cases. The part of FR-13 where a Manager sees only Available on a request belongs to epic 6; requests, reservations and approval are epics 5 and 6.

## References

- parent — initiative-leave-workforce-api.md, section Done when
- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, CAP-6
- prd — _bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md, section 4.3
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md, AD-4, AD-5, AD-23

## Notes

- Decision: balance writes lock the Account through IAccountState, then the balance row, in the AD-4 order (2026-10-12).
- Decision: the once-only rule of the opening balance is a persisted flag on the balance row, which is stricter than the PRD's 'while Total is 0' and closes the case where Total returns to 0 before requests exist (2026-10-12).
- Tracer bullet: entry 1, an Employee views their own balance.
- Decision: entry 2 follows entry 1 because both edit the Leave balance read model and the Administrator policy wiring (2026-10-12).
- Assumption: setting or adjusting the balance of a deactivated Employee is refused with 409; the PRD is silent.
