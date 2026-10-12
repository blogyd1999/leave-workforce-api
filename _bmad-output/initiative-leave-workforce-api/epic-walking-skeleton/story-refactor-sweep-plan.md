---
title: 'Refactor sweep'
type: 'chore'
ticket: '8'
created: '2026-10-12'
status: built
baseline_revision: '9847011310c92a9e030b4e8033651481f96fb5eb'
route: 'oneshot'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context: []
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 1's plans and reviews left small loose ends: a README sentence that 1.3 made stale, two test packages the first plan kept only because it listed them (`xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`; the 1.1 notes say Microsoft.Testing.Platform may not need them), and two known gaps recorded only inside plan notes.

**Approach:** Fix the README sentence, remove the two packages if the build and every test still pass without them (keep them and record why if not), and append the two plan-note gaps to `deferred-work.md`. No behaviour, no new code, no architecture change.

</frozen-after-approval>

## Implementation Notes

Oneshot: about ten changed lines across config and docs, no code. Removed `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` (no longer needed under Microsoft.Testing.Platform). Added two deferred-work entries: the unread connection string and the unchecked DbContext rule. Closed: stale README text. Remaining deferred: framework-generated problem codes (epic 8 entry 5).

## Review Triage Log

Quick review, pass 1: 5 findings (high 0, medium 0, low 2, false 1, n/a 2).

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| README line 15 says the key "is read" while the new line 19 says the API does not read it | low | patch | Real contradiction. Line 15 now says the string will be read and the API does not read it yet; the duplicate sentence on line 19 was removed. |
| Deferred entry cites 1.1's review removal and epic 2 entry 1 unverified | false | rejected | The 1.1 plan's Implementation Notes record the removal; the 1.5 notes name epic 2 entry 1 for the next unit-of-work step. |
| "`src` has no DbContext yet" is imprecise (`IUnitOfWorkScope` takes `DbContext`) | low | patch | Reworded to "no `DbContext` subclass". |
| Package removal criterion | n/a | none | Reviewer confirmed build 0 warnings and 94 of 94 tests pass without the packages. |
| Plan status and review fields not yet set | n/a | none | Bookkeeping, set when finalizing. |
