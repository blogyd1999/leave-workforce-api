# Capability map

Maps each capability in `spec-leave-workforce-api.md` to the PRD requirements it realises, the architecture decisions that govern it, and the build increment (numbering from `solution-structure.md`). Acceptance criteria are the testable consequences listed under each FR in the PRD.

| CAP | PRD functional requirements | Security and other PRD items | Journeys, metrics | Governing ADs | Increment |
|---|---|---|---|---|---|
| CAP-1 | FR-27, FR-29, FR-30 | SEC-22 | UJ-6, UJ-7, SM-5, SM-7 | AD-8, AD-14, AD-21, AD-25 | 1b |
| CAP-2 | FR-2, FR-7, FR-8, FR-9, FR-10 | SEC-1 to SEC-6, SEC-24 | UJ-1, SM-7 | AD-6, AD-8, AD-9, AD-10, AD-22 | 1b |
| CAP-3 | FR-1, FR-4, FR-6, FR-28 | SEC-12 | UJ-3, UJ-7, SM-3 | AD-3, AD-4, AD-21, AD-24 | 2 (request side effects completed in 4) |
| CAP-4 | FR-5, FR-31 | SEC-12 | UJ-8, SM-2, SM-6 | AD-4, AD-11, AD-21, AD-23 | 2 |
| CAP-5 | FR-3, FR-6 (Direct Report list) | SEC-10 | UJ-3, UJ-5, SM-2 | AD-3, AD-4, AD-23, AD-24 | 2 (request side effects completed in 4) |
| CAP-6 | FR-11, FR-12, FR-13 | SEC-9 | UJ-1, UJ-3, SM-2 | AD-4, AD-5, AD-23 | 3 |
| CAP-7 | FR-14, FR-15, FR-16, FR-17 | SEC-9 | UJ-1, UJ-4, UJ-5, SM-2 | AD-4, AD-5, AD-16, AD-23 | 4 |
| CAP-8 | FR-18, FR-19, FR-20, FR-21 | SEC-10, SEC-11 | UJ-1, UJ-2, SM-2, SM-3 | AD-6, AD-7, AD-24 | 5 |
| CAP-9 | FR-22, FR-23, FR-24 | SEC-9, SEC-10 | UJ-1, UJ-2, SM-3 | AD-5, AD-6, AD-7 | 6 |
| CAP-10 | FR-17, FR-21 (response codes), all endpoints | SEC-7, SEC-8, SEC-9, SEC-10, SEC-11, SEC-13, SEC-14, SEC-15, SEC-16, SEC-17, SEC-18, SEC-20, SEC-21 | SM-3 | AD-6, AD-7, AD-18, AD-19 | grows from 1b; matrix complete by 7 |
| CAP-11 | FR-25, FR-26 | SEC-19, SEC-23 | SM-4 | AD-12, AD-13 | 1b (insert-only, hashes), 7 (read, purge, verify) |
| CAP-12 | FR-32 | PRD section 6 Time and Retention | none | AD-15 | 1b |
| CAP-13 | none (PRD section 6 Testability and Portability) | none | SM-8, SM-9 | AD-20, AD-25 | 1a |

## User stories to capabilities

| Story | CAP |
|---|---|
| US-1 | CAP-3, CAP-5 |
| US-2 | CAP-3 |
| US-3 | CAP-6 |
| US-4 | CAP-3, CAP-11 |
| US-5 | CAP-2 |
| US-6 | CAP-6 |
| US-7, US-8, US-9 | CAP-7 |
| US-10, US-11, US-12 | CAP-8 |
| US-13 | CAP-9 |
| US-14 | CAP-10 |
| US-15 | CAP-1 |
| US-16 | CAP-9 |
| US-17 | CAP-11 |
| US-18 | CAP-3 |
| US-19 | CAP-4 |
| US-20 | CAP-1 |
| US-21 | CAP-12 |

## Cross-capability metrics

- **SM-1** (all journeys UJ-1 to UJ-8 end to end) is the spec's Success signal and spans CAP-1 to CAP-9.
- **SM-C1 to SM-C3** (counter-metrics) apply to the whole build: do not trade the correctness, isolation and no-password-leak tests for more endpoints, faster approvals or less Operator effort.

## Decisions that bind every capability

The approved decisions (AP-1 to AP-9, RV-1 to RV-11, Q1 to Q10, Q-C and Q-D defaults) and the known limitations L-1 to L-12 are in PRD section 10 and apply across capabilities; they are not repeated here.
