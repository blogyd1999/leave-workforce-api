---
type: initiative
title: "Leave and Workforce Management API"
parent: none
covers: ["CAP-1", "CAP-2", "CAP-3", "CAP-4", "CAP-5", "CAP-6", "CAP-7", "CAP-8", "CAP-9", "CAP-10", "CAP-11", "CAP-12", "CAP-13"]
assignee: ""
risk: high
---

# Leave and Workforce Management API

## Description

A secure, incrementally built backend for one organisation's leave: Employees request leave, Managers decide, Administrators run accounts and balances, and an Operator deploys and recovers the system. The spec owns the capabilities, constraints and non-goals; this initiative delivers them as eight epics that follow the architecture's increments.

## Outcome

An Employee, a Manager, an Administrator and an Operator complete every journey UJ-1 to UJ-8 end to end through the API and Operator Operations, with the concurrency, authorization, audit and no-password-leak tests passing; this is the spec's Success signal.

## Done when

1. UJ-1 to UJ-8 pass end to end in the closing suite of epic 8, against a fresh database.
2. Every capability CAP-1 to CAP-13 meets its success criterion in the spec.
3. The concurrency stress suites over the races in PRD section 6 end with all SM-2 invariants intact.
4. The authorization matrix, audit completeness and log-leak tests pass.
5. Everything is merged to the main branch with all tests green.

## Boundaries

Follows the module and ownership boundaries of the architecture spine: one epic per build increment, each owned by the same developer with agent lanes. Not the React front end, Azure deployment, CI, notifications, reporting or any item in the spec's Non-goals. Tracer path across epics: bootstrap an Administrator, log in, create an Employee with a manager and a balance, submit a request and approve it.

- Touch point: PostgreSQL 18 roles — `deploy/db/roles.sql` and the compose file; owner: epic-walking-skeleton (each epic's migrations create its own tables and schema)
- Touch point: operator runbook — a document the Operator follows; owner: epic-first-vertical-slice (entry 14), extended by epic-hardening-and-audit entry 3 with the purge and verify procedures

## References

- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, section Capabilities
- constraint — the same spec, section Constraints
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/solution-structure.md
- prd — _bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md, for acceptance criteria and glossary

## Notes

- Decision: eight epics, one per architecture increment, all planned into stories now, each with a closing Refactor sweep (2026-10-12).
- Decision: delivery means merged to the main branch with all tests green. Azure deployment and CI are out of scope for this MVP, so "production delivery" in each epic's Done when is that merge and green means a local run (2026-10-12).
- Decision: epic 2 owns the shared foundations later epics build on: the audit writer, the Leave skeleton with its declared contracts and ports, the auth pipeline with the Administrator policy, and the test support helpers (2026-10-12).
- Assumption: CAP-10 and CAP-11 are shared; epic 2 delivers the authentication pipeline and the insert-only audit writer, epic 8 completes the authorization matrix, audit query, purge and verification.
- Assumption: CAP-3 and CAP-5 are shared with epic 5; epic 3 delivers the administration use cases with recording no-ops for the Leave side effects, and epic 5 entry 6 makes them real.
- Assumption: CAP-1 is shared with epic 3, which adds recover-admin's optional reactivate and role-restore (entry 9); CAP-6 is shared with epic 6, which delivers the Manager's Available-days view on a request.
- Open question: Q-A, which jurisdiction-specific privacy, labour or legal-retention rules would apply before any real-data use.
- Open question: Q-B, how the Top-Most Person's leave should be handled once it is needed.
