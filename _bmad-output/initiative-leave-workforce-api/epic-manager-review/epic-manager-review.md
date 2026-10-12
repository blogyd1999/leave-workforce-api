---
type: epic
title: "Managers approve and reject their reports' requests"
parent: initiative-leave-workforce-api
covers: ["CAP-8", "CAP-6"]
assignee: ""
risk: high
---

# Managers approve and reject their reports' requests

## Description

Managers list their Direct Reports' requests and approve or reject them, and nobody else can decide. This is the architecture's increment 5.

## Outcome

Approval and rejection keep the balance exact and only the current Direct Manager decides; the signal is the spec's CAP-8 success.

## Requirements

- FR-18 (CAP-8): List requests to review.
- FR-19 (CAP-8): Approve a request.
- FR-20 (CAP-8): Reject a request.
- FR-21 (CAP-8): Decision authority.
- SEC-10 (CAP-8): A Manager acts only on current Direct Reports.
- SEC-11 (CAP-8): Response codes for decision calls.
- FR-13 (CAP-6): Completion only: a Manager sees only the Employee's Available days on a report's request (owned by epic 4).

## Done when

1. A Manager sees only current Direct Reports' requests with name, dates, days, note and Available days.
2. Approval moves Reserved into a reduction of Total atomically and is refused when the First Working Day has passed.
3. Rejection needs a reason and releases the Reservation, including after the First Working Day has passed.
4. Owner, Administrator, former manager and non-reader callers receive exactly the 403 or 404 SEC-11 defines, including after races.
5. Concurrent decisions on one request succeed once.
6. The work is merged to the main branch with all tests green.

## Boundaries

The Leave module's review use cases. Not history views (epic 7) and not the full authorization matrix across all endpoints (epic 8).

## References

- parent — initiative-leave-workforce-api.md, section Done when
- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, CAP-8
- prd — _bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md, section 4.5
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md, AD-4, AD-6, AD-7, AD-24

## Notes

- Decision: race losers are answered from committed state, as AD-7 states (2026-10-12).
- Decision: entries 2 and 3 include the basic rule that only the current Direct Manager decides, and entry 4 completes the response-code matrix and races (2026-10-12).
- Tracer bullet: entry 2, approve a request end to end; entry 1 is read-only.
- Decision: sequencing is 1, then 2, then 3 after 2 because both share the decision path and columns, then 4 and the race tests 5 (2026-10-12).
