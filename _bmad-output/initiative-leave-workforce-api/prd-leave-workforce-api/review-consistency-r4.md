# Consistency Review R4: brief, PRD and decision logs

Scope: brief-leave-workforce-api.md (BRIEF), prd-leave-workforce-api.md (PRD), and both hidden .memlog.md files (BRIEF-LOG, PRD-LOG). No file modified except this report.

Totals: critical 0, high 0, medium 3, low 11.

## Findings

### M-1 (medium) State of an Account after a Temporary Password is issued is ambiguous
- PRD §3 Awaiting Password: "has no usable password and cannot log in until the Operator issues a Temporary Password."
- PRD FR-7: "an Awaiting Password Account ... return the same generic 401", while FR-7 and FR-2 also let a Temporary Password log in.
- PRD FR-30: issuing is for "an Account that is Awaiting Password", and "issuing a new one invalidates the previous one" (so the Account is still Awaiting Password after the first issue).
- PRD UJ-3 edge: "the Account stays Awaiting Password" after expiry.
- Fix: in the §3 Awaiting Password definition, say the Account stays Awaiting Password until the Temporary Password is consumed. In FR-7, say a Temporary Password login is the one exception to the Awaiting Password 401.

### M-2 (medium) UJ-8 "grant a fourth Administrator directly ... refused (403)" has no matching FR
- PRD UJ-8 edge: "grant a fourth Administrator directly. Both are refused (403)."
- FR-31 gives no code for a direct create or grant after the window closes.
- FR-1 says only "A request with the Administrator role flag is subject to FR-31", which could be read as "becomes a Privileged Request".
- SEC-11 rule 5 has no example for this case.
- Fix: add one FR-31 consequence: "After the window closes, FR-1 with the Administrator flag and FR-5 grant by any direct call are refused (403); only a Privileged Request can take effect." Name the code and add it to the SEC-11 rule 5 examples.

### M-3 (medium) BRIEF Executive Summary overstates two-person control
- BRIEF Executive Summary: "the most powerful administrator actions need a second Administrator."
- BRIEF Known Limitations and PRD L-9: only creating or granting an Administrator needs two people. Deactivating, reactivating and removing the role of another Administrator are single-actor.
- Fix: replace with "creating or granting an Administrator needs a second Administrator."

### Low findings
- L-1. PRD line 9 "*Working title — confirm.*" is leftover unapproved-style text. Remove it or set the title as decided.
- L-2. PRD §10.1 heading "Accepted proposals Q-E1 to Q-E8" uses the word proposals. Acceptable, but "Accepted decisions" removes any doubt.
- L-3. BRIEF decision table wording is terse or lossy.
  - Q-E row: "two-person scope and 24-hour expiry" reads as a password expiry. It means the Privileged Request expiry (PRD Q-E3).
  - Q1,Q2... row: Q2 omits "configurable".
  - AP-9 row omits Operator Operations (PRD D1 includes them).
  - RV-3 row omits session validity (PRD RV-3 includes it).
  - Fix: reword these cells.
- L-4. BRIEF omits two PRD facts. Neither is wrong.
  - Release of Reservations on deactivation and on manager clearing is covered only by the AP-2 and Q-E rows. BRIEF Solution says only "rejection or cancellation releases it".
  - L-12 (a closed window never reopens; the Operator reactivates an Administrator) is absent.
  - Fix: add a half sentence to Solution and a Known Limitations bullet.
- L-5. PRD §3 "Audit Record — an immutable entry" has no retention exception, but FR-25, SEC-19 and SEC-23 say "immutable except for the retention process". Add the exception to the glossary.
- L-6. PRD FR-17 says the response has "the Direct Manager's name and no other manager data", but the same response includes the Decider (FR-17, UJ-1). Clarify that the Decider is allowed.
- L-7. PRD FR-14 refuses a weekend-only range with 409. SEC-11 rule 7 does not list it, and it could be read as 400 input. Add it to the rule 7 examples or move it to 400.
- L-8. PRD FR-12: "resulting Total non-negative ... otherwise 400" depends on current state. It overlaps the 409 for "Total lower than Reserved". Say that the below-Reserved 409 applies whenever the current state is involved.
- L-9. Unspecified response or limit details.
  - FR-11 gives no code for setting an opening balance a second time.
  - D9 and SEC-14 give length limits for reject reasons, but FR-3 (clearing reason), FR-11, FR-12 and FR-29 reasons have none.
  - FR-3 first bullet "cannot be their own Direct Manager" has no code.
- L-10. PRD Glossary Security-Class Event does not say whether login success is Security-Class or Ordinary. By "all others" it is Ordinary. State it.
- L-11. BRIEF Q2 and PRD memlog wording "Asia/Kolkata" appears without "operator configurable" in the BRIEF row only. Minor, covered in L-3.
- L-12. BRIEF Solution does not mention the Must-Change session, only "one-login". Acceptable, optional.
- L-13. PRD AP-9 / §8.3 cross-references "Q-D D1" in both places. Fine, but the BRIEF AP-9 row says "credentials, login and audit writing" without Operator Operations (see L-3).
- L-14. PRD-LOG and BRIEF-LOG end with "Requirements round 4 edits complete; running reviewer gate" and no later finalize event. Correct while the status is draft.

## Memlog items: superseded, fine (not defects)
- BRIEF-LOG and PRD-LOG "RV-5 approved (C1): ... creating or resetting an Administrator". Superseded by "RV-5 REVISED".
- RV-6 "interpretation pending Q-E2". Superseded by "Q-E1..Q-E8 ACCEPTED".
- RV-1 "single-use 7-day initial/reset passwords". Superseded by the explicit "SUPERSEDES" line, RV-10 and RV-11.
- "(assumption) UNAPPROVED ... Q-E1..Q-E8". Superseded by ACCEPTED, and the "UNAPPROVED markers removed" line says so.
- "Recommended defaults pending confirmation" (Q3, Q5, Q9, Q10), "Review-driven defaults awaiting confirmation" (including 5 failures / 15 min lockout, no clearing of Direct Manager). Superseded by Q-C and Q-D ACCEPTED and RV-7 / RV-8.
- "No max request length (D8)", "'start date' approval rule", "Brief finalized with PRD" and "PRD finalized". Superseded by RV-4, the status-reverted lines and the SUPERSEDES line.
- "awaiting user review" and "awaiting owner decisions". Superseded.
- No memlog line is both un-superseded and misleading.

## Verdict per topic
1. Administrator lifecycle: consistent in all three, except M-2 (UJ-8 403 untraceable) and M-3 (brief overstatement). Brief lacks L-12 (L-4).
2. Administrator reset and FR-28 targets: consistent. FR-28, UJ-7, Q-E2, RV-6, FR-29 and the brief agree. The brief does not list the three targets, which is fine.
3. Temporary passwords: lifecycle values (24h, one login, operator verifies, no ticket workflow, never in API or logs, Must-Change, return to Awaiting Password) agree in the brief, PRD and logs. One gap, M-1, on the Awaiting Password state after issuance.
4. Reservations and balance: consistent, including release on deactivation (FR-4) and on manager clearing (FR-3, Q-E4). The brief covers them only via decision rows (L-4).
5. Approval rules and first working day: consistent (FR-14, FR-19, RV-4, D10, brief scope and decision rows). The old "start date" log wording is superseded.
6. Concurrency and response codes: consistent 400 vs 409 ordering across FR-14, FR-19, FR-20, FR-21, SEC-11 and §6. Only edge ambiguities (L-7, L-8, L-9) and M-2.
7. Retention and audit protection: consistent. Only the glossary immutability wording (L-5).
8. Decision IDs: the brief table maps to PRD §10.1 completely (AP-1..9, RV-1..11, Q1/Q2/Q4/Q6/Q7/Q8, Q-E1..8, Q-C, Q-D). Both memlogs record RV-5 REVISED, RV-10, RV-11, Q-E ACCEPTED and Q-C/Q-D ACCEPTED. Wording gaps only (L-3).
9. Status: no artifact calls anything awaiting approval except Q-A and Q-B (PRD §10.3, BRIEF Open Questions). Leftovers: PRD line 9 "confirm" (L-1) and the "Accepted proposals" heading (L-2). No "unapproved" text in BRIEF or PRD; memlog UNAPPROVED line superseded.
10. Frontmatter: both `status: draft`, updated 2026-10-12. Correct for now. Set both to final after review.

## Stale or misleading brief text (summary)
- M-3 Executive Summary "most powerful administrator actions".
- L-3 terse decision-table cells (Q-E "24-hour expiry", Q2 without "configurable", AP-9 and RV-3 omissions).
- L-4 missing release-on-deactivation or clearing wording and missing L-12 limitation.
- The brief header "earlier D1 to D18 numbering is retired" is correct and not stale.
