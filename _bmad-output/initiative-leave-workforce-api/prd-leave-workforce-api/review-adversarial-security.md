# Adversarial Review: Leave and Workforce Management API PRD

Reviewed: `prd-leave-workforce-api.md` (draft, 2026-10-12). Scope: security, business-rule loopholes, contradictions, testability.

## Verdict

Solid foundations, not yet safe to hand to architecture unchanged. The relationship-based 404 concealment, atomic submit and reserve, refresh-token rotation with family revocation, mass-assignment ban and the Bootstrap Operation are well specified. The serious problems are: (1) a self-inflicted system lockout path with no credential recovery, (2) trust in stale Access Tokens for account state and roles, (3) a lost-update hole on balance adjustment, (4) an Administrator separation-of-duties bypass plus a glossary/FR contradiction, (5) a concurrency matrix that covers only two of the many racing operations, and (6) response-code rules that contradict each other (so SM-3 cannot pass as written).

Counts: Critical 1, High 6, Medium 11, Low 7. Total 25.

---

## Critical

### C-1. Anonymous attacker can permanently lock out the only Administrator; no recovery path exists
- Cites: SEC-5 "throttled per account and per source, with temporary lockout after repeated failures"; FR-27 "succeeds only when no Administrator Account exists"; SEC-12 "cannot read credentials"; no password-reset or unlock FR anywhere; FR-2 only covers an initial credential.
- Problem: (a) Per-account lockout lets anyone who knows an email lock that account out repeatedly, including the sole Administrator (the bootstrap email is guessable). Temporary lockout re-triggers indefinitely. (b) There is no forgot-password flow and no Administrator "reset credential" or "unlock" operation, so any Employee who forgets a password is permanently stuck. If it is the last Administrator, nobody can provision, reset or unlock anything, and the Bootstrap Operation correctly refuses because an Administrator exists. (c) A locked account's response may differ from the generic 401, which would also reveal that the email exists (contradicting SEC-5's non-disclosure).
- Fix: Add FR "Administrator resets credential": issues a new one-time credential outside the API, sets must-change, revokes all Refresh Tokens, audited, and clears lockout. Add an operator-run "recover Administrator" path (same non-logged-channel rules as FR-27) for the case where no Administrator can log in. Specify that lockout is progressive per (account, source) pair rather than a hard per-account lock, returns the identical generic 401 (or a uniform 429 for all identifiers), and applies to unknown emails too.

---

## High

### H-1. Access Token is trusted for account state and roles; L-2 is an unnecessary weakness
- Cites: FR-4 "Its existing Access Token stops being accepted no later than the access-token lifetime"; L-2; SEC-3 "carry identity and role"; SEC-13 reads only the manager relationship from current data; FR-5 role removal has no revocation text.
- Problem: For up to about 15 minutes after deactivation, a deactivated Employee can still call submit. Concrete race: the FR-4 transaction cancels Pending requests and releases Reservations, then the still-valid token submits a new request, which creates a Pending Reservation on a deactivated Account that nothing will ever clean up. A demoted Administrator keeps full Administrator powers (including balance adjustment and role grants, so they can re-grant themselves the role) for the token lifetime. A must-change account's restriction (FR-2) may also be a claim that survives refresh.
- Fix: Extend SEC-13: on every authenticated call the server re-reads Account active state, Administrator role and must-change flag (or compares a per-account security stamp / token version bumped on deactivation, role change, password change). Drop L-2 or limit it to "stale display claims only". State that Refresh also re-reads roles and active state and refuses a deactivated Account. FR-14 must refuse when the caller is deactivated.

### H-2. Balance adjustment lost-update: absolute versus relative is unspecified
- Cites: FR-12 "adjust Total up or down"; FR-11 "set"; FR-19 "Total decreases by Requested Days"; Glossary Total "reduced when leave is approved".
- Problem: Total is both the Administrator-managed entitlement and the running remainder. If the Administrator reads Total=12, a manager approves 5 days (Total=7), and the Administrator then submits "set Total to 10 (+/-)" computed from the stale 12, an absolute write erases the approved leave. Even the check "not below Reserved" can pass on stale data. Also there is no record of "used" days, so an Administrator cannot reason about entitlement versus consumption.
- Fix: Specify that adjustments are signed deltas applied inside the same serialised transaction (check Total+delta >= Reserved at commit), or require an expected-version/ETag on the balance and return 409 on mismatch. Consider exposing Used (cumulative approved) as a read-only figure so Total semantics are explainable.

### H-3. Administrator separation-of-duties bypass; Glossary contradicts FR-21
- Cites: Glossary Administrator "Cannot decide Leave Requests"; FR-21 "Only the request's current Direct Manager can decide it"; SEC-12 "cannot approve or reject leave"; Q3 default "Roles combine"; FR-3 (Administrator assigns any manager); FR-12 (no self-adjustment restriction); FR-5.
- Problem: An Administrator who is also an Employee/Manager can (a) assign themselves as Direct Manager of any colluding Employee and approve their leave as that Employee's Direct Manager, in which case it is unclear whether SEC-12/AP-4 denies it (the PRD says both "Administrator cannot decide" and "the Direct Manager decides"); (b) adjust their own Total without limit; (c) grant themselves or an accomplice any role; (d) deactivate or reactivate to hide a trail. The "no Administrator override" rule is cosmetic if an Administrator can become the approver by editing the org tree.
- Fix: Decide explicitly. Recommended: SEC-12 means "the Administrator role confers no approval right"; an Administrator who is legitimately a Direct Manager may decide as a Manager. Then add: an Administrator cannot adjust their own balance, cannot assign themselves as another Employee's Direct Manager (or this is separately audited and alerted), cannot change their own roles or manager. Add these as testable consequences in FR-3, FR-5, FR-12 and fix the Glossary wording.

### H-4. Concurrency specification is incomplete and covers only two races
- Cites: FR-19 "two concurrent decisions on one request succeed exactly once"; FR-14 atomicity; FR-27 concurrency; NFR "serialised"; FR-3; FR-5 "last active Administrator".
- Unspecified races:
  - Approve versus Employee cancel (same request, one must win; loser code unspecified).
  - Approve/reject versus manager reassignment (FR-3 moves Pending to the new manager while the old manager's approve is in flight; SEC-13 is "on each call", not "inside the commit transaction").
  - Submit versus deactivation of the same Employee (see H-1) and submit versus manager deactivation or manager reassignment.
  - Two reassignments that individually pass the cycle check (A under B and B under A concurrently) but together create a cycle.
  - Two Administrators concurrently removing each other's role or deactivating each other: each sees "another active Administrator exists", leaving zero.
  - Concurrent "set opening balance" ("when no balance exists") by two Administrators.
  - Reactivation versus another operation on the same Account.
- Fix: Add one requirement: every state-changing operation re-validates its preconditions (status, ownership relationship, active flags, last-Administrator count, cycle check) inside the same serialised transaction as the write (row locks or optimistic version columns; a global lock or serializable isolation for the org-tree and Administrator-count checks). Specify the loser's response (409 Conflict with a stable error code). Extend SM-2 to this matrix.

### H-5. Dangling and orphaned Direct Manager links
- Cites: FR-3 "The manager must be an active Account" (only at assignment); FR-4 "refused until those reports are reassigned" (only counts active reports); FR-4 "any Direct Manager link removed in the meantime"; FR-14; FR-3 "set or change" (no mention of clearing).
- Problems:
  - Deactivating an Employee leaves their link to their manager; the manager can then be deactivated because the report is not "active". Reactivating the report restores a link to a deactivated manager; FR-14 only checks that a Direct Manager exists, so a request is created, Reserved and assigned to someone who cannot log in. It is stuck Pending until the Employee cancels, and nobody else can decide it (no override).
  - If the Administrator can clear a manager (FR-3 is silent), existing Pending requests have no decider. The FR-4 text "link removed in the meantime" implies removal exists, but no FR defines it.
  - L-1 says "no Direct Manager" is refused, but "manager is inactive" is a different, unhandled state.
- Fix: Define Direct Manager link rules: (a) reactivation requires the manager to be active, or clears the link; (b) FR-14 refuses when the Direct Manager is not active; (c) specify whether the link can be cleared, and if so what happens to Pending requests (auto-cancel with Reservation release, or refuse the clear while Pending requests exist); (d) deactivating a manager counts all linked accounts, or deactivation of an Employee clears their link.

### H-6. Response-code policy contradicts itself and leaves key cases undefined
- Cites and conflicts:
  - SEC-11 says 403 where "deciding a request they are allowed to read" and "Where both apply, 404 wins", but FR-16 says "Only the owning Employee can cancel; any other caller receives 404", including an Administrator or the Direct Manager who can read the request. Under SEC-11 they should get 403.
  - FR-18 returns an empty list to a non-Manager, FR-21 returns 403 to a caller with no Direct Reports, SEC-11 says 403 for lacking "the role or capability". Manager is derived, not a role; the PRD is inconsistent about whether "not a Manager" is a 403 or a silent success.
  - FR-19 "A request that is not Pending cannot be approved" and FR-16 "refused" have no code. No code is defined anywhere for: validation failure (400 vs 422), state conflict or lost race (409), throttling (429), must-change-credential restriction (401 vs 403), deactivated Account with a still-valid token.
  - FR-24 "An Administrator cannot ... cancel any request" and FR-16 "Only the owning Employee can cancel" contradict FR-4, where Deactivation (an Administrator action) cancels the Account's Pending requests. The actor recorded in status history for that cancellation is undefined.
- Fix: Replace SEC-11 prose with a table (operation by caller class by resource state, to status code), including 400/409/422/429 and the must-change state. Make FR-16 agree with SEC-11 (403 for readable-but-not-owner, 404 otherwise). Choose one behaviour for non-Managers (recommend 403 on decide, empty list on list, stated in SEC-11). Name a system actor for deactivation-driven cancellations.

---

## Medium

### M-1. Unbounded dates and numbers (DoS, overflow)
- Cites: FR-14 "There is no maximum request length and no year-boundary rule"; FR-11/FR-12 (no upper bound on Total or delta); FR-22/FR-26 "paged" with no page-size cap.
- Problem: A request for 2026-10-13 to 9999-12-31 forces a large working-day computation and, if Total is large, a multi-million-day Reservation; Total and deltas can overflow int32 or be set to absurd values. A start date 50 years out blocks future overlaps.
- Fix: Max request span (for example 365 days) and max start-date horizon; Total, delta and Requested Days bounded to a stated maximum (and checked for overflow, using 64-bit or checked arithmetic); max page size.

### M-2. Overlap is ambiguous on non-working days
- Cites: FR-15 "Overlap on even a single day is refused"; FR-14 weekend-only range refused; Glossary Leave Request "continuous range of whole Working Days".
- Problem: Request A = Wed to Sat and request B = Sat to Tue overlap only on a Saturday that costs no days. It is unclear whether this is an overlap. Calendar-day overlap blocks valid use; working-day overlap needs the range to be defined on Working Days. Also should ranges be allowed to start or end on a weekend at all?
- Fix: Define overlap over Working Days only (two requests overlap iff they share a Monday-Friday date), or require start and end to be Working Days. Add test cases (Fri to Mon followed by Sat to Tue; Mon to Fri followed by Sat to Sun refused for zero days).

### M-3. Approving a stale or already-started request
- Cites: FR-14 (backdating check only at submit); FR-19 (no date rule); no expiry for Pending.
- Problem: A Pending request can be approved after its start (or end) date has passed, consuming balance for fully elapsed leave; an unresponsive manager leaves days Reserved indefinitely; the manager UI shows "Available" for a past request.
- Fix: Refuse approval when the start date is before Today (Company Time Zone) or define that past-dated Pending requests auto-expire/convert to Cancelled with Reservation release by a documented system process; state whether rejection of past-dated requests is still allowed (recommended: yes).

### M-4. Audit: "append-only" versus purge, configurable retention floor, lost explainability
- Cites: Glossary Audit Record "append-only"; FR-25 "may be removed only by a system process"; SEC-19 "append-only"; AP Q4; Glossary Audit Retention Period "configurable".
- Problems: The same records are called append-only and purged; retention is configurable with no minimum, so setting it to 0 or 1 day wipes the trail; the system purge actor and who can change retention (config, not API) are not audited; the purge summary may record only a count. Balances, roles and accounts live forever (L-4) but their explaining Audit Records vanish after a year, which breaks "explainable later" (Vision, UJ-3). No DB-level enforcement or tamper evidence is required (the application DB user could simply UPDATE or DELETE rows).
- Fix: Reword to "immutable except by the retention process"; set a minimum retention (for example 90 days) validated at start-up; the summary record states the date cutoff, count, and configured period; require DB permissions denying UPDATE/DELETE to the application role except via the purge routine; consider a hash chain; and state that Balance and Account facts older than retention remain explainable (for example keep balance-adjustment and role-change records longer).

### M-5. Failed-login audit: attacker-controlled data, accidental secrets, flood
- Cites: FR-25 "login success and failure"; "Audit Records never contain passwords, tokens or token hashes"; SEC-21.
- Problem: For unknown emails the "target" is attacker-supplied text. Users routinely paste their password into the email field, so the audit table ends up storing passwords. Unbounded failed-login writes let an anonymous attacker fill storage and slow Administrator audit queries; log-injection characters in the identifier; no source IP is recorded although SEC-5 throttles per source.
- Fix: Do not store the raw submitted identifier for unknown accounts (store a keyed hash or a "unknown" marker plus source IP and truncated, validated length); aggregate or sample repeated failures; cap field lengths; escape on output; record source IP per record.

### M-6. Audit gaps and non-transactional recording
- Cites: FR-25 event list; SM-4.
- Problems: Not audited: Administrator reads of other Employees' balances/requests (FR-13, FR-24), authorization denials (403/404 probing), Company Time Zone or retention configuration changes, Pending requests moved on reassignment (only "manager change" is listed), system cancellations from deactivation (only listed generically), lockout events, password-change failures, logout, and the purge process actor. There is no statement that the Audit Record commits in the same transaction as the business change (or that a failed audit write fails the operation), so an action can succeed without a record, and SM-4 only tests the happy path. FR-26 lets Administrators read the trail, but nobody reviews Administrator actions on themselves.
- Fix: Extend the FR-25 list; state that the audit write is in the same transaction as the change (fail closed), except security events outside a transaction, which must be best-effort with a loss metric.

### M-7. Former-manager visibility is inconsistent and leaks free text
- Cites: FR-23 "except requests they decided themselves, which remain visible to them"; FR-18 non-Manager gets an empty list; Glossary Manager (derived).
- Problems: (a) A former manager with zero remaining reports is no longer a "Manager", so FR-18/FR-21/FR-23 may deny them the endpoint through which they would see their decided requests; the retention of visibility has no defined endpoint. (b) They keep seeing the Employee's note, dates and (per FR-13) possibly Available days indefinitely about someone no longer under them; FR-13 does not say whether a former manager sees Available. (c) "Current Direct Report" in FR-23 and FR-18 does not say whether deactivated reports count.
- Fix: Define one history endpoint with explicit visibility rules (owner, current Direct Manager, decider for their decided requests only, Administrator), state fields exposed per class (no Available or note for the former manager, or limit to decision metadata), and state treatment of deactivated reports.

### M-8. Initial credential handling is contradictory and open-ended
- Cites: FR-1 "No credential or password is returned in any response"; UJ-3 "hands over the initial credential by a channel outside the API"; FR-2; FR-27.
- Problems: Who chooses the initial credential? If the Administrator types it in, the Administrator (and any intermediary or log of the request) knows it, and the Administrator can log in as the new user before the user does (impersonation until first login, with the audit trail pointing at the user's identity). If the server generates it, FR-1 forbids returning it. It never expires: an unclaimed Account with a known credential stays exploitable indefinitely. FR-2 must-change state also needs to be defined across Refresh and what code is returned.
- Fix: State the model: server-generated one-time credential returned exactly once in the creation response (carve-out from FR-1, and never logged), or Administrator-supplied with a documented caveat; add expiry (for example 7 days) after which login is refused until reset (needs C-1's reset); require that the new password differs from the initial one; apply the must-change check server-side from stored state, including on refresh.

### M-9. Login hardening gaps beyond throttling
- Cites: SEC-2, SEC-5, FR-7, FR-10, FR-8.
- Problems: No maximum password length (hashing DoS with megabyte passwords); no equal-time behaviour for unknown email versus wrong password (timing enumeration, contradicting FR-7's "same generic error"); throttling is specified only for login, not for change-password (FR-10: someone with a stolen Access Token can brute-force the current password), refresh, or bootstrap attempts; "known-breached" screening has no source and, if external, leaks hash prefixes (testability and privacy unspecified).
- Fix: Max length (for example 128) enforced before hashing; dummy hash verification for unknown emails; throttle FR-10 and refresh; name the breached-password mechanism (offline list or k-anonymity API) or drop it from testable criteria.

### M-10. Refresh Token and session details underspecified
- Cites: FR-8, FR-9, SEC-4, SEC-6, "longer-lived".
- Problems: No lifetime or absolute maximum session length, so rotation can extend a session forever. A legitimate double-submit (retry, two tabs) trips reuse detection and forces re-login; a stolen token's owner can be forced out repeatedly (low impact, but should be a conscious decision with a short grace window). Logout "revoke their current Refresh Token": the Access Token does not identify a token family, so the mechanism (body parameter? family claim?) is undefined, and "log out everywhere" does not exist. The transport/storage of the Refresh Token (cookie versus body, CSRF exposure) and signing algorithm pinning/issuer/audience are not constrained anywhere (deferred, but no requirement to decide).
- Fix: Add absolute session lifetime (for example 30 days) and idle expiry; define the reuse-detection grace behaviour; define logout input and an "all sessions" revocation; add architectural must-haves (algorithm pinned, issuer/audience validated, key rotation).

### M-11. Email uniqueness and identity normalisation
- Cites: FR-1 "unique work email"; SEC-5.
- Problem: Case, Unicode normalisation, trailing whitespace and lookalikes ("Admin@x.com" versus "admin@x.com") are not addressed; duplicate or impersonating Accounts and login ambiguity result. There is also no operation to correct a name or email (no update endpoint), so typos force a new Account, which the "one person has one Account" rule forbids.
- Fix: Specify case-insensitive, normalised uniqueness enforced by a DB unique index; add an update-profile FR (Administrator only, audited) or state the limitation.

---

## Low

### L-1. No balance exists: undefined behaviour
- FR-11 "set ... when no balance exists"; FR-13; FR-14. Does a new Employee have a zero balance or none? What does view return (404 or zeros)? Does submit refuse? Fix: auto-create a zero balance at Account creation (simplest) and make FR-11 "set the opening balance once, then only adjust".

### L-2. Approved leave cannot be undone, so mistakes are permanent
- Cites: UJ-4 edge case; §7 "no cancelling an Approved request". A wrongly approved request still blocks overlaps and consumes Total; the only remediation is an Administrator balance adjustment, which does not change the request status or the overlap. Fix: acknowledge as a limitation, or allow Administrator-driven "void" with audit.

### L-3. Manager can infer other pending leave from Available
- Cites: FR-13 "Available days, which already exclude that request's Reservation"; AP-3. Total minus Reserved is exposed, and it changes with the employee's other Pending requests. Also unclear whether Available is current or as at decision for historical requests. Fix: state "current value" and accept the minor leak, or show Available excluding all Pending.

### L-4. Free-text fields have no limits or handling rules
- Notes, rejection reasons, adjustment reasons and names: no max length, no stated encoding or output-escaping rule for a future React client, and they are copied into audit values (FR-25 before/after). Fix: max lengths, stored as plain text, and a note that audit copies inherit the personal-data classification.

### L-5. Unauthenticated surface not fully defined
- SEC-7 exempts "login, refresh and health"; SM-7 requires an OpenAPI document, and any Swagger UI or document endpoint is neither exempted nor required to be disabled in deployed environments; health must not reveal versions, dependency detail or DB state. Fix: state OpenAPI/UI behaviour per environment and health payload minimalism.

### L-6. Time handling edge cases to pin down
- Cites: FR-14 "Today (in the Company Time Zone)"; §6 Time.
  - A client in another zone picking its local "today" can be refused around the date boundary; document that the server decides and returns the current Today in an error or a metadata endpoint.
  - Midnight race: Today is evaluated at the server instant of validation; a request that starts the second after midnight is evaluated at commit, not at receipt. Specify the evaluation point and require an injectable clock for tests.
  - "Changing the Company Time Zone affects only evaluations made after the change" is fine, but existing Pending requests may have start dates that are now before Today; combined with M-3 their approval path is undefined. An invalid or non-IANA zone value must fail start-up. The zone value change itself is not audited (M-6). Whether the change is applied live or at restart is unstated.
  - Kolkata has no DST, so DST-gap behaviour is moot today, but because the zone is configurable, date arithmetic must be calendar-date based (no instant arithmetic).

### L-7. Untestable or ambiguous phrases
- "approximately 15 minutes" (SEC-3); "temporary lockout after repeated failures" (SEC-5, thresholds deferred); "known-breached" (SEC-2); "non-guessable" (SEC-18, no definition such as GUID v4/v7 or at least 128 random bits; note v7 is time-ordered); "no later than the access-token lifetime" (FR-4); "security headers are set" (SEC-17, no list); "need-to-know" (SEC-20); SM-2 and SM-5 "zero cases ... under concurrent tests" (absence cannot be proven; specify a stress test with stated iteration and parallelism counts and a DB-level invariant check); SM-1 claims FR-1 to FR-22 but UJ-6 and journey tests depend on FR-27 only; performance NFR gives no dataset, and no request-throughput.
- Fix: Give numeric or named-standard values (for example lifetime 15 minutes plus or minus clock skew, lockout 5 failures per 15 minutes, OWASP secure-headers list) or mark as architecture-owned with an explicit acceptance test.

---

## Areas reviewed and found solid
- Resource concealment for Employee and Manager access (FR-17, FR-21, FR-6; UJ-2): 404 for non-entitled callers, and the 403 for the owner/Administrator on decide leaks nothing the caller could not already read. A non-Manager receiving 403 for every id is not an oracle about specific requests.
- Atomic submit with overlap and Reservation (FR-14), balance invariants (Available = Total minus Reserved, never negative), and the arithmetic of approve (Total and Reserved both fall, Available unchanged), reject and cancel.
- Refresh rotation with family revocation on reuse (FR-8, SEC-4) and hash-only storage; revocation on logout, password change and deactivation (SEC-6).
- Mass-assignment protection (SEC-14) and server-side relationship checks (SEC-8, SEC-13).
- Bootstrap Operation behaviour (FR-27): idempotent, atomic, not hardcoded, secret-handling stated, refused repeats audited. Note one ambiguity only: UJ-6 says "no Accounts exist" while FR-27 tests "no Administrator Account exists"; align the wording on the latter and state what happens if Accounts exist without an Administrator (email collision or recovery use; see C-1).
- Weekend-only ranges and adjacent ranges are explicitly handled (FR-14, FR-15).

## Summary table

| Severity | Count | IDs |
|---|---|---|
| Critical | 1 | C-1 |
| High | 6 | H-1 to H-6 |
| Medium | 11 | M-1 to M-11 |
| Low | 7 | L-1 to L-7 |
