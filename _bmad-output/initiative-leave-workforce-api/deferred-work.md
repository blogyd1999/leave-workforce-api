- source_plan: `_bmad-output/initiative-leave-workforce-api/epic-walking-skeleton/story-result-error-and-the-sec-11-problemdetails-mapper-plan.md`
  summary: Problem responses the framework produces itself (for example 415 or a bare 404 from status-code pages) carry no stable `code` extension.
  evidence: Quick review of entry 1.4 found that only Error-mapped responses and the 500 handler set `code`; AD-7 wants a stable code on every problem body, so epic 8 entry 5 (API hardening) should close the gap.
