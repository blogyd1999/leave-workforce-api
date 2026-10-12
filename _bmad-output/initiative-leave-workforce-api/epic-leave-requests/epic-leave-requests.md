---
type: epic
title: "Employees submit, cancel and track leave requests"
parent: initiative-leave-workforce-api
covers: ["CAP-7", "CAP-3", "CAP-5"]
assignee: ""
risk: high
---

# Employees submit, cancel and track leave requests

## Description

Employees submit leave for whole Working Days with balance and overlap protection, cancel Pending requests and track status, and the Leave module completes the Pending-request side effects that administration calls. This is the architecture's increment 4. It owns the leave_requests table and the Request Reader rule.

## Outcome

No request can exceed the balance or overlap other Pending or Approved leave, even under concurrency; the signal is the spec's CAP-7 success. It also completes the request side effects of CAP-3 and CAP-5.

## Requirements

- FR-14 (CAP-7): Submit a Leave Request.
- FR-15 (CAP-7): Prevent overlapping requests.
- FR-16 (CAP-7): Cancel a Pending request.
- FR-17 (CAP-7): Track request status.
- SEC-9 (CAP-7): An Employee reads only their own requests.
- FR-3 (CAP-5): Completion only: Pending requests move or cancel when a manager changes (owned by epic 3).
- FR-4 (CAP-3): Completion only: Pending requests cancel on deactivation (owned by epic 3).

## Done when

1. A valid request becomes Pending, reserves its days and is assigned to the Direct Manager, with every refusal of FR-14 returning the specified code in the specified order.
2. Overlap is judged on Working Days and enforced by the database exclusion constraint.
3. Cancel releases the Reservation and only the owner can cancel; non-readers receive 404 and readers 403; the owner can view status, decision time, Decider and rejection reason of a request.
4. Moving and cancelling Pending requests when a manager changes or an Account is deactivated work end to end through the epic 3 flows.
5. Concurrent submissions that together exceed the balance or overlap never both succeed.
6. The work is merged to the main branch with all tests green.

## Boundaries

The Leave module's request use cases, using the ports declared in epic 2. Not approval, rejection or history lists (epics 6 and 7). Request status history is held in columns on leave_requests, not a separate table.

## References

- parent — initiative-leave-workforce-api.md, section Done when
- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, CAP-7
- prd — _bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md, section 4.4
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md, AD-4, AD-16, AD-23, AD-24
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/solution-structure.md, sections 4 and 5

## Notes

- Decision: request status history lives in columns on leave_requests: submitted_at, decided_at, decider_id, decision_reason, cancelled_at, cancelled_by and cancel_reason (2026-10-12).
- Decision: entry 4 owns the Request Reader rule (owner, current Direct Manager, Decider, Administrator) that epics 6 and 7 reuse (2026-10-12).
- Tracer bullet: entry 1, submit a Monday to Friday request and see it Pending with days reserved.
- Decision: sequencing is 1, then 2, 3 and 4 after 1 (3 after 2 because both edit the submit handler), then 5 after 4, side effects 6 after 4, race tests 7 last (2026-10-12).
- Assumption: FR-3 and FR-4 are covered here only for the request side effects; their owner is epic 3, and the initiative lists CAP-3 and CAP-5 as shared.
