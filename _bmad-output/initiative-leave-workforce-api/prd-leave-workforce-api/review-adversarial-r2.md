# Adversarial Review R2: Leave and Workforce Management API PRD

Scope: `prd-leave-workforce-api.md` (status: final). Findings are mine, formed from the PRD text alone.

**Verdict:** Not safe to hand to architecture unchanged. The PRD is strong on per-call authorization, response codes, balance arithmetic and audit atomicity. But its headline separation-of-duties claim (RV-2, SEC-12, AP-4) is cosmetic. A single Administrator can defeat every self-dealing rule using only operations the PRD grants, and L-9 understates this as "collusion between two Administrators". There is also one operational deadlock (offboarding the org root) and a few specification holes around throttling, audit retention and race response codes.

Counts: Critical 2, High 3, Medium 8, Low 7.

---

## Critical

### C1. Self-dealing restrictions are void: one Administrator can mint or take over a second identity
Quotes:
- SEC-12: "cannot assign themselves as another Employee's Direct Manager (at creation or later), change their own Direct Manager or roles, deactivate themselves, or set or adjust their own balance."
- FR-1: an Administrator creates Accounts, including "the Administrator role or not", and receives the temporary password.
- FR-28: an Administrator can reset "another Account's password".
- L-9: "Collusion between two Administrators is not prevented."

Every restriction is written as "not yourself". The sole actor needs no colluder:
1. Admin A creates puppet Account P with the Administrator role (FR-1; no second approval). A knows P's temporary password and sets a new one at first login.
2. P, a different actor, now sets A's balance (FR-11/12), assigns a Manager of A's choosing as A's Direct Manager (FR-3), grants or removes A's roles (FR-5), or reactivates A.
3. Alternatively, A resets any existing other Administrator (FR-28) and does the same through that identity.

The audit trail attributes the actions to P or B, and nothing links P to A. L-9 frames this as two-Administrator collusion. The attack is single-actor and needs no cooperation, so RV-2 and SEC-12 give a false assurance.

Fix (pick at least one and state the property honestly):
- Two-person control for privileged operations: creating an Administrator, granting the role, resetting an Administrator's password, and any change to a target's balance, manager or role where the target is an Administrator. Approval comes from a different Administrator than the initiator.
- Record `created_by` on Accounts. Forbid the creator of an Account from performing FR-3/5/11/12/28 on it, and from acting on the Accounts it created.
- Rewrite SEC-12 and RV-2 as accountability controls (detective), not preventive, if neither is accepted. Move the single-actor bypass into L-9 explicitly.

### C2. Password reset and creation hand the Administrator working credentials, which negates "Administrator confers no right to decide leave" (AP-4)
Quotes:
- FR-28: "returned once to the Administrator"; the Account "must change" the password at first login.
- FR-2: the Account can call only change-password and logout until changed.
- FR-10: change-password needs only the current password, which the Administrator knows.

The Administrator logs in with the temporary password, changes it, and now owns the account. The real user is locked out and learns of it only if they try to log in. There is no notification (L-3). Consequences:
- Admin resets the Direct Manager of any Employee, including the Administrator's own manager, and approves or rejects as that Manager. Audit shows the Manager as Decider. This is self-approval and approval-by-proxy despite AP-4 and FR-24.
- Admin takes over any Employee: submits leave, cancels requests, reads the balance. Audit blames the victim.
- Admin takes over another Administrator (feeds C1).
- "No role can read passwords" (SEC-12) is false in effect. The Administrator is the carrier of every initial credential.

L-9 notes the "log in as that person before they do" risk and accepts it with "audit trail". That does not bound it, because the audit attributes the takeover to the victim.

Fix:
- The reset flow should not give the Administrator a usable credential. Options are a one-time reset token the user redeems (needs email, deferred), or a reset that sets the must-change state with a credential the Administrator cannot use for login (for example, a code valid only for the change-password call from a different source).
- Failing that, tag the session: the first login after a reset or creation is audited with the "login under temporary credential" marker, and the resulting change-password audit record is flagged. Block Administrators from resetting their own Direct Manager, any Administrator, and any Manager with Pending requests from a Direct Report who is that Administrator.
- Add a visible "password last changed / last reset by" field to the Account so the victim can see it.

---

## High

### H1. The top of any reporting chain can never be deactivated (offboarding deadlock)
Quotes:
- FR-4: "Deactivating an Account that still has active Direct Reports is refused until those reports are reassigned."
- FR-3: "There is no operation to clear a Direct Manager in the MVP"; reassignment cycles are refused.
- UJ-5 and L-1 describe a Top-Most Person with reports.

Take the root R with reports R1..Rn. Each Ri must be reassigned to a manager who is not in Ri's own subtree and is not R. Eventually some Rk has no valid new manager: clearing is not allowed, and any remaining candidate is either R or in Rk's subtree (cycle). So the root, and the first Administrator if they manage anyone, can never be deactivated while the organisation has a single tree. A departed founder or the bootstrapped Administrator keeps a working login forever. A deactivated Direct Report chain has the same problem when it is the last one. FR-5's last-Administrator rule makes it worse for an Administrator root.

Fix: add an audited Administrator operation to clear a Direct Manager (resolves L-8 for this case), or allow deactivation to leave reports as Top-Most Persons with an explicit audit entry. State the intended outcome for Pending requests of reports when their manager disappears.

### H2. Throttling depends on an undefined "source", and temporary passwords have no stated strength
Quotes:
- SEC-5: "throttled per account-and-source pair", "5 failures in 15 minutes triggers 15 minutes of refusal".
- FR-2/FR-28: "server-generated temporary initial password"; valid 7 days.
- L-9: "guessing spread across many sources" is not stopped.

"Source" is not defined. Behind a reverse proxy or Azure front door it will be a forwarded header, which a caller can rotate to get unlimited attempts. Behind NAT, an entire office shares one source, so an insider on the same NAT can lock a named colleague (or the only Administrator) out. Throttle state keyed by arbitrary email strings (SEC-5 covers unknown emails too) is also an unbounded-growth vector.

The server-generated temporary password is valid for 7 days, and SEC-2 applies only to passwords the person chooses. Its length and entropy are unspecified (FR-2, FR-28). Operator-supplied ones (FR-27/29) meet SEC-2, but the server-generated ones carry no requirement.

Fix: define source as the connection address, trusting forwarded headers only from configured proxies. Add a global per-account failure ceiling with exponential back-off (bounded to avoid a permanent lockout DoS) in addition to the per-source one. Require generated temporary passwords of at least 128 bits of entropy (or a stated minimum length from a CSPRNG). Cap throttle-state size and TTL.

### H3. The audit "immutable" guarantee is weaker than stated, and retention erases the evidence the vision depends on
Quotes:
- FR-25: records "may be removed only by a system process"; the removal "is itself recorded in a summary Audit Record"; the minimum is 30 days.
- FR-25: changes to "the Company Time Zone or Audit Retention Period" are audited, but no FR provides an endpoint, actor or procedure for changing either.
- L-4: balances and leave data are kept indefinitely.
- Vision / JTBD: "every change [is] explainable later".

Problems:
- Who changes retention or time zone, and how? If it is configuration, the "actor" is undefined. If an Administrator can change it through the API, a rogue Administrator sets 30 days and purges the evidence of C1/C2. Neither is specified.
- Nothing requires that the application database role lack DELETE or UPDATE on the audit table, or that the purge runs under a separate role. "Cannot be edited or deleted through the API" is satisfied by a code path that still uses a fully privileged connection.
- The purge summary record is itself purgeable on the next cycle.
- After one year the audit disappears while balances persist forever. A balance of 3 days cannot be explained, which contradicts the vision. Security events (role changes, resets, recovery) and business-balance events deserve different retention than login noise.
- There is no tamper evidence (hash chain, external sink), so an operator with database access can rewrite audit data undetected.

Fix: make retention and time zone operator-only configuration with an audited start-up record (actor "operator/config", old and new). Specify DB permissions (app role has INSERT and SELECT only; purge is a separate job and role). Exempt the purge summary and security-class events from the short retention, or set a longer floor for them. Add tamper evidence as an architecture requirement.

---

## Medium

### M1. FR-6 contradicts SEC-11 on status codes
FR-6: "a lookup of any other Account by a non-Administrator returns 404." SEC-11 rule 2 gives 403 for "administrative endpoints, Administrator-only reads", and rule 2 outranks rule 3. If lookup is the same Administrator endpoint as the list, rule 2 says 403 while FR-6 says 404. If it is a different endpoint (Manager's reports), say so. SM-3's matrix cannot be written without this.
Fix: define the Direct-Report lookup as a distinct endpoint (resource-scoped, 404) and the Administrator account endpoints as admin-only (403).

### M2. Race response codes conflict with SEC-11 and with each other
- §6: "The loser receives 409 with a stable error code", including approve versus manager reassignment. SEC-11/FR-21 says a former Direct Manager who is not the Decider is not a Request Reader and receives 404. The loser of that race is exactly that caller. Both cannot hold.
- FR-21 lists 403 for non-deciding Request Readers and 409 for a non-Pending request without an order. SEC-11 rule 5 puts 409 last, so 403 should win. State it.
- SEC-11 rule 5 puts 400 after 404/403, but typical model binding returns 400 before any resource lookup. State whether blank-reason or malformed input from a non-reader is 400 or 404, as 400 is harmless and 404 is costly.
- FR-19's "A Manager cannot approve their own request (403)" cannot occur: nobody is their own Direct Manager (FR-3). Dead requirement; keep only the owner-is-not-manager rule.
Fix: one table of (caller relationship, resource state, input validity) to status code.

### M3. Expired temporary password: unspecified response and incomplete enforcement
- FR-2: "after that, login is refused". The status and body are unspecified. A distinct error reveals that the email exists and is in a particular state, contrary to FR-7/SEC-5. State that it returns the generic 401.
- Expiry is enforced at login only. An Account that logged in on day 6 stays in must-change state with a live Refresh Token (7 days idle, 30 absolute) and can sit on an expired temporary password. Refresh and change-password do not re-check expiry (SEC-13 lists active state, role, must-change flag only).
Fix: check expiry on every call in must-change state, refresh included, and consume the temporary credential on first successful use.

### M4. Past-start approval rule leaves requests in limbo, and uses the wrong date
- FR-19/RV-4: a request whose start date is before Today "cannot be approved (409). It can still be rejected, and the Employee can still cancel it."
- A Pending request that nobody decides or cancels holds Reserved days indefinitely, for example after an Employee goes on leave unapproved and never cancels. There is no expiry or auto-cancel. A manager can also deliberately wait it out. The Employee has taken leave with no record, and the days stay locked unless someone acts.
- The rule tests the start date, not the first Working Day. A Sat-Tue request submitted Friday is refused for approval from Saturday, though no Working Day has passed (FR-14 has the same edge: a request submitted Sunday for Sat-Tue is refused though Mon-Tue is future). Use the first Working Day of the range for both rules.
Fix: add a system transition (Expired or auto-cancel with audit) for Pending requests whose first Working Day has passed, releasing the Reservation, or document the manual path. Use first Working Day.

### M5. Concurrency list misses several races that break invariants
§6 lists approve/cancel, approve/reassign, submit/deactivate, cycle, mutual role removal, concurrent adjusts. Missing:
- Reactivation of Employee X versus deactivation of X's Direct Manager M. FR-4 checks active Direct Reports on M and FR-4 checks active manager on X independently, so both can pass: an active Employee under a deactivated Manager.
- Assigning or reassigning a manager (FR-3 "manager must be an active Account") versus deactivating that manager.
- Approve versus deactivation of the Employee (deactivation cancels Pending; approve must lose with 409).
- Cancel versus manager reassignment, and adjust versus approve (should be fine under row locks, but list them).
- FR-11 versus FR-12 on a new Account.
SM-2 only tests the three invariants for FR-14/15/19. Add an invariant "every active Employee's Direct Manager is active" and cover the above in the stress test.

### M6. Access tokens outlive logout, password change and grace-window refresh
- FR-9/FR-10/SEC-6 revoke Refresh Tokens only. A stolen Access Token works for up to 15 minutes after logout or password change (apart from the must-change case after reset, which SEC-13 handles well).
- SEC-4's "short grace window for a legitimate double submit" lets a thief who replays a just-used token receive a valid second pair, forking the session undetected.
Fix: put a session or credential-version stamp in the Access Token and compare it per call (the per-call lookup already exists, so the cost is zero). Return the same successor pair inside the grace window, not a new one.

### M7. Unauthenticated audit and throttle flooding
FR-25 audits every login failure and lockout, and the audit write is in the same transaction as the change. An anonymous attacker can generate unlimited rows (and throttle entries) with random emails, filling storage and, if the audit write must succeed, slowing or failing logins. Retention (1 year) means this accumulates.
Fix: audit the lockout transition and a bounded count per source and window, not every failed attempt. Rate-limit globally before the audit write.

### M8. Privileged reads are not audited
FR-25 lists writes only. Administrators read any Employee's requests, notes, balances (FR-24, FR-13) and any Audit Record (FR-26). Nothing is recorded when an Administrator reads another person's leave data or queries the audit. Notes are free text and can hold health information. A curious or compromised Administrator is undetectable.
Fix: audit Administrator reads of another person's requests and balance, and every FR-26 query (query parameters, not results).

---

## Low

- **L-a. Time-zone change is under-specified.** "Changing the Company Time Zone affects only evaluations made after the change" is true by construction and says nothing useful. Real effects: a Pending request flips between approvable and not, and the no-backdating rule shifts by up to about a day (a westward change permits submitting and approving up to a day of backdated leave). Define a single server clock source and state whether in-flight requests re-evaluate.
- **L-b. Date input format.** Leave dates are "calendar dates without a time of day" but the API contract never says that date-times or offsets are rejected. A datetime with an offset converted to UTC can shift the day.
- **L-c. Operator identity and preconditions.** FR-29 says the operator is "marked as the actor" but not how the operator is identified. It does not require that no Administrator can log in as a precondition, so the operation can reset any Administrator at any time. Acceptable for a trusted operator, but say so.
- **L-d. Bootstrap trigger ambiguity.** "A start-up task or something else" (FR-27 Notes) risks a start-up bootstrap re-run from a leftover environment variable after DB restore or Administrator loss. Require that the bootstrap input is consumed or ignored once an Administrator exists, and audit refusals (already required).
- **L-e. Permanent email and no reuse.** Email is unique including deactivated Accounts and cannot be edited (L-8). A typo at creation is permanent, and a re-hire must reactivate the old Account with old history and balance. Document the reactivation path, since FR-4 reactivation also retains the old balance.
- **L-f. Password reuse.** FR-10 only requires the new password to differ from the initial one. A user can change to their current password or a prior one. Add "must differ from the current password" at least.
- **L-g. Documentation tension.** SEC-17 disables the OpenAPI document in deployed environments while SM-7 and §6 require a usable contract for the front-end developer. Say the document is produced as a build artifact.

---

## Where the PRD is solid

- **Per-call authorization (SEC-13, RV-3, L-2).** Re-reading active state, role and the must-change flag on every call closes stale-token privilege problems, including post-reset access with an old Access Token. This is the best part of the document.
- **Response-code policy (SEC-11).** The ordering with "404 wins" is sound and consistent with FR-16/17/21 apart from M1 and M2.
- **Balance arithmetic.** Delta adjustments inside the serialised transaction, Total >= Reserved at commit, approval moving Total and Reserved together, whole days, bounded values, and the SM-2 invariants are coherent. I found no way to make Total or Reserved negative with the listed operations.
- **Overlap and working-day semantics (FR-15).** The weekend-sharing example is correct and Rejected and Cancelled do not block.
- **Audit write atomicity (FR-25).** Business change and audit in one transaction, no secrets, and the pasted-password-in-email-field case are all handled.
- **Bootstrap (FR-27).** Atomic, concurrent-safe, no default credentials, refused repeats audited.
- **Last-Administrator and mutual removal (FR-5, §6).** Covered with a serialised check.
- **Login enumeration (SEC-5, FR-7).** Dummy verification, same 429 for unknown and known emails, same 401 for deactivated.
