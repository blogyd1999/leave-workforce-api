---
type: epic
title: "People see exactly the leave history they are entitled to"
parent: initiative-leave-workforce-api
covers: ["CAP-9"]
assignee: ""
risk: medium
---

# People see exactly the leave history they are entitled to

## Description

Employees list their own history, Managers see their reports' history, and Administrators have audited read-only access. This is the architecture's increment 6.

## Outcome

Nobody sees another person's leave data beyond their entitlement; the signal is the spec's CAP-9 success.

## Requirements

- FR-22 (CAP-9): Leave history for the Employee.
- FR-23 (CAP-9): Report visibility for the Manager.
- FR-24 (CAP-9): Administrator visibility, read-only.
- SEC-9 (CAP-9): An Employee reads only their own data.
- SEC-10 (CAP-9): A Manager sees only current Direct Reports, and a former Decider only requests they decided.

## Done when

1. An Employee's history lists only their own requests, filtered, newest first and paged, with status history.
2. A Manager sees each current Direct Report's history with names, deactivated reports included; a former Manager sees only requests they decided and never balances.
3. Administrators read any Employee's requests read-only, one audit record per call.
4. A visibility test matrix over the roles passes.
5. The work is merged to the main branch with all tests green.

## Boundaries

Read use cases in the Leave module. Not new write paths and not the audit query (epic 8).

## References

- parent — initiative-leave-workforce-api.md, section Done when
- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, CAP-9
- prd — _bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md, section 4.6
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md, AD-5, AD-6, AD-7

## Notes

- Decision: one audit record per Administrator read call, with the target, filters and result count (2026-10-12).
- Decision: entry 1 owns the shared history query and DTO and the Employee's own request list; entry 2 reuses epic 6 entry 1's Direct Report query (2026-10-12).
- Tracer bullet: entry 1, an Employee lists their own history.
- Decision: sequencing is 1, then 2 and 3 after 1, then the matrix 4 (2026-10-12).
