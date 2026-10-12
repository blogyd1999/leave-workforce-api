# PRD Quality Review (round 2) — Leave and Workforce Management API

Scope: fresh read of the latest revision (status: final, 2026-10-12), judged on the PRD alone first. Earlier reviews were consulted afterwards only to confirm that their high findings (FR-21 versus SEC-11 tie-break; no password reset or unlock path) are resolved. Both are: FR-21 now defers to SEC-11, and FR-28 and FR-29 exist.

## Overall verdict
This is a strong, unusually rigorous PRD for a learning project. The vision is specific, the non-goals are explicit, nearly every FR carries testable consequences, and the SEC-11 ordered response-code policy is applied consistently wherever an FR states a code. The remaining risk is not substance. It is (a) business-rule refusals that have no status code, which weakens the SM-3 authorization matrix and story acceptance criteria, (b) two configurable settings (Company Time Zone and Audit Retention Period) that are audited but have no defined way to be changed, and (c) review-process residue and unconfirmed items in a document marked "final". There are no critical findings.

## Decision-readiness — strong
§10 cleanly separates approved decisions (AP, RV, Q), known limitations (L-1 to L-9), defaults awaiting confirmation (§10.3) and genuinely open items (Q-A to Q-D). Trade-offs are named with what was given up, for example L-1 ("Leave for such people is handled outside the system"), L-2 (one lookup per call), L-9 (Administrator trust and collusion), and RV-4, which openly amends brief decision D8. Q-B (policy for the Top-Most Person's leave) is a real open question with an owner and a revisit trigger. The PRD uses §10 instead of `[NOTE FOR PM]` callouts, which works because the tensions are concentrated there.

### Findings
- **medium** "Final" status coexists with unconfirmed items and process language (front matter `status: final`; §0 "*Working title — confirm.*"; §8.3 "two dependency corrections for review"; §10.3 "Question as asked in the earlier review"; §10.4 Q-C and Q-D). A reader cannot tell whether the document is approved or awaiting sign-off, and §8.3 reads as a change-log. *Fix:* Either mark the status "approved with open confirmations" or resolve Q-C and Q-D. Rewrite §8.3 and §10.3 in neutral present tense, and move the review narrative out of the PRD.
- **low** Q-D is a bundle of about ten defaults confirmed as one item (§10.4). It cannot be answered selectively. *Fix:* Split it, or fold the items into §10.3 with one row each.

## Substance over theater — strong
Nothing here is furniture. Vision is specific to this product ("nobody can submit leave that overlaps other leave or exceeds the balance"). There are four personas, and each drives decisions (the Administrator self-dealing rules, the Manager's view limited to Available days, the Operator's bootstrap and recovery). NFRs carry numbers: p95 under 500 ms for 200 Employees, page size 25/100, a 15-minute token, 5 failures in 15 minutes, a 366-day span. There is no innovation section. The concurrency NFR names concrete races.

### Findings
- **low** The Front-end developer persona (§2.1) drives only SM-7 and the API-quality NFR, and the Operator has no JTBD line although FR-27, FR-29, UJ-6 and US-15 depend on that role. *Fix:* Add a one-line Operator JTBD, or note that the front-end developer is a consumer rather than a persona.
- **low** Several requirements prescribe mechanisms that belong to architecture, although §0 says design belongs there: "dummy password verification" (SEC-5), "serialised transaction" (FR-12, §6), "enforced by the database" (FR-1), "token family" (SEC-4, FR-8). *Fix:* Keep the behavioural outcome and move the mechanism to architecture notes.

## Strategic coherence — adequate
The thesis (the system enforces the rules so figures cannot drift and nobody can submit unviable leave) is validated by SM-2 (invariants under concurrency) and SM-3 (isolation matrix). Counter-metrics exist. Scope kind is clearly problem-solving, and the increment order follows dependencies. The weaker point is that no metric tests the user-outcome half of the JTBDs ("know quickly", "decide in one step").

### Findings
- **low** SM-C2 counterbalances "approval speed" (§9), but no SM measures approval speed. SM-1 is a completion test. *Fix:* Reword the counter-metric to guard against weakening checks to make journeys pass, or add a turnaround metric.
- **low** SM-6 (§9) is a process rule, not an outcome metric. SM-2 defers its iteration and parallelism counts to architecture, so it is not yet measurable.
- **low** No SM or test requirement covers SEC-1 to SEC-6 or SEC-14 to SEC-21. SM-3 covers only SEC-7 to SEC-13. *Fix:* Add a secondary SM for authentication and hardening, for example a negative test suite for throttling, hashing, headers and log scrubbing.
- **low** SM-1 says UJ-1 to UJ-7 complete "via the API", but UJ-6 and the UJ-7 edge case use operator operations that §4.1 and §4.2 may place outside the public API ("decided in architecture"). Its Validates list also omits FR-2, FR-10, FR-18 and FR-21, which UJ-1 to UJ-3 exercise. *Fix:* Say "via the API or the operator operation", and complete the list.

## Done-ness clarity — adequate (strong on rules, thin on refusal codes and field bounds)
Most FRs have bounded, testable consequences. Examples are FR-14 (shortfall named, 366 days, 2 years), FR-19 (Total down, Reserved down, Available unchanged, concurrent decisions succeed exactly once) and FR-25 (per-event list, atomic audit). The gaps follow.

### Findings
- **medium** Business-rule refusals mostly lack a status code, although SM-3 demands "the response codes defined in SEC-11". Codes are absent for the cases below. SEC-11 rule 5 only says "409 for a state conflict", so 400 versus 409 is a judgment call that testers and story writers will make inconsistently. *Fix:* Add a one-line "Refusal codes" list per FR (409 for state and rule conflicts, 400 for malformed input), or add an SEC-11 rule that says business-rule refusals are 409.
  - FR-3: self-as-manager (first sentence), cycle, inactive manager.
  - FR-1: nonexistent or inactive Direct Manager at creation.
  - FR-4: active Direct Reports.
  - FR-5: last Administrator.
  - FR-11: set-once violation.
  - FR-12: Total below Reserved.
  - FR-14: shortfall, no Direct Manager, inactive Direct Manager.
  - FR-15: overlap.
  - FR-16: cancelling a non-Pending request.
  - FR-2/FR-7: expired initial password. The refusal must not reveal that the email exists (SEC-5).
  - FR-8: expired or reused Refresh Token.
- **medium** Free-text maximum lengths are unspecified. FR-1 ("Name and email have maximum lengths"), SEC-14 and SEC-16 name no values for names, email, notes or reasons. The bounds in FR-14 and FR-11 show the PRD can do this. *Fix:* Give indicative maxima (for example name 200, email 254, note 500, reason 500) tagged `[ASSUMPTION]`.
- **low** Either/or consequences are not deterministic. FR-12: "both apply, or the second is refused with 409". FR-3: an Employee "cannot be their own Direct Manager" has no code. FR-19: when a request is both non-Pending and past its start date, the order of 409s is undefined. These are harmless but make test oracles loose.
- **low** The self-dealing 403s (FR-1, FR-3, FR-4, FR-5, FR-11, FR-12, FR-28) do not map cleanly onto a SEC-11 rule. Rule 2 is "role whose existence is not secret" and rule 4 is "may read the resource". *Fix:* Add "self-dealing prohibitions are 403" to SEC-11.
- **low** FR-11 ("set once ... while Total is 0") overlaps FR-12 (delta adjustment), so both can reach the same Total by different routes. If Total returns to 0 after adjustments, whether FR-11 may run again is ambiguous.
- **low** FR-17's consequences test the 404 and the manager name, but not the returned fields (status, decision time, decider, reason). FR-9 and FR-6 are similarly thin.
- **low** FR-28 does not say whether a deactivated Account may be reset. FR-29's "reactivates only if the operator explicitly asks" implies a deactivated sole Administrator, which FR-5 makes impossible through the API.

## Scope honesty — strong
§7 is thorough and explicit, and §10.2 is honest (L-1, L-8, L-9). De-scoping is surfaced in RV-4 rather than done silently. Assumptions are tagged and indexed. Open-items density (about 16 assumptions, 4 open items, 9 limitations) is fine for a low-stakes learning project.

### Findings
- **medium** Company Time Zone and Audit Retention Period are "configurable" and their changes are audited (FR-25, §6 "Changing the Company Time Zone affects only evaluations made after the change", FR-25 "validated at start-up"), but no FR, endpoint, user story or increment in §8.3 says who changes them or how. This is either a missing Administrator capability, or configuration read at start-up (in which case "changes" are audited only if start-up diffing is required). *Fix:* State which one it is. If configuration, say so and describe how the change audit is produced. If runtime, add an FR, a story and an increment slot.
- **low** Not listed as non-goals or limitations: self-service forgotten-password (only Administrator reset, FR-28), a "who am I" or own-profile read (FR-6 is Administrator-only apart from Direct Reports), and the fact that the only way to return days after an Approved leave is an Administrator adjustment. *Fix:* Add one line each to §7 or §10.2.
- **low** The 5-day Monday to Friday Working Day with a default zone of `Asia/Kolkata` is stated as fact in the Glossary and is not tagged as an assumption.

## Downstream usability — adequate to strong
IDs are contiguous and unique, the §4.8 note explains that FR-27, FR-28 and FR-29 are numbered out of order, and the US table maps every FR 1 to 29 at least once. Cross-references resolve (see Mechanical notes). Each UJ names a protagonist and carries inline context. The weaknesses are structural.

### Findings
- **medium** The increment order in §8.3 has forward dependencies. FR-3 reassignment (moves Pending requests) and FR-4 deactivation (cancels Pending requests, releases Reservations) sit in increment 2 but act on requests and Reservations created in increment 4. FR-1 creates a balance (Total 0), but the balance data arrives in increment 3. FR-17 (track status) is not placed in any increment. §8.3 notes that final slicing is a ticketing step, which softens but does not remove this. *Fix:* Split the side-effect consequences of FR-3 and FR-4 into the increment that introduces requests, and place FR-17 explicitly.
- **low** Out-of-order numbering invites range errors. FR-27 sits in §4.1 and FR-28 and FR-29 sit in §4.2, so "FR-1 to FR-6" and "FR-7 to FR-10" silently exclude them (the PRD flags this). L-9 appears before L-7 and L-8. The Q-series is split between §10.1 and §10.3.
- **low** The US table (§4.8) restates the FRs, and US-14 ("be prevented from seeing...") is a negative story with no distinct acceptance. SEC-1 to SEC-6, SEC-14 to SEC-21 and the NFRs map to no story.

## Shape fit — adequate
This is a multi-role internal tool. Seven UJs are on the heavy side, but UJ-5 to UJ-7 each justify themselves by anchoring a limitation or an operator flow. The US table duplicates the FR content, which is mild over-formalisation. It is acceptable because the PRD feeds ticketing. The capability-spec structure (FR consequences, SEC register, NFRs) suits an API-only product.

### Findings
- **low** The product is titled "Workforce Management" and the Vision says "wider workforce features can be added later", but the scope is leave only. This is harmless; the title may over-promise.

## Mechanical notes

**ID continuity and cross-references (all resolved unless noted).**
- FR-1 to FR-29 are all present and unique. UJ-1 to UJ-7, SEC-1 to SEC-21, SM-1 to SM-7 and SM-C1 to SM-C2, US-1 to US-18, AP-1 to AP-9, RV-1 to RV-4, L-1 to L-9, Q1 to Q10 (split between §10.1 and §10.3) and Q-A to Q-D are all present with no gaps.
- Every "Realizes UJ-n" in a feature or FR points to an existing UJ. §8.1 ranges cover all 29 FRs.
- The SM "Validates" lists and the US FR lists resolve. SM-1 omits FR-2, FR-10, FR-18 and FR-21, which its journeys exercise.
- **SEC-11 applied to the FRs.** No contradictions found. The FR-16 (403 for a Request Reader, otherwise 404), FR-17, FR-19 to FR-21, FR-24 and FR-6 (list 403, lookup 404) outcomes and FR-4's 401 all agree with the order. The FR-21 versus SEC-11 tie-break issue from the earlier review is fixed. SEC-11 rule 3 uses "Request Reader (or otherwise not entitled...)", and that term is defined only for Leave Requests. FR-6's Account lookup relies on the parenthetical. **low**
- **Section 10.1 "Where applied" accuracy.** Most entries are accurate. Discrepancies:
  - **low** AP-1 lists FR-19 but not FR-20, which "follows FR-21" and shares the same 404/403 behaviour.
  - **low** RV-1 cites SEC-4 (Refresh Token lifetimes), but the 7-day password expiry and lockout belong to SEC-5, SEC-2 and SEC-6. SEC-2 is not listed although RV-1 requires operator passwords to meet it.
  - **low** Q2 omits FR-19, which also uses Today.
  - **low** RV-3 omits FR-2 (the server-side must-change check on every call).
  - Checked and accurate: AP-2 to AP-9, RV-2 (SEC-12, FR-1, 3, 4, 5, 11, 12, 21, 24), RV-4, Q1, Q4, Q6, Q7, Q8.
- L-1 (Top-Most Person) versus FR-14: FR-14 says "whether that is a Top-Most Person or an Employee not yet assigned a manager", but the Glossary defines Top-Most Person as any Employee with no Direct Manager, which already includes the second case. **low**

**Assumptions Index roundtrip.** Every inline `[ASSUMPTION]` is indexed: Manager, FR-2 (both tags), FR-3, FR-11, FR-12, FR-14, FR-15 (working-day overlap, under "review-driven defaults"), FR-17, FR-25, SEC-2, SEC-4, SEC-5, and §6 (scale, page size). Reverse direction, entries with no matching inline tag:
- **low** "Recommended defaults Q3, Q5, Q9" have no inline tags, and only Q10 does (FR-17).
- **low** "Brief A1 to A4" is stale or duplicate. The Administrator-approval assumption is now approved (AP-4, RV-2), "Manager is derived" duplicates the first bullet, and the single-organisation and time-zone assumptions are resolved by Q2 and §7.
- **low** SEC-2's 128-character maximum has no `[ASSUMPTION]` tag.

**Glossary drift.**
- **low** Terms used but not defined: "must-change state" or "must-change flag" (FR-28, SEC-11, SEC-13), "Operator" (FR-29, UJ-6, US-15), and "recovery operation".
- **low** "request" (lowercase) is used throughout for "Leave Request". "Account" and "Employee" are used interchangeably in FR-3, FR-4 and FR-14 (for example "manager must be an active Account"). "Direct Manager" is defined by "the single Manager", while Manager is defined as the Direct Manager of at least one Employee, which is circular. The FR-15 consequence says the response "names the conflicting request", while UJ-1 says "the conflicting dates".
- **low** SEC-12 "No role can read passwords or tokens" sits next to FR-1/FR-2, where the Administrator reads the temporary password once. The intent (stored secrets) is clear, but the wording should be "stored passwords".

**Other minor.**
- **low** SEC-5 requires unknown emails to be throttled "the same", which needs some per-identifier key. FR-25 says no raw submitted identifier is stored. A keyed hash would reconcile them, but the PRD does not say so.
- **low** Anonymous failed-login audit writes (FR-25) are an unbounded write path beyond the per-source throttle. Worth a line in architecture.
- Required sections for the stakes and shape (Vision, users, UJs, Glossary, FRs, NFRs, Non-Goals, Scope, SMs with counter-metrics, open questions, Assumptions Index) are all present.

## Findings count by severity
- critical: 0
- high: 0
- medium: 6
- low: 31 (approximate, as counted per bullet)
