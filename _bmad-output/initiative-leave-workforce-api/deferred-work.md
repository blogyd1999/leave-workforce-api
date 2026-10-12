- source_plan: `_bmad-output/initiative-leave-workforce-api/epic-walking-skeleton/story-result-error-and-the-sec-11-problemdetails-mapper-plan.md`
  summary: Problem responses the framework produces itself (for example 415 or a bare 404 from status-code pages) carry no stable `code` extension.
  evidence: Quick review of entry 1.4 found that only Error-mapped responses and the 500 handler set `code`; AD-7 wants a stable code on every problem body, so epic 8 entry 5 (API hardening) should close the gap.

- source_plan: `_bmad-output/initiative-leave-workforce-api/epic-walking-skeleton/story-refactor-sweep-plan.md`
  summary: `Program.cs` does not read `ConnectionStrings:App`; the key exists only in `appsettings.json` and the test `ApiFactory`.
  evidence: Entry 1.1's review removed an unused holder record, so the first real read arrives with epic 2 entry 1 (first DbContext and unit-of-work registration).

- source_plan: `_bmad-output/initiative-leave-workforce-api/epic-walking-skeleton/story-refactor-sweep-plan.md`
  summary: The architecture rule that `DbContext` types are internal and unused outside their module has no real-world check until a real DbContext exists.
  evidence: Entry 1.6 proves the rule fails on fixture types, but `src` has no `DbContext` subclass yet; epic 2 entry 1 should confirm it passes on the first real one.
