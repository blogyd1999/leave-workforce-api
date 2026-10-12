# Consistency review r3: brief, PRD and memlogs

Scope: brief-leave-workforce-api.md, prd-leave-workforce-api.md, and both .memlog.md files. No existing file was modified.
Abbreviations: PRD = prd-leave-workforce-api.md; BRIEF = brief-leave-workforce-api.md; PM = prd .memlog.md; BM = brief .memlog.md.

## Summary

Critical 0, High 1, Medium 7, Low 13.

| # | Sev | Title |
|---|---|---|
| C-1 | High | No path to a second Administrator after bootstrap, so FR-31 cannot be exercised |
| C-2 | Medium | Resetting an Administrator's password needs three Administrators, but every document says "two" |
| C-3 | Medium | Effects of an approved Administrator password reset are undefined |
| C-4 | Medium | RV-6 (PRD 10.1 and both memlogs) says an Administrator cannot reset "another Administrator", contradicting FR-31 |
| C-5 | Medium | Temporary Password state model has a hole (Awaiting Password vs issued, re-issue, expiry) |
| C-6 | Medium | Brief states unapproved Q-E1, Q-E2 and Q-E3 content as settled in Solution and Scope |
| C-7 | Medium | PRD draft gate: banner says Q-E, Q-C and Q-D keep it draft; 10.4 says only Q-E blocks finalization |
| C-8 | Medium | Brief decision table does not map cleanly to PRD 10.1 (AP-9, RV-1, Q6 missing; "D1 to D7 (kept)" vs "D1 to D18 retired") |

## Findings in detail

### C-1 (High): second Administrator cannot be created
- PRD FR-31: "creating an Account with the Administrator role, granting the Administrator role, and resetting an Administrator's password" all need "a different Administrator". "When no second eligible Administrator exists, the action cannot proceed through the API; the only sanctioned alternative is the Operator's recovery operation (FR-29)."
- PRD FR-27: bootstrap "succeeds only when no Administrator Account exists"; a repeat is refused.
- PRD FR-29 only recovers access to an existing Administrator Account. It does not create or promote one.
- Result: after UJ-6 there is exactly one Administrator and no sanctioned way to get a second. UJ-8 (Meera and Arun) and SM-1 (UJ-1 to UJ-8 end to end) cannot start from the UJ-6 state. The "recovery" alternative named in FR-31 does not solve creation or grant. BRIEF Solution and Success Criteria ("Creating an Administrator, granting the role ... needs a different Administrator") have the same gap.
- Fix: choose one. (a) Allow the Operator recovery or bootstrap operation to create or promote a second Administrator, with audit and a stated reason; or (b) allow bootstrap to create two Administrators; or (c) state that the sole-Administrator state is a known limitation. Then update FR-27, FR-29, FR-31, UJ-6/UJ-8 and the brief.

### C-2 (Medium): reset of an Administrator needs three people
- PRD FR-31: "The target Account of a Privileged Request can be neither its Initiator nor its Approver (403)", the Initiator cannot approve, and the Approver must be an active Administrator.
- For "resetting an Administrator's password" the target is an Administrator, so Initiator, Approver and target are three distinct Administrators.
- Conflicting wording of "two": PRD section 1 ("a second Administrator"), FR-31 title, RV-5 ("two Administrators unavailable"), US-19, SM-6 ("fewer than two distinct Administrators"), UJ-7 ("Administrator resets need two Administrators"); BRIEF Success Criteria ("two distinct Administrators").
- With exactly two Administrators, any reset of either one must go through the Operator (FR-29), which is not what UJ-7 and RV-5 imply.
- Fix: say "at least three Administrators are needed to reset an Administrator's password through the API; with fewer, use FR-29", or drop the target-as-Approver exclusion for resets. Fix SM-6 wording.

### C-3 (Medium): effects of a privileged reset are unspecified
- PRD FR-28 covers only "an Employee who is not an Administrator" and defines the effects (invalidate password, end sessions, revoke refresh tokens, clear throttle, Awaiting Password). FR-28 bullet 3 only says an Administrator reset "needs two Administrators (FR-31)".
- FR-31 never says what executing "resetting an Administrator's password" does. SEC-5 lists throttle clearing for FR-28, FR-29 and FR-30 but not the FR-31 reset. FR-25 lists "Administrator password reset (FR-28)" but the Privileged Request events do not name the reset effects.
- Fix: state in FR-31 that execution has exactly the FR-28 effects (Awaiting Password, sessions ended, throttle cleared, Operator issues the Temporary Password), and add the FR-31 reset to SEC-5 and SEC-6.

### C-4 (Medium): RV-6 wording contradicts FR-31
- PRD 10.1 RV-6: "Privileged resets are restricted: an Administrator cannot reset themselves, another Administrator, or the restricted target in Q-E2."
- BM line 33 and PM line 42: "Administrator cannot use privileged reset on self, another Administrator or restricted target (interpretation pending Q-E2)".
- PRD FR-28 and FR-31 and UJ-8: an Administrator may initiate a two-person reset of another Administrator. FR-28 limits only the ordinary reset operation (Q-E2).
- A reader of 10.1 would conclude Administrator-on-Administrator reset is forbidden outright. This is not marked superseded by the SUPERSEDES line (BM 37, PM 46). Treat as defect because RV-6 is an approved row.
- Fix: reword RV-6 to "the ordinary reset (FR-28) is restricted: not on themselves, any Administrator, or the restricted target in Q-E2; resets of Administrators go through FR-31". Append a clarifying (change) line to both memlogs.

### C-5 (Medium): Temporary Password lifecycle state hole
- Glossary: Awaiting Password = "no usable password and cannot log in until the Operator issues a Temporary Password". FR-7 returns generic 401 for "an Awaiting Password Account". FR-30: issue "for an Account that is Awaiting Password"; "issuing a new one invalidates the previous one"; Must-Change State only "at first use".
- Unspecified: what state the Account is in between issuance and first login, whether it still counts as Awaiting Password (then FR-7 would reject the Temporary Password), and whether the Operator can re-issue after the 24-hour expiry or a lost password (precondition says Awaiting Password; "issuing a new one" implies yes). UJ-3 and UJ-6 say "within 24 hours" but not what happens at hour 25. For the bootstrap Administrator this matters because FR-27 refuses a rerun once the Account exists.
- Fix: define an explicit state for "Temporary Password issued"; allow FR-30 for Awaiting Password or expired-Temporary-Password Accounts; note in UJ-6 that an expired bootstrap Temporary Password is recovered with FR-29.

### C-6 (Medium): brief presents unapproved items as settled
- BRIEF Solution: "Creating an Administrator, granting the role, or resetting an Administrator's password needs a different Administrator" and "An operator issues short-lived temporary passwords outside the API". BRIEF Scope: "Two-person approval for creating an Administrator, granting the role and resetting ..."; "restricted administrator password resets for ordinary accounts"; "operator-issued temporary passwords". The brief's own approved table (RV-5) says only "creating or resetting". The role grant (Q-E3), operator-only issuance (Q-E1) and restricted reset targets (Q-E2) are unapproved.
- The brief does list them under "Awaiting Your Approval", but the body carries no inline marker, so the Solution and Scope read as agreed.
- PRD side is better but not fully tagged: FR-5, FR-31 and 10.1 RV-5 omit the role grant from the approved row; only the 24-hour expiry bullet carries `[ASSUMPTION: Q-E3]`. UJ-3, UJ-6, FR-30, SEC-22 and Glossary "default 24 hours" carry no Q-E5 tag (the index in section 11 does).
- Fix: add "(proposed, Q-E1)", "(proposed, Q-E3)" and "(proposed, Q-E2)" tags in the brief Solution and Scope. Tag FR-5, FR-31 and Glossary in the PRD.

### C-7 (Medium): what keeps the PRD in draft
- PRD banner (line 11): "Open owner approvals are listed in 10.4 (Q-E, Q-C, Q-D). The PRD stays in draft until they are recorded."
- PRD 10.4 closing line: "None of the Q-A to Q-D items blocks architecture, but Q-E blocks finalization." The Q-E heading also says "(these block finalization)".
- PM line 48: "Status draft pending Q-E, Q-C, Q-D approvals". BRIEF: Q-C items are "Low impact and non-blocking".
- Fix: pick one rule. Either Q-C and Q-D block finalization (fix 10.4 and the brief), or only Q-E does (fix banner and PM line 48).

### C-8 (Medium): brief decision table vs PRD 10.1
- Missing from the brief table: AP-9 (increment order), RV-1 (Administrator reset and recovery, FR-28/FR-29), and the name "Q6" (the AP-8 text is folded into the "D1 to D7" row). The table's last row lists "Q1, Q2, Q4, Q7, Q8" (no Q6).
- Internal contradiction: the intro says "the brief's earlier D1 to D18 numbering is retired" while the first row is "Brief D1 to D7 (kept)". The first row also mixes ID references: "(AP-6, AP-7, AP-8)" next to a D-range.
- Everything else maps: AP-1..AP-8, RV-2..RV-9, Q1, Q2, Q4, Q7, Q8 match 10.1 in meaning.
- Fix: add rows for AP-9, RV-1 and Q6 (or state "Q6 resolved by AP-8"); replace the D-range row with AP/RV references and drop the "kept" label or the "retired" sentence.

## Low-severity findings

- L-a (brief Solution): "Managers see only their direct reports' requests" omits the former-manager-as-Decider read (FR-21, FR-23). Fix: add "and requests they decided".
- L-b (brief Solution): "rejection or cancellation releases it" omits release on deactivation and on manager clearing (FR-3 clearing is Q-E4, FR-4 deactivation is AP-2). Brief table AP-2 says "cancels pending requests" without the release. Fix: mention release.
- L-c (brief Known Limitations): "Every new account, ordinary password reset and recovery needs an operator" omits reactivation (FR-4 returns the Account to Awaiting Password) and the Administrator reset via FR-31. Matches L-6 only partly.
- L-d (PRD UJ-7): edge case says "Administrator resets need two Administrators (UJ-8)" but UJ-8 shows role grant, not a reset. Fix: reference FR-31 or add a reset journey.
- L-e (PRD 10.1 RV-5 "Where applied"): lists FR-31, FR-29, SEC-12, UJ-8 but not FR-1, FR-5 and FR-28, which also implement it.
- L-f (PRD 10.1 RV-6 vs Q-E2): an approved row references an unapproved interpretation. Disclosed, so acceptable, but flag for the owner.
- L-g (PRD section 8.3 and 10.1 AP-9): refer to "Q-D1" while the 10.4 table labels it "D1". Fix: label rows Q-D1..Q-D16.
- L-h (PRD FR-2): the session in Must-Change State ends at Temporary Password expiry, but the response code when expiry hits mid-session is not stated (expected 401 per SEC-11 rule 2).
- L-i (PRD FR-5): removal of the Administrator role is a single-Administrator action, so one Administrator can demote another unilaterally. L-9 collusion text does not mention it. Fix: note it or require two-person for removal.
- L-j (PRD NFR concurrency list): does not name approval versus expiry of a Privileged Request, Privileged approval versus Initiator or target demotion or deactivation (covered only inside FR-31), or approve versus reject. The list says "at least", so low. Loser codes are otherwise consistent: 409 where still entitled (FR-11, FR-12, FR-19, FR-31, SEC-11).
- L-k (PRD FR-25 vs Glossary): the classification of leave approval (changes Total) as Ordinary or balance Security-Class is ambiguous. L-11 says an old balance may lack an explaining Ordinary Event.
- L-l (PRD 4.8): FR-5 is not mapped to any user story.
- L-m (BM/PM stale phrases, none explicitly superseded but overtaken): BM line 18 "login throttling/lockout"; PM line 29 "5 failures/15 min lockout" (now 5 then 1 minute doubling to 15, Q-E7); PM and BM RV-1 "lockout per account-and-source" (now per-account independent of source, RV-8/SEC-5); BM line 20/21 manager's-manager approver and "admin as account/balance manager only" (superseded by the Q1 and RV-2 lines that follow); PM line 22 "configurable retention" (now Operator config only, RV-9). Fine as history, but a reader could be misled by the RV-1 "per account-and-source" line. Suggest a clarifying (change) line.

## Topic verdicts

1. Account recovery and lockout: INCONSISTENT. FR-28, FR-29, SEC-5, RV-1, RV-8 agree on the ordinary reset, operator recovery, throttle clearing and back-off numbers (5, 1 min, 15 min). Gaps: C-2, C-3, C-4; stale RV-1 memlog phrase (L-m).
2. Temporary passwords: INCONSISTENT (mostly consistent). Operator-issued, never in API responses, 24 h, one login, 128 bits, Awaiting Password and bootstrap agree across FR-2, FR-27, FR-30, SEC-22, UJ-6 and RV-6/RV-8. Gaps: C-5, and 24 h and operator-only untagged in places (C-6).
3. Reservations and balance: CONSISTENT. Reserve, deduct, release on reject, cancel, deactivation and clearing, whole days, delta adjustments and bounds (1000) agree across PRD sections. The brief is only less complete (L-b).
4. Approval rules: CONSISTENT. The Direct Manager decides, an Administrator who is Direct Manager decides as Manager, First Working Day rule (RV-4, FR-14, FR-19), rejection reason, and top-most cannot submit (L-1) all match; the stale "start date" memlog line is explicitly superseded (superseded, fine).
5. Concurrency: CONSISTENT. Races named in section 6 and FRs; losers 409 when still entitled, otherwise 403 or 404 under SEC-11; minor omissions only (L-j).
6. Separation of duties and two-person approval: INCONSISTENT. The self-dealing list (RV-2) maps to FR-1, FR-3, FR-4, FR-5, FR-11, FR-12 and SEC-12 correctly. Two-person approval has C-1 (no second Administrator), C-2 (three needed), C-6 (brief scope of grant) and L-i.
7. Manager relationships: CONSISTENT. Clearing (reason, cancels Pending, Q-E4), reassignment moving Pending requests, deactivation refused with active Direct Reports, reactivation refused while the manager is inactive, and L-1 agree in FR-3, FR-4, FR-14 and RV-7. The brief omits the Pending-request effects only (L-b).
8. Retention and audit protection: CONSISTENT. One-year ordinary and three-year security defaults (Q-E6, unapproved), Operator configuration (FR-32), DB role without update or delete (SEC-23), tamper evidence as an architecture requirement, and the purge summary agree. The brief is a summary. The classification gap in L-k is minor.
9. Decision IDs and status: INCONSISTENT. See C-8 (brief table) and C-7. Positives: PRD 10.1 holds AP-1..9, RV-1..9, Q1, Q2, Q4, Q6, Q7, Q8; both memlogs record RV-5..RV-9 and Q-E1..Q-E8 (BM 32 to 36 and 38; PM 41 to 45 and 47). Q-C and Q-D are in PM (lines 29, 36) but only implicitly in BM (line 25 covers the Q-C defaults, nothing for Q-D or Q-A/Q-B); Q-A and Q-B appear in the brief Open Questions. The unapproved items are shown as NOT approved in the PRD (banner, 10.4), the brief ("Awaiting Your Approval") and the memlogs ("UNAPPROVED"), except the inline presentation in C-6.
10. Frontmatter status vs memlog: CONSISTENT. Both files say `status: draft`. BM line 31 and PM line 39 record the revert from final to draft; the earlier "finalized" lines (BM 30, PM 38) are superseded by those later lines. Minor: BM has no event stating that the brief was rewritten after RV-5..RV-9, though its content reflects them.

## Stale or misleading text in the brief
- Decisions table intro vs first row: "D1 to D18 numbering is retired" vs "Brief D1 to D7 (kept)" (C-8).
- Solution and Scope present the role grant, operator-only issuance and restricted resets as settled (C-6).
- Success Criteria "two distinct Administrators" is inaccurate for resetting an Administrator (C-2) and unreachable after bootstrap (C-1).
- Solution "Managers see only their direct reports' requests" is incomplete (L-a); "rejection or cancellation releases it" is incomplete (L-b).
- Known Limitations list of operator-dependent actions is incomplete (L-c).
- "Recommended Defaults Awaiting Confirmation: Low impact and non-blocking" conflicts with the PRD banner, which gates draft on Q-C (C-7).
- Decisions table row RV-6 "restricted administrator resets" inherits the ambiguity in C-4.
