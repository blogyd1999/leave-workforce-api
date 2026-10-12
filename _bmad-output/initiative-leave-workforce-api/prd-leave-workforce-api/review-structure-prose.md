# Editorial Review: Structure and Prose Lenses

Skill: bmad-review (`lenses=structure,prose`), content class: docs. Style guide: Microsoft Writing Style Guide. Reader type: humans. Customization resolved from defaults (no overrides, no report_path). Word counts from `word_metrics.py`. Editorial lenses only: content and decisions are untouched; nothing was modified.

Plan: structure lens runs first on each document, then prose lens (skipping passages the structure pass tagged CUT, attaching fixes to surviving locations of MERGEs).

---

# Document 1: PRD (`prd-leave-workforce-api.md`, 8,253 words)

Read: this document exists to help the product owner and downstream architecture and ticketing work derive and verify the MVP's behaviour from one authoritative requirements source.

Structure model: Strategic/Context (Pyramid) for the frame, with Reference/Database (consistent FR schema, glossary) for the requirement body. The FR schema is consistent and should be preserved. The shape problems are mostly at the front (status and open items sit at the back) and in repetition across §4, §5, §10 and §11.

## Structure findings

| Pass | Original Text | Revised Text | Changes |
|---|---|---|---|
| structure | §0 Document Purpose (164 words) plus §10.3/§10.4 status ("awaiting owner confirmation") | MOVE a two-line status to the top of §0: "Draft. Awaiting owner confirmation of §10.3 defaults and §10.4 items; none blocks architecture, UX or ticketing." | Pyramid model puts status first; today the reader learns the PRD is unconfirmed only at §10.4. Adds ~25 words, but removes the closing "None of these blocks..." line in §10.4 (net ~0). |
| structure | §4.1 FR-27 placed after FR-6; §4.2 FR-28, FR-29 numbered after FR-27; notes in §4.2 ("FR-28 and FR-29 are numbered after FR-27 and belong to §4.2") and §4.8 ("FR-27 is numbered out of order... ranges such as 'FR-1 to FR-6' do not include it") | MOVE or RENUMBER: either renumber in reading order, or keep IDs and delete the two apology notes by listing the IDs per feature in each Description (e.g. "FR-1 to FR-6, FR-27"). §8.1 already does the latter. | Out-of-order IDs force three explanatory notes and make every range ("FR-7 to FR-10", "FR-13 to FR-17") a trap. Stable IDs are defensible, so this is a QUESTION on renumbering and a CUT on the notes if IDs stay (saves ~55 words). |
| structure | §4.5: FR-19 and FR-20 each say "Response codes follow FR-21"; FR-21 "Decision authority" comes after them | MOVE FR-21 before FR-19 (or fold its rules into FR-19/FR-20 consequences) | The two decide operations depend on rules defined later. Dependency-first ordering. No word impact. |
| structure | §10.1 Approved decisions table (528 words): the Q1, Q2, Q4, Q6, Q7, Q8 rows restate content already stated in FR-14, §6, FR-25/26, FR-12, SEC-20, FR-27; AP-5 restates SEC-1 to SEC-6; RV-3, RV-4 restate SEC-13 and FR-14/19 | CONDENSE each row to: ID, one-line topic, "Where applied". Keep the decision text only where no FR carries it (AP-1, AP-9, Q1, Q3-style rationale). | The table duplicates requirement text and can drift from it. Decision log value is the ID-to-location trace, not a second copy. Saves ~150 words. |
| structure | §10.3 Recommended defaults (156 words) and §10.4 items Q-C, Q-D | MERGE Q-C into the §10.3 heading note ("awaiting confirmation"), and MERGE Q-D into §10.3 as additional rows or a list; leave §10.4 with Q-A and Q-B only | Q-C only says "confirm §10.3"; Q-D lists defaults that are also tagged `[ASSUMPTION]` inline and indexed in §11. Three places hold the same "please confirm" signal. Saves ~60 words. |
| structure | §11 Assumptions Index (198 words) | QUESTION: keep as the single owner-facing list but CONDENSE to ID-and-location lines (e.g. "FR-2: initial password, 7-day expiry"), dropping restated rule text | §0 promises the index, and indexes aid review (PRESERVE the section). But each bullet re-explains the rule; inline tags and §10.4 Q-D already carry it. Saves ~80 words. |
| structure | §5 SEC-6, SEC-9, SEC-10, SEC-12 (second half), SEC-19 | MERGE: SEC-6 into FR-4/FR-9/FR-10 references; SEC-9 and SEC-10 into SEC-8; SEC-19 into a pointer to FR-25/FR-26; SEC-12's self-dealing list into a pointer to RV-2/FR-3,4,5,11,12 | The same rule appears as an FR consequence and a SEC line (self-dealing list appears in SEC-12, RV-2, FR-3, FR-4, FR-5, FR-11, FR-12). One source of truth per rule. Saves ~110 words. PRESERVE SEC-11 (ordered response-code policy), which is the canonical statement and is well structured. |
| structure | §6 Correctness under concurrency (single ~120-word paragraph) | CONDENSE into a short lead sentence plus a bullet list of the six races | The densest block in the document is the one most likely to be tested (SM-2 references it). Whitespace and scanability, not word savings (about neutral). |
| structure | §8.1 In Scope (46 words) and §8.2 "Everything in §7" plus deferred list (31 words) | CONDENSE §8.1 to a pointer table (feature group to FR range) already implied by §4 headings; CUT the first sentence of §8.2 and keep only the deferred-value ordering | §8.1 repeats the §4 feature groupings; §8.2 repeats §7. Saves ~40 words. |
| structure | Known-limitation L-1 stated in UJ-5, FR-14 (twice), §7, §10.1 AP-6/Q1, §10.2 L-1, §10.3 Q3 | CONDENSE FR-14's second sentence and §7's bullet to "see L-1" pointers; keep UJ-5 and L-1 in full | Seven restatements of one decision. Saves ~40 words. |
| structure | §2.3 Key User Journeys (709 words) | PRESERVE | Looks long, but the journeys give concrete examples and edge cases that the requirements map back to; reader's journey and examples are comprehension aids. |
| structure | §4.8 User Stories table (499 words) | PRESERVE | Consistent schema and the only story-to-FR trace; useful for ticketing. Mild QUESTION: US-5 and US-14 use ranges ("FR-7 to FR-10", "SEC-7 to SEC-13") that depend on the numbering issue above. |
| structure | §3 Glossary (510 words) placed after §2.3 journeys that already use its terms | QUESTION: MOVE the Glossary ahead of §2.3 (or keep and add "terms in §3" to §2.3's intro) | Scaffolding: terms (Direct Manager, Reservation) are used before defined. Low impact since §0 points to §3. |

Structure summary: 11 recommendations (9 actionable, 2 PRESERVE) plus 2 QUESTIONs counted among them. Estimated reduction if all accepted: about 535 words, roughly 6.5% of 8,253 (estimate from section counts; offset by about 25 words added for the status line). No length target was supplied. Comprehension trade-offs: merging SEC lines into FR pointers makes the §5 security section less self-contained for a security reviewer reading it alone; condensing the §10.1 table removes the at-a-glance decision text.

## Prose findings

| Pass | Original Text | Revised Text | Changes |
|---|---|---|---|
| prose | §10.4 Q-D "Confirm the review-driven defaults I applied"; §10.3 heading "awaiting your confirmation"; AP-9 "shown in §8.3 for your confirmation" | Consider: "Confirm the review-driven defaults applied in this draft"; "Recommended defaults awaiting owner confirmation"; "shown in §8.3 for owner confirmation" | The PRD otherwise speaks in third person ("the owner"); first and second person addresses a single reader inside a document meant for several. Same issue in three places. |
| prose | SEC-3 "Signing algorithm pinning, issuer and audience validation, and key rotation are decided in architecture and must be decided there." | "Signing algorithm pinning, issuer and audience validation, and key rotation must be decided in architecture." | Removed redundant repetition. |
| prose | SEC-3 "short-lived, with a lifetime of 15 minutes"; AP-5 and Brief D10 "about 15 minutes" | "Access Tokens expire after 15 minutes (plus bounded clock skew)." Consider aligning AP-5 and the brief to the same wording ("15 minutes") | Removes the redundant "short-lived, with a lifetime of" and resolves the "about" versus exact mismatch across documents. |
| prose | FR-1 "the Administrator role or not" | "whether the Account has the Administrator role" | Clearer and avoids the dangling "or not". |
| prose | FR-4 "Reactivation restores login only; it does not restore cancelled requests. It is refused while the Account's Direct Manager is not active" | "Reactivation restores login only; it does not restore cancelled requests. Reactivation is refused while ..." | "It" is ambiguous between reactivation and the cancelled requests. |
| prose | "one-time password" (UJ-7, FR-1 via "one-time initial password"), "single-use" (FR-2, FR-28), "initial password" | Consider: use "single-use" and "initial password" everywhere, "reset password" for FR-28 | Three labels for the same concept; the Glossary demands exact terms but does not define these. |
| prose | FR-25 "activation or deactivation"; FR-4 "reactivate"/"Reactivation" | "deactivation or reactivation" | Terminology consistency with FR-4. |
| prose | "Realizes UJ-n" (US spelling) alongside "organisation", "normalised", "anonymisation", "Authorisation" (UK spelling) | Consider: choose one variant. Microsoft Writing Style Guide uses US English ("Realizes", "organization", "normalized", "anonymization"); if UK is intentional, change "Realizes" to "Realises". | Mixed spelling variants throughout. |
| prose | "Mon-Fri" (UJ-1, brief), "Monday-Friday" (FR-14, FR-15), "Monday to Friday" (Glossary) | Consider: "Monday to Friday" in prose; "Mon-Fri" only inside the journey narrative if desired | Three forms of one term. |
| prose | UJ-7 Climax "Dev is back in and chooses a new password; old sessions were already revoked." | "...; old sessions are revoked." | Tense shift mid-sentence. Same pattern: UJ-6 "through a non-logged channel" - Consider: "unlogged channel" or "a channel that is not logged" (FR-27 wording). |
| prose | FR-19 "A request that is not Pending cannot be approved." and later "A request that is not Pending returns 409." | MERGE: "A request that is not Pending cannot be approved (409)." | Same rule stated twice in one requirement. |
| prose | §8.3 "The order approved earlier is kept, with two dependency corrections for review:" and AP-9 "Incremental delivery order." (approved) | Consider: "The previously approved order is kept, with two proposed dependency corrections awaiting confirmation:" | Wording says both "approved" and "for review"; status is ambiguous at first read. |
| prose | UJ-5 title "hits the top-of-chart limit" | Consider: "cannot submit leave because she has no Direct Manager" | "top-of-chart" is informal and is not a Glossary term (Top-Most Person is). |
| prose | §1 and §2.1 lowercase "account", "balance" versus Glossary-capitalised "Account", "Leave Balance" | Consider: leave as is in §1 (narrative) and JTBD quotes; capitalise defined terms in §0-adjacent and requirement text | Glossary instructs exact use; narrative sections are plausibly an intentional exception. Low confidence. |

Prose: 13 rows. Tone and voice noted as intentional: terse, imperative requirement style, `[ASSUMPTION]` tags, bold FR/UJ labels; preserved.

---

# Document 2: Product Brief (`brief-leave-workforce-api.md`, 1,394 words)

Read: this brief exists to help the product owner and later PRD/architecture authors confirm what the MVP is, why, and what it excludes.

Structure model: Strategic/Context (Pyramid). The first half (Summary, Problem, Solution, Serves, Success, Scope) follows the model well. The tail (Decisions, Limitations, Assumptions, Defaults, Open Questions) is fragmented and partly out of scope for a brief.

## Structure findings

| Pass | Original Text | Revised Text | Changes |
|---|---|---|---|
| structure | Decisions (owner-approved), D1 to D18 table (381 words) | CONDENSE to a one-paragraph pointer to PRD §10.1 plus at most the decisions not otherwise visible in the brief, or CUT and rely on the existing "Recorded in the PRD (§10.1)" line | Scope violation: this is the PRD's decision log duplicated with different IDs (D1-D18 here, AP/RV/Q there), so the two can drift and readers must cross-map. D5, D4, D11, D12 also repeat Solution bullets; D2, D3, D6, D7 repeat Scope. Saves ~250-330 words. |
| structure | Known Limitations first bullet (leave for no-Direct-Manager) and Scope "Out" bullet "Leave for anyone without a Direct Manager..." | MERGE: keep the limitation in Known Limitations; in Scope "Out" write "Leave for anyone without a Direct Manager (see Known Limitations)" | Same fact twice, about 30 words apart in reading distance. Saves ~20 words. |
| structure | Assumptions (45), Recommended Defaults Awaiting Confirmation (49), Open Questions (24) | MERGE into one section "Assumptions, Defaults and Open Questions" with three short lists | Three tiny sections with the same "unconfirmed" role; one section reduces fragmentation. Saves ~15 words (headings). |
| structure | Vision (40) after Scope; "What Makes This Different" ends "...grow into wider workforce management (more leave types, holiday calendars, reporting) without a rewrite." | MERGE the growth sentence into Vision, or move Vision before Scope | The growth idea appears twice; Vision is conventionally read before scope detail. Saves ~20 words. |
| structure | "recorded ... in each `.memlog.md`" (Decisions intro) | QUESTION: reference points to working files a brief reader cannot open or does not know | Dangling reference; either cite the path or remove. |
| structure | Exec Summary, Problem, Solution, Who This Serves, Success Criteria, Scope | PRESERVE | Short, front-loaded, and each earns its place; Solution bullets are the brief's best density. |

Structure summary: 6 recommendations (4 actionable, 1 QUESTION, 1 PRESERVE). Estimated reduction if all accepted: about 305-385 words, roughly 22-28% of 1,394 (estimate from section counts, mostly the Decisions table). Trade-off: cutting D1-D18 removes the brief's at-a-glance decision summary; the PRD pointer keeps the content reachable but not in the same document.

## Prose findings

| Pass | Original Text | Revised Text | Changes |
|---|---|---|---|
| prose | Scope "Out" bullets: "...(a known MVP limitation);" followed by a bullet ending with a period | "...(a known MVP limitation)." | Inconsistent terminal punctuation (stray semicolon). |
| prose | "direct manager" / "Direct Manager" (Scope "Out" bullet uses "Direct Manager"; rest lowercase); "top-most person"; "administrator" lowercase in Solution, "Administrator" elsewhere | Consider: capitalise Employee, Manager, Administrator, Direct Manager, Top-Most Person consistently with the PRD Glossary, or lowercase all in the brief | Same terms capitalised inconsistently within one document and across the pair. |
| prose | Exec Summary "The project is also a deliberate, incremental build: the platform is ASP.NET Core on .NET 10 as a modular monolith with PostgreSQL, each increment built and tested before the next." | Consider: "The project is also a deliberate, incremental build on ASP.NET Core (.NET 10), structured as a modular monolith with PostgreSQL; each increment is built and tested before the next." | The colon introduces a sentence whose subject switches from the project to the platform; run-on list. |
| prose | Decisions D10 "access token about 15 minutes" | Consider: "access tokens expire after 15 minutes" (match PRD SEC-3) | "about" conflicts with the PRD's exact value. |
| prose | Decisions D3 "repeat-safe bootstrap operation"; Open Questions "A policy for the top-most person's leave, if it becomes a real need." | Consider: "a bootstrap operation that refuses repeat runs"; "Whether to define a policy for ..." | "repeat-safe" is jargon not used outside this project; the open question is a fragment where the sibling item is a full topic phrase. Low confidence. |

Prose: 5 rows. Tone noted as intentional: plain, candid ("Nothing here is novel"); preserved.

---

# Counts

| Document | Structure rows | Prose rows |
|---|---|---|
| PRD | 13 (11 actionable/QUESTION, 2 PRESERVE of which §2.3 and §4.8) | 13 |
| Brief | 6 (5 actionable/QUESTION, 1 PRESERVE group) | 5 |

Overlap between lenses and documents: the "single-use / one-time" and "15 minutes / about 15 minutes" terminology issues span both documents; the decision log is duplicated across the brief (D1-D18) and the PRD (§10.1, with different IDs). The PRD's own "awaiting confirmation" signal is repeated in §10.3, §10.4 and §11.
