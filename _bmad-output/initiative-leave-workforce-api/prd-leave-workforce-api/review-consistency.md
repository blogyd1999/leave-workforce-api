# Consistency review: brief, PRD and memlogs

Artifacts: BRIEF = brief-leave-workforce-api.md, PRD = prd-leave-workforce-api.md, BML = brief .memlog.md, PML = PRD .memlog.md. Line numbers refer to those files. No existing file was modified.

Totals: critical 0, high 0, medium 8, low 14.

## Topic verdicts

1. Account recovery and lockout: consistent (only low items L1, L2, L3, L4)
2. Temporary passwords: INCONSISTENT (M1, M2, M3)
3. Reservations and balance: consistent (low items only, L5, L6, L7)
4. Approval rules: INCONSISTENT in wording only (M4); the rules themselves agree
5. Concurrency: INCONSISTENT (M8)
6. Separation of duties: INCONSISTENT in the summaries (M6); the PRD itself is consistent
7. Decision IDs and status: INCONSISTENT (M7, plus L8, L9, L10)
8. Frontmatter vs finalization: consistent (BRIEF `status: final`, PRD `status: final`, BML line 30 "Brief finalized with PRD", PML line 38 "PRD finalized")

## Medium findings

### M1. BRIEF D16 says all initial passwords are server-generated; the PRD says bootstrap and recovery passwords are operator-supplied
- BRIEF D16 (line 96): "initial and reset passwords are server-generated, temporary and expire in 7 days".
- PRD FR-27 (line 198): the initial password "is supplied at invocation through a channel that is not logged". FR-29 (line 253): "is supplied through a channel that is not logged". PRD §10.1 RV-1 (line 565): "the bootstrap and recovery passwords (FR-27, FR-29) are supplied by the operator".
- BML line 26 and PML line 31 say "server-generated single-use 7-day initial/reset passwords", with no operator-supplied exception. The first Administrator's initial password is covered by neither log's wording.
- Fix: reword D16 and the memlog entries. Admin-created and admin-reset passwords are server-generated. Bootstrap and recovery passwords are operator-supplied, must meet SEC-2, and expire in 7 days.

### M2. "Single-use" in the memlogs vs "temporary / returned once" in the PRD
- BML line 26 and PML line 31: "single-use 7-day ... passwords".
- PRD uses "temporary" and "returned exactly once" (FR-2 line 148, FR-28 line 244). "Single-use" is reserved for Refresh Tokens (Glossary line 124).
- Nothing in the PRD says whether the temporary password can authenticate more than once. FR-2 (line 152) only restricts the resulting session to change-password and logout. "Single-use" in the log therefore implies a rule the PRD does not state.
- Fix: choose one meaning. Either state in FR-2 that the temporary password is valid for any number of logins until changed or 7 days pass, or state that it is consumed at first login. Align the memlog wording ("temporary, shown once").

### M3. RV-1 is recorded as owner-approved but the same facts are still tagged as unconfirmed assumptions
- PRD §10.1 RV-1 (line 565) and BRIEF D16 under "Decisions (owner-approved)" fix server-generated passwords with a 7-day expiry.
- PRD FR-2 (line 148): "`[ASSUMPTION: server-generated initial password returned once to the Administrator.]`". FR-2 (line 151): "7 days `[ASSUMPTION: indicative]`". PRD §10.4 Q-D (line 606) and PML line 36 list "server-generated initial passwords with 7-day expiry" as a default still awaiting confirmation.
- The status of the 7-day value is therefore contradictory: approved in one place, awaiting confirmation in another.
- Fix: remove the assumption tags and the Q-D entry for the approved parts of RV-1, or say explicitly that only the exact 7-day figure is indicative.

### M4. BRIEF says Administrators "do not decide leave" without the D17 exception
- BRIEF line 29: "Administrators manage accounts and balances but do not decide leave."
- BRIEF D12 (line 92): "administrators read requests but cannot decide them".
- BRIEF D17 (line 97): "an Administrator who is legitimately a direct manager may decide as a manager".
- PRD FR-21 (line 373) and FR-24 (line 400) are scoped to "an Administrator who is not the request's Direct Manager".
- Fix: add "unless they are that employee's Direct Manager, acting as a Manager" to line 29 and D12.

### M5. BRIEF scope omits password reset, operator recovery and forced first-login change
- BRIEF "In for the MVP" (lines 53-61) lists bootstrap, login and refresh tokens but not password reset, operator recovery or must-change-password. The Administrator persona (line 40) also omits reset.
- These appear only in D16. The PRD puts FR-28 and FR-29 in scope (§8.1 line 519).
- Fix: add "Administrator password reset, an operator recovery operation, and forced password change at first login" to the In-scope list and the Administrator persona.

### M6. BRIEF D17 and the memlog RV-2 entries omit two separation-of-duties rules that the PRD states
- PRD RV-2 (line 566), SEC-12 (line 476) and FR-3 (line 159): an Administrator cannot assign themselves as another Employee's Direct Manager "at creation or later" and "cannot change their own Direct Manager".
- BRIEF D17 (line 97) says only "no self-assigned reports, self role changes, self-deactivation or own-balance changes".
- BML line 27 and PML line 32 say only "self-manager assignment, self role change, self-deactivation, own balance". The "at Account creation" and "own Direct Manager" parts appear only in PML line 35 ("N-1 self-manager guards in FR-1/FR-3/SEC-12").
- The PRD is internally consistent: FR-1 line 139, FR-3 line 159, FR-4 line 173, FR-5 line 183, FR-11 line 271 and FR-12 line 282 agree.
- Fix: extend D17 and the RV-2 memlog lines with "including at Account creation" and "cannot change their own Direct Manager".

### M7. D-numbers cannot be traced: the PRD uses AP/RV/Q IDs, and the memlogs do not map them to D1..D18
- BRIEF line 77: "Recorded in the PRD (§10.1) and in each `.memlog.md`."
- PRD §10.1 has no D-IDs. It names D8 only in RV-4 (line 568).
- PML line 7 refers to "brief D1-D10" only.
- BML line 24: "brief D11-D15 added". D11 (404 rule) and D12 (manager sees only Available) are recorded under AP-1 and AP-3 in PML (lines 10 and 12), not in BML. D16, D17 and D18 are never named in either memlog. They map implicitly to RV-1, RV-2 and RV-3 (BML lines 26-28).
- Fix: add a D-to-AP/RV/Q mapping. Proposed mapping: D3 = Q8; D4 = AP-4; D5 = AP-2; D6 = AP-7; D7 = AP-8; D8 = RV-4; D9 = AP-2; D10 = AP-5; D11 = AP-1; D12 = AP-3; D13 = Q2; D14 = Q4; D15 = Q7; D16 = RV-1; D17 = RV-2; D18 = RV-3. Add one memlog line saying D16-D18 were added to the brief with RV-1..3.

### M8. Concurrency: the §6 race list is incomplete, and loser responses are not consistently 409
- PRD §6 (line 493) names: approve vs cancel, approve vs reassignment, submit vs deactivation, concurrent reassignments forming a cycle, two Administrators removing each other's role, two concurrent balance adjustments. SM-2 (line 538) tests "the races listed in §6".
- Named elsewhere but not in §6: concurrent submissions that overlap or exceed the balance (FR-14 line 309); two concurrent decisions on one request (FR-19 line 356); concurrent bootstrap (FR-27 line 199).
- Not named anywhere: an adjustment racing a submit or approve. FR-12 (line 279) refuses Total below Reserved "at commit time", so this is the AP-8 race. Also unnamed: reset vs login and reactivation vs manager deactivation.
- Loser response: §6 says 409 with a stable code, and FR-12 says 409. FR-14 and FR-27 give no code for the loser. FR-19 says "succeed exactly once" and relies on FR-21 (line 374) "no longer Pending returns 409".
- BRIEF (line 46) names only concurrent submissions.
- Fix: add the missing races to §6 (or state the list is illustrative), and add "loser receives 409" to FR-14, FR-27 and FR-19.

## Low findings

- L1. Brief D10 and BML line 18 ("login throttling/lockout") do not carry the per-account-and-source rule (RV-1, SEC-5). Fix: add it to D10 or D16.
- L2. Terminology is mixed: PRD FR-7 (line 218) says "throttling or temporary lockout", FR-25 (line 409) says "lockout", and SEC-5 uses a 15-minute 429 refusal. Fix: pick one term.
- L3. FR-2 (line 151) says that after the initial password expires "login is refused until an Administrator resets the password (FR-28)". For the only or bootstrapped Administrator this means FR-29. FR-7 does not list the expired-initial-password refusal or its code. Fix: mention FR-29 and add the code.
- L4. FR-29 (line 255) reactivates "only if the operator explicitly asks". FR-4 (line 176) refuses reactivation while the Direct Manager is inactive. FR-29 does not say whether the FR-4 guard applies. Also, FR-28's own-reset 403 (line 246) is not a recorded decision in §10.1 or either memlog. Fix: state both.
- L5. BRIEF D5 and BML line 13 say "release on reject or cancel" only. Release on deactivation is in D9 (line 89) and PRD FR-4 (line 175). Fix: add it to D5 or cross-reference D9.
- L6. PRD FR-4 (line 174) refuses deactivation while there are "active Direct Reports". BRIEF D9 (line 89) and AP-2 say "direct reports". Fix: align the wording.
- L7. The brief says nothing of the delta-only adjustment rule, the 1000 bound or the zero starting balance (PRD FR-11, FR-12; PML lines 28 and 29). The "Recommended Defaults" section (BRIEF lines 113-120) omits the Q-D review-driven defaults (zero starting balance, working-day overlap, lockout numbers and others). Fix: add a pointer to PRD §10.4 Q-D.
- L8. BML line 16 is stale: "No max request length or year-boundary rule". RV-4 (BML line 29) supersedes it, and BRIEF D8 is amended. Also stale: BML line 18 ("exact values deferred to architecture"; the PRD now gives indicative values) and line 21 ("MVP treats admin as account/balance manager only"; D17 and RV-2 let an Administrator who is also a Direct Manager decide as a Manager). Fix: append "(superseded by ...)" markers.
- L9. Stale counts and statuses in PML: line 9 ("26 FRs, 21 SEC, 14 stories, 9 approvals ... 10 open questions") vs the final PRD (FR-1..FR-29, SEC-1..21, US-1..18); line 8 ("not yet confirmed: 404..., bootstrap, audit retention, top-level approver") vs AP-1, Q8, Q4 and Q1 resolved on lines 10, 20 and 22; line 14 (AP-4 unqualified) vs "Clarifies AP-4" in RV-2. Fix: mark them superseded.
- L10. Completeness: BML does not record Q6 or the substance of AP-1..AP-9 (it says only "AP-1..AP-9 approved"). PML records AP-1..9, Q1, Q2, Q4, Q6 (via AP-8), Q7, Q8 and RV-1..4, so every ID in PRD §10.1 is recorded, with Q6 only inside the AP-8 line. Q3, Q5, Q9 and Q10 appear unlabelled in BML line 25. Fix: add labelled lines.
- L11. BRIEF D14 (line 94) says "append-only". PRD Glossary (line 119) says "append-only" while FR-25 (line 417) allows a retention purge. The PRD's §4.7 description says "immutable except for the retention process". Fix: use "append-only except retention purge" everywhere.
- L12. BRIEF line 65 ends with a stray semicolon: "(a known MVP limitation);". Fix: replace with a full stop.
- L13. BRIEF "Open Questions" (lines 122-125) are correctly still open (PRD Q-A and Q-B), so none is stale. However, the brief does not say that Q3, Q5, Q9 and Q10 are also awaiting confirmation (PRD Q-C); BRIEF lines 113-120 list them without IDs. Also, PRD §10.3 Q3 ("roles combine") is effectively approved by RV-2, since an Administrator who is a Direct Manager is allowed. Fix: cross-reference.
- L14. The PRD overlap and balance refusal codes are not specified. FR-14 and FR-15 say only "refused". SEC-11 rule 5 offers 400 or 409, so an implementer must choose. Fix: state 409 (or 400) explicitly.

## Topic notes (what was checked and found to agree)

1. Recovery and lockout: BRIEF D16, BML line 26, PML line 31, PRD FR-28, FR-29, SEC-5 and UJ-7 all agree. The agreed rules are an Administrator reset, operator recovery for the no-Administrator case, per-account-and-source throttling, lockout cleared on reset or recovery, and an administrator cannot reset their own password.
2. Temporary passwords by operation:
   - Creation (FR-2): server-generated, returned once to the creating Administrator, expires in 7 days.
   - Reset (FR-28): same rules as creation.
   - Bootstrap (FR-27) and recovery (FR-29): operator-supplied, not logged, expires in 7 days.
   - Only FR-1's creation response and FR-28's reset response ever return a password.
3. Balance: BRIEF D5, D7, D8 and D9, PML AP-2 and AP-8, and PRD FR-4, FR-11, FR-12, FR-13, FR-14, FR-19 and FR-20 agree on these rules:
   - Reserve on submit, deduct on approve (Total and Reserved both fall, Available is unchanged), release on reject, cancel or deactivate.
   - Whole days, Available = Total - Reserved, no negative balance.
   - Adjustments are deltas that cannot take Total below Reserved, with bounds of 1000 (a PRD-only indicative bound).
4. Approval: all three artifacts agree on these rules:
   - Only the current Direct Manager decides; a reason is required to reject.
   - Approval is refused (409) once the start date has passed, while reject and cancel stay allowed (BRIEF D8, RV-4, FR-19).
   - Top-most and manager-less employees cannot submit (D6 and AP-6, L-1, FR-14).
   - No self-approval (FR-19, FR-21).
   - An Administrator who is the Direct Manager may decide (D17, RV-2, FR-21, SEC-12); only the brief's summary lines (M4) omit this.
7. IDs: BRIEF D1..D18, PRD AP-1..9, Q1, Q2, Q4, Q6, Q7, Q8, RV-1..4 are all present in the PRD. Every PRD ID is in PML. D1..D15 are in BML only in summary (see M7 and L10).

Suggested fix priority: M1 to M3 (password model wording), M4 to M6 (brief summaries), M7 (mapping table), M8 (concurrency list).
