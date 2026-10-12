# PRD Re-check Review: Leave and Workforce Management API

Scope: revised PRD (draft, 2026-10-12) re-validated against `review-rubric.md` and `review-adversarial-security.md`, and checked for defects introduced by the revisions (RV-1 to RV-4).

## Blocking verdict

No critical issue remains. **One high issue remains (N-1: the RV-2 self-dealing guards are incomplete) and should be fixed before the PRD is finalised and handed to architecture.** The fix is two one-line consequences in FR-1 and FR-3, so this is a short patch, not a rework. The mediums (N-2 to N-9) are consistency fixes. They do not block architecture, but N-2, N-3, N-4 and N-5 should be tidied in the same pass because they change what architecture must build for password handling and audit.

Every prior critical or high finding is resolved or partly resolved (section 1). The SEC-11 ordered rules are internally consistent and the listed FRs (3, 4, 5, 6, 11, 12, 16, 17, 19, 20, 21, 24, 28, 29) agree with them (section 2.1).

---

## 1. Prior critical and high findings

| Prior ID | Finding | Status | Justification (PRD quote) |
|---|---|---|---|
| Adv C-1 | Sole Administrator lockout, no recovery | Resolved | FR-28 "An Administrator can reset another Account's password"; FR-29 "operator operation that resets the password of a named Administrator Account when no Administrator can log in"; SEC-5 "throttled per account-and-source pair". A residual existence-oracle point on 429 is raised as N-9 (medium). |
| Adv H-1 | Stale Access Token trusted for state and roles | Resolved | SEC-13 "re-reads the Account's active state, Administrator role and must-change flag ... The Access Token proves identity only"; FR-4 "rejected with 401 on its next use"; "Refresh also re-checks active state". |
| Adv H-2 | Absolute versus relative balance adjustment | Resolved | FR-12 "a signed whole-number change (a delta) applied to the current Total inside the same serialised transaction, never an absolute overwrite". |
| Adv H-3 | Administrator separation-of-duties bypass | **Partly** | SEC-12 and FR-3/4/5/11/12 add the guards, but FR-1 lets an Administrator create an Account with themselves as Direct Manager, and nothing stops an Administrator changing their own Direct Manager (N-1). |
| Adv H-4 | Concurrency matrix too narrow | Resolved | §6 "every state-changing operation re-validates its preconditions inside the same serialised transaction ... approve versus cancel, approve versus manager reassignment, submit versus deactivation ... The loser receives 409 with a stable error code." |
| Adv H-5 | Dangling and orphaned Direct Manager links | Resolved | FR-14 "An Employee whose Direct Manager is not active is refused"; FR-4 "refused while the Account's Direct Manager is not active"; FR-3 "There is no operation to clear a Direct Manager". |
| Adv H-6 | Response-code policy contradictions | Resolved | SEC-11 five ordered rules ending "Where rules 3 and 4 could both apply, 404 wins"; FR-16 now says "403 if they are a Request Reader ..., otherwise 404". |
| Rubric high | FR-21 contradicts SEC-11 tie-break | Resolved | FR-21 "A caller who is not a Request Reader receives 404, whether or not they are a Manager"; the "no Direct Reports, 403" rule is gone. |
| Rubric high | No credential reset path, not a non-goal | Resolved | FR-28, FR-29, US-18, UJ-7, §8.3 increments 1 and 2. |

Counts: **8 resolved, 1 partly, 0 not resolved** (9 items; Adv C-1 and the rubric credential finding overlap in substance but are counted separately).

---

## 2. New findings

### 2.1 SEC-11 versus FR consistency (checked, no contradiction found)

Walked FR-3, 4, 5, 6, 11, 12, 16, 17, 19, 20, 21, 24, 28, 29 against SEC-11.

- Administrator-only operations return 403 (rule 2) before any 404: FR-3 self-assign, FR-4 self-deactivate, FR-5 own role, FR-11/12 own balance, FR-28 self-reset. All are consistent.
- Request-scoped operations (FR-16, 17, 19, 20, 21, 24) use Request Reader for rules 3 and 4. All are consistent, including the owner deciding their own request (403 because the owner is a reader) and a former Direct Manager (403).
- The mixed cases resolve correctly by ordering. A Request Reader who is not the Direct Manager gets 403 even on a non-Pending request (rule 4 beats the 409 in rule 5). The current Direct Manager gets 409 on a non-Pending request.
- Residual gaps are low-severity (see L-1, L-2, L-3 below).

### 2.2 High

**N-1. RV-2 self-dealing guards are incomplete (FR-1, FR-3, SEC-12).**
Quote: FR-1 "an optional Direct Manager" (no restriction). FR-3 "An Administrator cannot assign themselves as another Employee's Direct Manager (403)". SEC-12 "cannot assign themselves as another Employee's Direct Manager, change their own roles, deactivate themselves, or set or adjust their own balance".
Problems:
1. **Creation bypass.** The FR-3 guard is not on FR-1. An Administrator can create an Account with themselves as Direct Manager. FR-2 returns the initial password to that Administrator, FR-11 lets them set that new Account's balance (it is not "their own"), and they can then decide its requests as Direct Manager. That is a ghost Employee with a self-approved balance, and it defeats the intent of RV-2.
2. **Own manager change.** Nothing stops an Administrator reassigning their own Direct Manager (FR-3 guards only "another Employee"). They can pick a colluding approver for their own leave. The original adversarial fix (H-3) asked for "cannot change their own roles or manager", and only the roles part was applied.
3. FR-1 does not state that the FR-3 manager rules (active, no cycle) apply at creation.

Fix: add to FR-1 "the creating Administrator cannot be named as the new Account's Direct Manager (403), and the manager rules of FR-3 apply at creation". Add to FR-3 "An Administrator cannot change their own Direct Manager (403)". Add both to SEC-12 and to the RV-2 "Where applied" list (FR-1). Optionally state that two colluding Administrators can still defeat this and accept it as a limitation (N-7).

### 2.3 Medium

**N-2. FR-1 "only password returned" contradicts FR-28 and SEC-12.**
Quote: FR-1 "The only password ever returned by the API is the one-time initial password in the creation response (FR-2)". FR-28 "returned once to the Administrator". SEC-12 "No role can read passwords or tokens."
FR-28 returns a second one-time password. SEC-12 literally forbids the Administrator from reading what FR-2 and FR-28 hand them. The creation and reset responses also need to be non-cacheable and excluded from response logging; SEC-21 says only that logs avoid "tokens or passwords".
Fix: FR-1: "The only passwords ever returned are the one-time passwords in the creation (FR-2) and reset (FR-28) responses". SEC-12: "No role can read stored passwords or tokens; the one-time password is shown once to the issuing Administrator". Add "never cached or logged" to FR-2 and FR-28.

**N-3. RV-1 says passwords are server-generated, but FR-27 and FR-29 allow supplied passwords; policy and expiry are unspecified for them.**
Quote: RV-1 "initial and reset passwords are server-generated, single-use and expire in 7 days". FR-27 "The initial password is supplied at invocation". FR-29 "supplied or issued". UJ-6 "supplies the initial password".
Problems: (a) the §10.1 decision text does not match FR-27, FR-29 and UJ-6; (b) FR-27 and FR-29 do not say a supplied password must meet SEC-2 or must differ after the change; (c) they do not say whether the 7-day expiry applies. FR-27 only refers to FR-2 for "must change", and FR-29 says "must-change state" with no expiry. A bootstrap Administrator whose password expires after a deployment gap is recoverable only through FR-29.
Fix: reword RV-1 ("server-generated for Administrator-issued passwords; operator-supplied or issued for bootstrap and recovery"), apply SEC-2 and the 7-day expiry to FR-27 and FR-29 explicitly, or state that they are exempt.

**N-4. "Single-use" initial password is ambiguous and conflicts with the must-change flow.**
Quote: FR-2 "single-use initial password that must be changed at first login ... Until the password is changed, the Account can call only the change-password and logout operations".
If single-use means consumed on first login, the user whose first session ends (Access Token expiry, closing the app) before changing the password cannot log in again. If it means valid until changed or expired, it is not single-use. FR-10 "Success revokes all Refresh Tokens" also means the user must log in again after the forced change (UJ-3 and UJ-6 read as a single session).
Fix: define "single-use" as "valid for login until the first successful password change or 7-day expiry, then void". Note in UJ-3, UJ-6 and UJ-7 that a fresh login follows the change.

**N-5. FR-25 event list omits events that FR-28, FR-29 and the retention process require.**
Quote: FR-25 list ends "...password change; balance set or adjusted; ...; and changes to the Company Time Zone or Audit Retention Period". FR-28 "The reset is audited". FR-29 "Each use is audited, including refused attempts".
Password reset (FR-28), recovery attempts (FR-29), initial-password expiry and the retention purge summary (stated only in a consequence) are not in the list. SM-4 is defined as "Every event listed in FR-25", so those events would not be tested. FR-25 also does not say how the operator actor is identified (FR-29 "operator marked as the actor").
Fix: add "Administrator password reset; operator recovery attempts; retention purge" to FR-25, and say what identifies the operator (for example the OS user or a supplied label).

**N-6. §8.1 In Scope omits FR-28 and FR-29; FR-27 to FR-29 placement is only partly signposted.**
Quote: §8.1 "authentication and sessions (FR-7 to FR-10)"; §4.8 "FR-27 is numbered out of order but belongs to §4.1; ranges such as 'FR-1 to FR-6' do not include it"; FR-29 Notes "FR-28 and FR-29 are numbered after FR-27 and belong to §4.2".
FR-28 and FR-29 are in §8.3 but not in the §8.1 scope list. The §4.8 note mentions FR-27 only, so a range such as "FR-7 to FR-10" also silently excludes FR-28 and FR-29. The physical order is FR-1 to FR-6, FR-27, FR-7 to FR-10, FR-28, FR-29, FR-11, which is awkward for anyone extracting by numeric range. The §4.2 description still says "Realizes UJ-1, UJ-2, UJ-3" and omits UJ-7. The placement itself (FR-27 in 4.1, FR-28 and FR-29 in 4.2) is correct by topic. FR-29 in §4.2 while §8.3 puts it in increment 1 with the Bootstrap Operation is fine.
Fix: §8.1 "authentication and sessions (FR-7 to FR-10, FR-28, FR-29)". Extend the §4.8 note to "FR-27 (§4.1) and FR-28, FR-29 (§4.2) are numbered out of order; ranges do not include them". Add UJ-7 to the §4.2 description.

**N-7. Residual self-dealing and impersonation risks are not acknowledged as limitations.**
Quote: FR-28 "returned once to the Administrator"; SEC-12 "An Administrator who is legitimately the Direct Manager of an Employee may decide that Employee's requests".
Residuals that the owner should consciously accept or mitigate:
1. An Administrator who resets a Manager's password (FR-28), or creates an Account (FR-1), knows the one-time password. Until the owner changes it, the Administrator can log in as that person. If that person is a Direct Manager, the Administrator can then decide leave, which sidesteps AP-4 and RV-2. The audit trail attributes the actions to the victim, though the victim's subsequent lockout would expose it.
2. Two colluding Administrators (A assigns B as manager of A's accomplice, or each sets the other's balance) defeat the "cannot do it to yourself" guards.
3. An Administrator who is the Direct Manager can also adjust that Employee's balance and decide their requests. SEC-12 accepts this without naming the trade-off.
Fix: add a limitation (L-9) naming these, and add an audit event that distinguishes "first login after Administrator reset" so the trail can be reviewed. L-6 covers only delivery of the password.

**N-8. Assumptions Index roundtrip has gaps.**
Quote: index "§4.3 FR-11, FR-12: ... balance bounds are indicative (1000)"; "Review-driven defaults (Q-D, §10.4): working-day overlap and the indicative numeric values in SEC-4 and SEC-5."
Checked: every inline `[ASSUMPTION]` tag is indexed (Manager, FR-2 both tags, FR-3, FR-11 both tags, FR-14, FR-17, FR-25, SEC-2, §6 both tags). The reverse fails in these places:
- FR-12 is indexed but has no inline tag for the 1000 bounds (only FR-11 does).
- The SEC-4 and SEC-5 indicative values and the FR-15 working-day overlap are indexed but only "Indicative defaults" prose appears inline, with no `[ASSUMPTION]` tag.
- The "§10.3 Q3, Q5, Q9, Q10" and "Brief A1 to A4" entries cannot be verified inside the PRD (acceptable, but unchanged from the prior review).
- The 7-day expiry is tagged only in FR-2 and is repeated untagged in FR-28.
Fix: tag the inline spots, or drop those lines from the index.

**N-9. Throttling can still be an existence oracle, and the distributed-guessing trade-off is unnamed.**
Quote: SEC-5 "throttled per account-and-source pair ... The response and its timing do not reveal whether the email exists (unknown emails go through a dummy password verification)."
Only the verification is made equal for unknown emails. SEC-5 does not say that unknown emails also hit the 5-in-15 threshold and get 429. If they do not, the 429 itself reveals that the email exists. Per-pair throttling also leaves an attacker with many sources free to guess one account indefinitely, and users behind one shared address can lock each other out. Both are reasonable trade-offs but are not named in §10.2.
Fix: "unknown emails are throttled by the same key and return the same 429". Add a limitation or note about distributed guessing, with a reference to SEC-2 as the mitigation.

**N-10. Glossary drift: Access Token definition contradicts SEC-13.**
Quote: Glossary "Access Token — a short-lived JWT proving who the caller is and their roles." SEC-13 "The Access Token proves identity only; the role it carries is never trusted for authorization."
Fix: "proving who the caller is (any role claim is informational only, SEC-13)".

**N-11. Decision tracking inconsistencies in §10.1 and §10.4.**
- AP-9 sits in the approved table but says "shown in §8.3 for your confirmation", and §10.4 Q-D does not list the §8.3 corrections as awaiting confirmation. §8.3 says "two dependency corrections for review".
- RV-3 says "Replaces the former limitation L-2", but L-2 now exists with a different meaning (per-call lookup). RV-4 says "Amends decision D8", and D8 appears nowhere in the PRD.
- RV-1 "Where applied" (FR-2, FR-28, FR-29, SEC-5, UJ-7) omits FR-27 and FR-1, which also issue passwords. RV-3 omits FR-2 (must-change re-read), FR-5 (demotion effect on the next call) and FR-8 (refresh re-check). RV-2 omits FR-1 (see N-1).
- Q-D is written in the first person ("the review-driven defaults I applied") and still lists "the numeric bounds in RV-4", which is already approved.
Fix: move AP-9 to §10.4 or mark it confirmed, rename the dangling references ("the earlier stale-token limitation", "the earlier request-bounds decision"), and complete the "Where applied" lists.

### 2.4 Low

- **L-1. Several refusals have no status code.** FR-14 and FR-15 refusals (no manager, insufficient balance, overlap), FR-16 refusing a non-Pending request, FR-2 expired initial password ("login is refused"), and "An Employee cannot be their own Direct Manager" (FR-3) have no code. SEC-11 rule 5 implies 400 or 409. The login case matters because a distinct code for "correct but expired password" would reveal the account. *Fix:* state 409 for state conflicts and the generic 401 for expired initial passwords.
- **L-2. SEC-11 rule 1 does not list a deactivated Account's valid token** (FR-4 and SEC-13 say 401). Also the order of 400 versus 409 within rule 5 is unspecified (for example blank reason on a non-Pending request). *Fix:* add "or an Account deactivated since issue" to rule 1 and state 409 before 400, or the reverse.
- **L-3. FR-19 has two different 409s** (not Pending, and start date passed) that need distinct stable error codes. *Fix:* say so.
- **L-4. FR-11 "set once" is moot.** FR-12 can raise Total from 0 at any time and "once" is not defined if the first value was 0. *Fix:* make FR-11 an alias of the first FR-12 delta, or define how "once" is detected, and cover set-versus-adjust in §6.
- **L-5. FR-6 and own-Account read remain unclear** (the prior low finding is unaddressed). The Manager's view of Direct Reports versus the Administrator-only list (403) is not named as a separate operation, and an Employee cannot read their own Account, role, must-change state or Direct Manager. *Fix:* add a "view own Account" consequence and name the Manager's report lookup.
- **L-6. FR-13 wording.** "Available days, which already exclude that request's Reservation" is only true for Pending requests; for decided requests the Manager sees the current Available, which also reflects the Employee's other requests. *Fix:* "current Available".
- **L-7. Glossary gaps.** "Operator" (US-15, FR-27 to FR-29), "must-change state", "one-time/initial password" and "recovery operation" are used as terms but are not defined. Lowercase "manager" remains in FR-3 ("The manager must be an active Account"). "Any user" in US-5 and US-14 is still not a glossary actor.
- **L-8. Metrics coverage.** SM-1 lists FR-4 (no journey deactivates anyone) and omits FR-2 and FR-10, which UJ-3, UJ-6 and UJ-7 exercise. SM-2 validates only FR-14, FR-15 and FR-19 though §6 lists races across FR-3, FR-4, FR-5, FR-12 and FR-16. SM-5 covers no-password-leak for FR-27 but not FR-28 or FR-29. *Fix:* adjust the "Validates" lists.
- **L-9. UJ-6 entry state versus FR-27.** UJ-6 says "no Accounts exist"; FR-27 tests "no Administrator Account exists". Align on FR-27's wording, and say whether a deactivated Administrator counts as existing.
- **L-10. Request Reader and Decider are consistent but a few operations are implied.** No FR defines a get-single-request operation for the Direct Manager or the Decider (UJ-2 "opens one"; FR-18 and FR-23 are lists). *Fix:* one line in FR-17 or FR-18.
- **L-11. FR-5 and FR-29 minor.** FR-5 does not say whether the Account receiving the Administrator role must be active. FR-29 reactivation bypasses the FR-4 "Direct Manager must be active" rule without saying so.
- **L-12. FR-28 on a deactivated target** is not specified (allowed or refused).

---

## 3. Checks that came out clean

- **Balance arithmetic.** UJ-1 (12 to Reserved 5 and Available 7 to Total 7, Reserved 0) agrees with FR-14 and FR-19. FR-12 (Total at least Reserved), FR-11 and FR-14 bounds (Total at most 1000, span at most 366 days) are mutually consistent. Requested Days cannot exceed about 262, below the 1000 cap.
- **Request Reader and Decider terms.** Used consistently in SEC-11, FR-16, FR-17, FR-21, FR-23 and FR-24. The Decider is a Reader, so a former Direct Manager gets 403 (not 404) on a decide attempt. A Cancelled request has no Decider, which is correct.
- **Administrator-as-Direct-Manager.** Glossary, SEC-12, FR-21 and FR-24 agree: the role confers no right, but the Direct Manager relationship does. The only open points are N-1 and N-7.
- **US to FR coverage.** All of FR-1 to FR-29 now map to a story (FR-24 to US-16, FR-25 to US-17, FR-28 and FR-29 to US-18).
- **ID continuity.** FR-1 to FR-29, UJ-1 to UJ-7, SEC-1 to SEC-21, SM-1 to SM-7 plus SM-C1 and SM-C2, US-1 to US-18, L-1 to L-8 and AP-1 to AP-9 plus RV-1 to RV-4 are contiguous and unique. All FR, UJ, SEC and L cross-references resolve.
- **§10.1 "Where applied".** Accurate for AP-1 to AP-9, Q1, Q2, Q4, Q6 to Q8 and RV-4. RV-1, RV-2 and RV-3 are accurate but incomplete (N-11).
