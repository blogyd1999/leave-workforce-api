# Adversarial review r4: Leave and Workforce Management API PRD

Scope: findings not already accepted in section 10.2 (L-1 to L-12). Accepted limitations are not repeated.

Verdict: sound, close to done. No critical findings. Two high findings are specification gaps, not design flaws, and each has a few-line fix. The rest are small.

Counts: critical 0, high 2, medium 6, low 6.

## Where the PRD is solid (no action)

- Response-code order (SEC-11) is internally coherent. Rules 4 over 5 and 400 before 409 are applied consistently in FR-16, FR-19, FR-20 and FR-21, including the lost-race rule.
- First-working-day rules (FR-14, FR-19, D10) are consistent: weekend-only ranges, start in the past with a later first working day, approve/reject/cancel asymmetry, and the time-zone re-evaluation in FR-32.
- Balance and overlap invariants hold. Reserve, release and approve all happen in one transaction, and FR-11, FR-12 and FR-14 are serialised on the same balance.
- Bootstrap (FR-27) cannot be re-run. The last-active-Administrator count never reaches zero, because deactivation and role removal both refuse the last one and self-actions are blocked.
- The API never returns a password through any listed path.
- The Single-Administrator Window cannot be reopened by deactivation or role removal, provided "for good" is implemented as a persisted flag (see M-1).

## HIGH

### H-1. Password state machine is incomplete and contradicts itself; Must-Change and expiry transitions have no trigger and can dead-end

Quotes:
- Glossary: "Awaiting Password — ... has no usable password and cannot log in until the Operator issues a Temporary Password."
- FR-7: "Wrong email, ... an Awaiting Password Account ... all return the same generic 401" and "An active Account with a usable password can log in".
- FR-2: "An Account can log in only after the Operator issues a Temporary Password."
- FR-30: "issue ... for an Account that is Awaiting Password"; and "issuing a new one invalidates the previous one."
- FR-2: "If the Must-Change session expires or is logged out before the password is changed, or the Temporary Password is not used within its 24 hours, the Account is returned to the Awaiting Password state."

Problems:
1. No state exists for "Temporary Password issued, not yet used". Read literally, FR-7 returns 401 for an Awaiting Password Account, which contradicts FR-2 login with the Temporary Password. FR-30's "invalidates the previous one" only makes sense if reissue is allowed while a Temporary Password is live.
2. "Is returned to Awaiting Password" has no actor. A Must-Change session that just expires (15-minute token, no refresh, no logout), or a lost response after the Temporary Password was consumed, leaves the Account in Must-Change. FR-30 allows issuance only for Awaiting Password, so the Operator cannot restore access. This breaks FR-2's own claim "No Account can be left in a state from which the Operator cannot restore access."
3. FR-28, FR-4 reactivation and FR-29 do not say what happens to a live Temporary Password or Must-Change session. FR-28 and FR-29 say "ends all its sessions", which covers sessions. Neither says the issued Temporary Password is voided.

Smallest fix, as a few lines in the Glossary and FR-2:
- Make Must-Change a property of the session, not the Account.
- The Account stays Awaiting Password until change-password succeeds.
- Define Awaiting Password as "no permanent password set"; login succeeds there only with an unexpired Temporary Password.
- FR-30 may issue for any Account that is Awaiting Password, including after a consumed or expired Temporary Password.
- Issue, reset and reactivation void any live Temporary Password.
- Delete the "returned to" sentence and replace it with "the Account remains Awaiting Password".

This removes the lazy-transition problem entirely.

### H-2. Privileged Request for "create" has no defined payload or target, and the post-window direct path has no defined response

Quotes:
- Glossary: "Privileged Request — a request to create an Account with the Administrator role or to grant the role".
- FR-31: "An Administrator creates it (naming the target and the action)".
- FR-1: "A request with the Administrator role flag is subject to FR-31."
- UJ-8: "grant a fourth Administrator directly ... refused (403)".
- FR-31 rule: "the target cannot be Initiator or Approver (403)"; "cannot create an Administrator for an Account that is deactivated (409)".

Problems:
- For a "create", no target Account exists yet. The PRD does not say where name, email and manager are held.
- It does not say whether the Account is created at request time (without the role) or at approval.
- It does not say when email uniqueness is checked.
- FR-1 and FR-5 with the Administrator flag after the window: is that call refused (403 per UJ-8) or does it create a Privileged Request?
- SEC-11 would make "window closed" a 409 state conflict, while UJ-8 says 403.
- An implementer who creates the Account at request time and promotes on approval is arguably compliant. That is the loophole SM-6 is supposed to close.

Smallest fix, as two sentences in FR-31:
- "After the window closes, FR-1 with the flag and FR-5 grant are refused with 403; the Administrator uses the Privileged Request operation instead. For a create, the request holds the Account details and no Account exists until approval; email uniqueness is checked at request and again at approval (409)."
- Add: a request targeting an existing Administrator is refused (409).

## MEDIUM

### M-1. "Exactly one Administrator Account has ever existed" has no stated source of truth

Quotes: Glossary "Single-Administrator Window — the period while exactly one Administrator Account has ever existed. It closes for good when a second Administrator exists." FR-31: "the sole Administrator may create or grant". FR-25 audit records are purged after the Security Retention Period.

Problem:
- Role removal erases history from the Account row. Audit Records are purgeable after three years. "Sole Administrator" reads as present-tense and could be implemented as "currently exactly one active Administrator". That would reopen the window after A deactivates or demotes B. Or it could be derived from audit data that retention later deletes.
- "Account" versus "role" is also blurred: an Account that later loses the role still counts as having been an Administrator.

Fix, one sentence: "The window is a persisted one-way flag set when the second Administrator Account is created or granted; it is never derived from current roles or Audit Records, and never reopens."

### M-2. Sole Administrator whose Direct Manager is E: E can never be reset or deactivated

Quotes: FR-28: "An Administrator cannot use this operation ... on their own Direct Manager (403)". FR-3: Administrator "cannot change or clear their own Direct Manager". FR-4: deactivating an Account with active Direct Reports is refused. FR-29 covers Administrators only. FR-30 covers Awaiting Password Accounts only.

Problem: while A is the only Administrator and A has Direct Manager E:
- E locked out cannot be reset: A is blocked, there is no other Administrator, and FR-29 does not cover ordinary Accounts.
- E cannot be deactivated, because A is E's active Direct Report and only A's own manager change would fix it, which A cannot do.
- Neither state is listed as accepted.

Fix, smallest: let FR-29 recovery name any Account, with the same reason and audit. Alternatively, add one note: "This condition is resolved by a second Administrator." The first costs one clause and is better.

### M-3. FR-29 optional reactivation bypasses FR-4's manager check and breaks an SM-2 invariant

Quotes: FR-29 "reactivates the Account only if the Operator explicitly asks". FR-4: reactivation "is refused (409) while the Account's Direct Manager is not active". SM-2: "every active Employee's Direct Manager is active or absent".

Problem: Operator reactivation of an Administrator whose Direct Manager is deactivated creates an active Employee with an inactive manager. That violates the SM-2 invariant, and FR-14 then refuses its leave requests through an otherwise dead branch.

Fix: "Reactivation under FR-29 applies the same preconditions as FR-4 reactivation."

### M-4. Unstated order among multiple 409 refusals in FR-14

Quotes: FR-14 lists, all as 409, a first working day in the past, weekend-only range, balance shortfall, overlap, and no Direct Manager or inactive Direct Manager. SEC-11 rule 7 groups them without internal order.

Problem: a request can hit several at once, for example no Direct Manager and a past date. A stable error code per case is required, but which one wins is untestable. Test authors will disagree.

Fix: "FR-14 evaluates 409 checks in the order listed: manager, first working day, weekend-only, overlap, balance."

### M-5. FR-28 and FR-30 against deactivated or already-Awaiting targets are unspecified

Quotes: FR-28 "an Employee who is not an Administrator". FR-30 "an Account that is Awaiting Password". FR-7 "An active Account...". FR-29 can issue for a deactivated Administrator without reactivation.

Problem: nothing says whether a deactivated Account may be reset or issued a Temporary Password. FR-29 without reactivation can issue a password that cannot be used, and FR-4 reactivation then voids it. Harmless but ambiguous and untestable.

Fix: "FR-28 and FR-30 refuse a deactivated Account (409); FR-29 without reactivation refuses a deactivated Account."

### M-6. Concurrency list in section 6 omits some races the PRD itself relies on

Quotes: FR-12 "An adjustment racing an approval or a submission is serialised" while section 6 lists "an adjustment versus an approval" only. Section 6 has no entry for change-password in Must-Change versus Operator reissue or Administrator reset. It has none for a Privileged Request creation versus the window closing, or for two cancel/reject decisions on one request.

Fix: add three phrases to the section 6 list: "submission versus adjustment; change-password in Must-Change versus reissue or reset; Privileged Request creation or execution versus the window closing; reject versus cancel." The section already says "at least", so this is clarification.

(M-4, M-5 and M-6 are medium or low; kept together here because each is a one-line fix.)

## LOW

- **L-A. Expiry has no actor.** FR-31 requires an audited "expired" event for Privileged Requests, and FR-2 requires a state return on expiry. Neither says whether expiry is evaluated lazily or by a sweep. Fix: "Expiry is evaluated lazily on next access and recorded then, actor 'system process'."
- **L-B. 429 before 401 for change-password and refresh (SEC-11 rule 1).** The throttle key is the Account, but an unauthenticated token cannot be keyed to one. Fix: "Rule 1 applies only once the caller is identified; otherwise rule 2."
- **L-C. FR-30 first bullet is labelled testable but is a procedure.** "The Operator verifies that the person asking is the Account's owner" cannot be automatically tested. Fix: move it to a Notes line and keep the deliverable (written procedure) as the testable item.
- **L-D. Dead branch in FR-14.** "An Employee whose Direct Manager is not active is refused" cannot occur, because deactivation with active Direct Reports is refused (FR-4) and reactivation requires an active manager. Harmless; may be kept as defence in depth, or add "(defensive)".
- **L-E. "Eligible Administrators" in the FR-31 Notes is undefined.** Fix: "eligible means active."
- **L-F. FR-11 "set once".** It is unclear whether setting 0 consumes the "once". Total returning to 0 after a delta pair re-enables it. This is harmless to the invariants. Fix: define "once" as "while Total is 0 and no request exists", which is already the test, and drop the word "once".

## Not findings (checked and clean)

- Window reopening by deactivation or role removal: closed, given M-1.
- A single Administrator getting a Temporary Password via the API: no path found. Administrator reset (FR-28) only moves a state, and the Operator is trusted.
- Leave requests violating balance, overlap or ownership: no path found.
- Last-Administrator rule: counts and serialisation hold.
- Reactivation returning an Account to Awaiting Password: consistent with FR-4, D16 and FR-30, subject to H-1.
