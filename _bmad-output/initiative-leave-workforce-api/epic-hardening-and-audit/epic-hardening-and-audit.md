---
type: epic
title: "The system is audited, hardened and verified end to end"
parent: initiative-leave-workforce-api
covers: ["CAP-10", "CAP-11"]
assignee: ""
risk: high
---

# The system is audited, hardened and verified end to end

## Description

Administrators can query the audit trail, the audit chains can be purged and verified, every endpoint is covered by the authorization matrix, transport and logging are hardened, the PRD's races are stress-tested, and the journeys run end to end. This is the architecture's increment 7. It completes CAP-10 and CAP-11, whose pipeline and insert-only writer epic 2 delivered.

## Outcome

The security and correctness claims of the PRD are demonstrated by tests; the signal is the spec's CAP-10, CAP-11 and Success signal.

## Requirements

- FR-25 (CAP-11): Completion: every listed event except the purge (entry 3) is recorded and a failed audit write rolls the change back.
- FR-26 (CAP-11): Read audit records.
- SEC-7 (CAP-10): Completion: authentication on every endpoint, proved by the matrix.
- SEC-8 (CAP-10): Completion: server-side authorization, proved by the matrix.
- SEC-9 (CAP-10): Completion: Employee isolation across all endpoints.
- SEC-10 (CAP-10): Completion: Manager isolation across all endpoints.
- SEC-11 (CAP-10): Completion: response codes across all endpoints.
- SEC-12 (CAP-10): Self-dealing and restricted-target cases across all endpoints.
- SEC-13 (CAP-10): Completion: next-call refusal of a deactivated or demoted caller across endpoints.
- SEC-14 (CAP-10): Input validation and no mass assignment.
- SEC-15 (CAP-10): HTTPS and secrets.
- SEC-16 (CAP-10): No internals in errors.
- SEC-17 (CAP-10): CORS, secure headers, OpenAPI and health.
- SEC-18 (CAP-10): Non-guessable identifiers.
- SEC-19 (CAP-11): Completion: no secrets in audit records, Administrator-only reads.
- SEC-20 (CAP-10): Privacy-conscious data exposure.
- SEC-21 (CAP-10): No personal data or secrets in logs.
- SEC-23 (CAP-11): Completion: purge role, anchors and tamper evidence.

## Done when

1. Administrators query Audit Records by target, actor, action and time, each query audited, and no other role can read them.
2. Every event in FR-25 except the purge produces a record, a failed audit flush rolls the change back, and no record contains a secret; `purge-audit` runs under the retention role with anchors and a Security-Class summary, and `verify-audit` detects an altered record.
3. An authorization matrix over every endpoint, role and resource class passes with the codes of SEC-11, including all self-dealing and restricted-target cases and next-call refusal of deactivated or demoted callers.
4. Transport hardening, validated input, non-guessable ids and the log, response and audit leak scan pass.
5. The stress suite over the PRD section 6 races not covered by earlier epics ends with all SM-2 invariants intact.
6. UJ-1 to UJ-8 pass end to end through the API and Operator Operations, and the work is merged to the main branch with all tests green.

## Boundaries

Audit read, retention and verification, cross-cutting security tests and hardening, and the closing end-to-end suite. Not new business features and not Azure deployment.

## References

- parent — initiative-leave-workforce-api.md, section Done when
- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, CAP-10, CAP-11, Success signal
- prd — _bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md, sections 4.7, 5, 6 and 9
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md, AD-6, AD-7, AD-12, AD-13, AD-14, AD-18, AD-19, AD-20
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/solution-structure.md, section 8

## Notes

- Assumption: CAP-10 and CAP-11 are shared with epic 2, which delivered the pipeline and insert-only writer; this epic completes them.
- Decision: the closing end-to-end suite runs after the refactor sweep, as the guide orders (2026-10-12).
- Decision: entry 7 owns only the PRD section 6 races not covered in epics 3, 5 and 6: two concurrent reassignments forming a cycle, Temporary Password login versus reissue or reset, and adjustment versus approval (2026-10-12).
- Decision: entry 3 owns the chain_anchors table and the retention_job grants; hashes are written from epic 2 (2026-10-12).
- Tracer bullet: entry 1, an Administrator queries the audit trail.
- Decision: sequencing is the audit entries 1 to 3, the matrix 4, hardening 5 and 6, the stress suite 7, then the sweep and the closing suite (2026-10-12).
