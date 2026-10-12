# PRD Quality Review (round 4) — Leave and Workforce Management API

Scope note: accepted limitations L-1 to L-12 and section 10.1 decisions are treated as given. Findings below are contradictions, undefined states, dead ends, untestable requirements and mechanical drift. Judgment was formed from the PRD before any look at earlier reviews.

## Overall verdict
The PRD is strong: a clear thesis (rules enforced by the system, one narrow path), specific testable consequences on nearly every FR, an honest limitations register, and a consistent ordered response-code policy that the FRs follow almost everywhere. The residual risk is concentrated in the temporary-password account state model (the post-issuance state is unnamed and two statements contradict, and the Must-Change to Awaiting Password transition has no trigger) and in a few Administrator-lifecycle edges (role-removal dead end, undefined response when a direct grant is attempted after the window closes). No critical defects; two high findings are small wording fixes and should be closed before architecture.

## Decision-readiness — strong
Decisions are stated as decisions and indexed (AP, RV, Q-E, D), trade-offs are named in L-1 to L-12 with what was given up, and the two open questions are real and non-blocking. L-9 honestly admits collusion and single-Administrator actions. Only gap: L-12 states a remedy that does not work in one reachable case (see M1).

### Findings
- **medium** L-12 remedy fails when the second Administrator lost the role (§10.2 L-12, FR-31 Notes, FR-5, FR-29) — L-12 and the FR-31 note say the Operator "reactivates an existing Administrator through FR-29". FR-5 lets one Administrator remove another's role (L-9). After the window closes, A removes B's role: A is the last active Administrator, B is an ordinary Employee, FR-29 covers only "an Administrator Account", FR-27 refuses bootstrap because an Administrator exists, and FR-31 needs two. No path ever restores a second Administrator. *Fix:* either let FR-29 restore the role on a named Account (audited, Operator-reasoned), or state in L-12 that role removal is a permanent dead end for adding Administrators.

## Substance over theater — strong
No persona theater (8 UJs, each drives distinct FRs and edge cases; Karan the Operator is load-bearing). NFRs carry numbers (15 min, 24 h, 128 bits, 12 to 128 chars, p95 500 ms for 200 employees, page 25/100). Vision is specific to this product. Counter-metrics SM-C1 to SM-C3 are real.

### Findings
- **low** SM-8 and SM-9 are process or aspiration metrics ("usable without reading the code") with no threshold (§9). *Fix:* accept as secondary, or give SM-9 a check (every endpoint present in OpenAPI, verified by a contract test).

## Strategic coherence — strong
Thesis (system enforces rules so figures cannot drift) is validated by SM-2 (database invariants after concurrency stress), SM-3 (authorization matrix) and SM-4 (audit atomicity). Scope kind (problem-solving, narrow path) matches the increment order in 8.3. Out-of-scope items are deliberate.

### Findings
- **low** Increment order places "audited Administrator reads" in increment 7, but FR-13 (Administrator balance read, increment 3) and FR-24 require those reads to be audited from the start (§8.3 vs FR-13, FR-25). *Fix:* note that the read endpoints in increments 3 and 6 ship with audit, or move the audit hook earlier.

## Done-ness clarity — adequate
Almost every FR has concrete, status-coded consequences, and the concurrency list in section 6 is unusually thorough. Weak points are the temporary-password state model (undefined states below) and a handful of values deferred without an owner.

### Findings
- **high** Awaiting Password semantics contradict across the PRD and the post-issuance state is unnamed (§3 Awaiting Password, FR-2, FR-7, FR-30, UJ-3) — the Glossary says an Awaiting Password Account "cannot log in until the Operator issues a Temporary Password". FR-2 and UJ-3 imply that after issuance the Account is still Awaiting Password (UJ-3 expiry: "the Account stays Awaiting Password"; FR-30 allows reissue only for Awaiting Password; FR-30 "issuing a new one invalidates the previous one"). But FR-7 lists "an Awaiting Password Account" among cases that return the generic 401, while FR-2 says a Temporary Password login must succeed into Must-Change State. A tester cannot decide whether an Account holding an unexpired issued password gets 401 or Must-Change. Glossary "no usable password" is also contradicted by an issued but unused password. *Fix:* define the account password states explicitly: Awaiting Password (no credential issued), Temporary Issued (credential issued, unexpired, unused), Must-Change, Normal; or define Awaiting Password as "no consumed password" with an "issued" flag. Update the Glossary, FR-7's 401 list ("an Awaiting Password Account with no valid Temporary Password"), FR-30's precondition and UJ-3.
- **high** Must-Change to Awaiting Password transition has no trigger (FR-2 last bullet, FR-30 precondition, SEC-13, FR-9) — FR-2 says that if the Must-Change session expires, is logged out, or the 24 hours lapse "the Account is returned to the Awaiting Password state". Session expiry and 24-hour lapse are passive events with no request to carry them. If the transition is lazy, an Account abandoned in Must-Change state fails FR-30's "Account that is Awaiting Password" precondition and the Operator cannot reissue: the exact dead end FR-2's last sentence says cannot occur. The consumed Temporary Password also means login returns 401, so nothing can trigger it. *Fix:* state that the Operator's issuance (FR-30) and FR-29/FR-28 accept an Account in Must-Change or Temporary Issued state (reissue supersedes), or that state is derived (Must-Change is valid only while its session is valid, otherwise treated as Awaiting Password on read). Add an audit event for the return, or state none is needed.
- **medium** Response contract when FR-1 or FR-5 would create or grant an Administrator after the window closes is undefined (FR-1, FR-5, FR-31, UJ-8) — FR-1 says such a request "is subject to FR-31"; FR-31 says the action "takes effect only through a Privileged Request"; UJ-8 says a direct fourth grant is "refused (403)". FR-31 consequences never list that 403, and SEC-11 does not classify it (rule 3 "role whose existence is not secret", rule 5, or rule 7). It is also unclear whether FR-1 with the flag silently turns into a Privileged Request (what is returned, is the Account created now?). *Fix:* decide one behaviour, for example "direct create/grant is refused 409 with a code pointing to the Privileged Request endpoint", and place it in SEC-11's list.
- **medium** Privileged Request for Account creation has no defined target or payload (FR-31, Glossary Privileged Request) — "Naming the target and the action", "the target cannot be Initiator or Approver", and re-check "target still valid" assume an existing Account, but creation requests have none; they must carry name, email and manager, and email uniqueness (409) can fail at execution. *Fix:* state that a creation request stores the FR-1 fields, that execution re-validates them under FR-1 (duplicate email 409), and who the "target" is for creation (the email).
- **medium** Combined account states are undefined for deactivated Accounts (FR-28, FR-29, FR-30, FR-4, FR-7) — FR-28 puts any ordinary Account, including a deactivated one, into Awaiting Password; FR-30 will then issue a password for it, but FR-7 requires an active Account so login returns 401. FR-29 explicitly covers this only for Administrators (reactivate only if asked). *Fix:* either refuse FR-28 and FR-30 for deactivated Accounts (409 or Operator refusal) or state that issuance to a deactivated Account is allowed but unusable until reactivation, and reactivation (FR-4) resets to Awaiting Password anyway.
- **medium** Bounds deferred without a value or owner make several requirements untestable (SEC-5(c), FR-25, SEC-14) — SEC-5(c) "a per-source ceiling and a global ceiling" has no numbers; FR-25 "bounded failure count per source and time window" and "field lengths are capped" give none; SEC-14 requires maximum lengths for "reasons" but only the Leave Request rejection reason (500, D9) has one. Balance Adjustment reason, manager-clearing reason, FR-29 recovery reason and FR-11 reason have no limit, and no response (400) is tied to them. SM-3/SM-2 cannot verify what is unstated. *Fix:* add "indicative" values or explicitly assign them to architecture; apply the 500-character reason limit to every reason field.
- **low** FR-30 and SEC-22 require the Operator to "verify the recipient" and follow a written procedure; this is a human control with no testable consequence beyond the existence of the document (FR-29 deliverable). SM-7 also asserts "at least 128 bits of randomness" "verified by tests", which can only be checked by inspecting generator parameters. *Fix:* phrase as "the procedure document exists and is referenced by the Audit Record's stated reason" and "generator uses a CSPRNG with at least 16 random bytes, verified by unit test".
- **low** FR-2 does not say whether the Must-Change Access Token is invalidated when change-password returns "a normal session" (FR-2, FR-10, SEC-24). *Fix:* state that the Must-Change session is replaced, so its token is rejected after success.
- **low** No requirement says which response an Administrator gets for a nonexistent target Account or employee identifier on FR-1/FR-3/FR-11/FR-12/FR-28 (SEC-11 rule 4 speaks only of request readers). Likely 404 by design. *Fix:* add one line to SEC-11 for Administrator operations on missing targets.
- **low** FR-27 does not name its inputs (name, email, optional configuration) although it refers to "leftover bootstrap input". *Fix:* list name and work email as inputs and the uniqueness/length rules from FR-1.
- **low** FR-6 has no consequence for the Manager-side single-request read that UJ-2 uses ("opens one"). FR-17 is Employee-centred; FR-18 lists. *Fix:* add a Manager get-one-request line to FR-18 or FR-21 and state that it follows the Request Reader rules.

## Scope honesty — strong
Non-Goals are explicit and match 8.2. Limitations are enumerated and linked from FRs. Assumption density is low (4 inline plus the brief's A1 to A4), proportionate for a learning project. Open questions are genuinely open.

### Findings
- **low** Security-Class versus Ordinary classification is ambiguous for some FR-25 events (§3 Security-Class Event, FR-25). Login success, lockout transitions, failed-login aggregates and Audit Record queries are not obviously "Account, role, manager, password, session-security ..." events, and the retention split (L-11) depends on it. *Fix:* list the class of every FR-25 event, or add "login and lockout" to the Security-Class list.

## Downstream usability — adequate
The Glossary is real and used almost uniformly; IDs are contiguous; requirements are quotable out of context. Some drift and one orphan FR (below). Chain-top PRD, so traceability matters.

### Findings
- **low** FR-5 (manage the Administrator role) is not mapped by any user story in 4.8 (US-1 to US-21). *Fix:* add FR-5 to US-19 or a new story.
- **low** SM-1 "Validates" omits FR-2 and FR-10 although UJ-1, UJ-3, UJ-6 and UJ-7 pass through first login and change-password; SM-1 also lacks FR-5 and FR-6 while claiming the full UJ set (§9). *Fix:* add FR-2, FR-10 (and FR-6/FR-5 if intended).
- **low** Glossary drift: "Bootstrap Operation" (capitalised in UJ-6 and 8.3) is not a Glossary term (only "bootstrap" under Operator Operation); "single-Administrator action" (FR-4, FR-5, L-9, RV-5) is used as a term but not defined; lower-case "temporary password", "operator operation", "account", "manager" appear in UJs, RV and L entries; "Manager" is defined as having at least one active Direct Report while FR-18 and FR-23 give Manager capability to callers whose reports are all deactivated or gone (empty list, Decider reads). *Fix:* add two Glossary lines and align case; note in the Manager definition that FR-18 and FR-23 apply to any caller.

## Shape fit — strong
A backend-only API with an Administrator, Manager, Employee and Operator: UJs with named protagonists are justified (multi-role, isolation-heavy). The capability-spec shape (FRs with consequences) fits. Heavy security content is owner-chosen and the learning purpose is acknowledged in section 0.

### Findings
- **low** UJ-7 and UJ-6 protagonists are split (Dev is the "persona", Meera/Karan act); acceptable but the Persona line for UJ-7 names Dev while the climax and edge case are Meera's and Karan's. *Fix:* optional; name Meera as protagonist.

## Mechanical notes

**ID continuity.** FR-1 to FR-32 contiguous and unique (27 to 32 placed out of order and flagged in 4.8); SEC-1 to SEC-24, SM-1 to SM-9 plus SM-C1 to SM-C3, UJ-1 to UJ-8, US-1 to US-21, L-1 to L-12, AP-1 to AP-9, RV-1 to RV-11, Q-E1 to Q-E8, D1 to D16, Q1 to Q10 (Q1, Q2, Q4, Q6, Q7, Q8 in 10.1; Q3, Q5, Q9, Q10 in Q-C), Q-A and Q-B. No gaps or duplicates. All FR, SEC, L, D, AP, RV references resolve. 8.1 covers every FR. Only unmapped item: FR-5 in 4.8 (low, above).

**Assumptions Index roundtrip.** Inline tags: Manager (§3), Operator (§3), SEC-2, section 6 performance. All four are indexed. The index's "Brief A1 to A4" entry has no inline counterpart (low; it duplicates the Manager assumption and the Administrator-no-approval rule, which is a decision, not an assumption). No inline assumption is missing from the index.

**Section 10.1 'Where applied' accuracy.** Accurate except: AP-1 lists FR-6, whose only response rule is a role 403 (SEC-11 rule 3), not resource-concealing 404 (low). RV-11 omits FR-7 and FR-10, both of which carry Must-Change or temporary-password behaviour (low). RV-2 does not list FR-28/Q-E2 (covered by RV-6; fine). All others verified.

**Cross-reference consistency with SEC-11.** FR-16, FR-17, FR-19, FR-20, FR-21, FR-23, FR-24 and FR-31's codes are consistent with the seven ordered rules, including the 404-beats-403 tie-break and the 400-before-409 order. Only the FR-1/FR-5 post-window response (M above) is unplaced. FR-12's "Total below Reserved (409)" versus "non-negative (400)" resolves under rule order (400 first); worth a note but not a defect.

**Temporary-password lifecycle check (FR-2, FR-7, FR-8, FR-10, FR-30, SEC-11, SEC-13, SEC-22).** Rules for one-use, 24 h, 128 bits, hash-only, no-logging, no Refresh Token, 401/403 codes and throttle clearing are consistent across FR-2, FR-30, SEC-5, SEC-11, SEC-13, SEC-22 and SM-7. Defects are the two highs (state naming and transition trigger) and the deactivated-Account combination (medium).

**Administrator lifecycle check (FR-31, FR-5, FR-4, FR-27, FR-29).** Last-Administrator safeguards (FR-4, FR-5), window concurrency, self-dealing 403s and bootstrap-once are consistent. Dead end: role removal after window closure (M1). Minor, with a workaround: if every Administrator shares one Direct Manager who forgot a password, FR-28 forbids each from resetting that manager; any Administrator can first clear another's Direct Manager via FR-3, so no true dead end (FR-28 for own Direct Manager, Q-E2).

## Counts
Critical 0, High 2, Medium 5, Low 13.

## Fit to finalize
Fit to finalize and hand to architecture after fixing the two high items (both wording changes to the password state model); the five mediums can be fixed in the same pass or handed to architecture as noted decisions.
