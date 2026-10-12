---
title: Leave and Workforce Management API
status: final
created: 2026-10-12
updated: 2026-10-12
---

# PRD: Leave and Workforce Management API

## 0. Document Purpose

This PRD defines the MVP of a backend-only Leave and Workforce Management API. It is written for the product owner and for the downstream architecture and ticketing work. It builds on `brief-leave-workforce-api` and does not repeat its narrative. Vocabulary is anchored in the Glossary (§3). Features are grouped in §4 with functional requirements (FR-n) nested and numbered globally. Security requirements are in §5. §10 separates **approved decisions**, **known limitations** and **open questions**. Assumptions are tagged `[ASSUMPTION]` inline and indexed in §11.

This is a fictional learning project, so the MVP favours a clear, testable core over exhaustive enterprise policy. Where a policy was deliberately kept simple, §10.2 records it as a known limitation. It makes no claim of legal or regulatory compliance (§10.1, Q7).

Technology constraints given by the owner: ASP.NET Core Web API on .NET 10, modular monolith, PostgreSQL with EF Core, JWT access and refresh tokens. A React front end and Azure deployment come later. These are recorded as constraints; their design belongs to architecture, not this PRD. Values marked "indicative" may be refined in architecture without changing the requirement.

## 1. Vision

Give one organisation a single, trustworthy system for employee leave. Employees request leave and always know where it stands and what balance remains. Managers get a clear queue and decide with the facts in front of them. Administrators keep accounts and balances correct, and creating further Administrators needs a second Administrator. The system enforces the rules itself, so the figures cannot drift and nobody can submit leave that overlaps other leave or exceeds the balance.

The MVP is one narrow path done properly: provision an account, log in, request leave, receive a decision, see the balance change, and leave an audit trail behind. It is exposed as an API first so a React front end can follow without rework, and structured as modules so wider workforce features can be added later.

## 2. Target User

### 2.1 Jobs To Be Done

- **Employee:** "When I need time off, I want to ask once, know quickly whether it is approved, and trust my remaining balance."
- **Manager:** "When a report asks for leave, I want to see the dates, the days and the balance impact, and decide in one step."
- **Administrator:** "When someone joins, moves team or leaves, I want accounts, managers and balances to be right, and I want every change to be explainable later."
- **Operator:** "When the system is first deployed, or access is lost, I want a safe, documented way to issue passwords and recover access without hardcoded secrets."
- **Front-end developer (secondary):** "I want a stable, documented API contract so I can build the React app against it."

### 2.2 Non-Users (v1)

Contractors without accounts, HR analysts needing reports, payroll systems, anyone who is not provisioned by an Administrator, and anonymous public users.

### 2.3 Key User Journeys

- **UJ-1. Asha books a week off and watches it get approved.**
  - **Persona + context:** Asha, a developer with 12 days of balance, reports to Ravi.
  - **Entry state:** account provisioned; Asha has already replaced her temporary password at first login.
  - **Path:** she logs in; views her balance (12 available); submits a request for Monday to Friday next month (5 working days); sees it as Pending with 7 days available; Ravi approves; she sees Approved and a balance of 7 with 0 reserved.
  - **Climax:** the status flips to Approved and the balance reflects it without anyone emailing her.
  - **Resolution:** the request is in her history with the decision time and Decider.
  - **Edge case:** she tries a second request overlapping the first. It is refused with the conflicting dates named.

- **UJ-2. Ravi rejects a request with a reason.**
  - **Persona + context:** Ravi, a manager with four reports, is preparing a release.
  - **Entry state:** authenticated as a Manager.
  - **Path:** he lists pending requests from his direct reports; opens one; sees the employee's name, dates, working days and available days; rejects it with the reason "release week".
  - **Climax:** the request becomes Rejected and the reserved days return to the employee's balance.
  - **Resolution:** the employee sees the status and the reason.
  - **Edge case:** he tries to decide a request that belongs to another manager's report. The system answers as if the request does not exist.

- **UJ-3. Meera sets up a new joiner.**
  - **Persona + context:** Meera, an Administrator, onboards Dev. Karan is the operator.
  - **Entry state:** Meera is authenticated as an Administrator.
  - **Path:** she creates Dev's Account, assigns Ravi as manager, and sets an opening balance of 10 days. Dev's Account is Awaiting Password. Karan checks that he is handing over to Dev, runs the operator operation that issues a temporary password, and gives it to Dev.
  - **Climax:** within 24 hours Dev logs in with it, immediately chooses a new password, and sees 10 days available.
  - **Resolution:** the account, manager link, balance and password issuance each have an audit record.
  - **Edge case:** Dev does not log in within 24 hours. The temporary password expires, the Account stays Awaiting Password, and Karan issues a new one.

- **UJ-4. Asha changes her mind.**
  - **Persona + context:** Asha submitted a request that is still pending and realises the dates are wrong.
  - **Entry state:** authenticated as an Employee with one Pending request.
  - **Path:** she cancels the request; the reserved days return; she submits a new request with the correct dates.
  - **Climax:** the old request shows Cancelled and the new one Pending.
  - **Resolution:** both appear in her history.
  - **Edge case:** after approval she can no longer cancel through the API in the MVP, and she cannot edit a submitted request at all.

- **UJ-5. Priya, the founder, hits the top-of-chart limit.**
  - **Persona + context:** Priya is the Top-Most Person and has no Direct Manager.
  - **Entry state:** authenticated as an Employee with no Direct Manager.
  - **Path:** she tries to submit a Leave Request.
  - **Climax:** the system refuses with a clear reason: no Direct Manager is assigned.
  - **Resolution:** she takes leave outside this system in the MVP (known limitation L-1).

- **UJ-6. Deploying the system for the first time.**
  - **Persona + context:** Karan, the operator, has an empty database and needs the first Administrator.
  - **Entry state:** no Administrator Account exists.
  - **Path:** he explicitly invokes the Bootstrap Operation; the first Administrator Account is created in the Awaiting Password state and a temporary password is issued, shown to Karan once on a non-logged channel.
  - **Climax:** the Administrator logs in within 24 hours, replaces the password, and can provision everyone else.
  - **Resolution:** the operation is audited.
  - **Edge case:** Karan runs the bootstrap again by accident. It is refused and nothing changes.

- **UJ-7. Meera helps Dev who is locked out.**
  - **Persona + context:** Dev forgot the password after a holiday.
  - **Entry state:** Dev cannot log in; Meera is an authenticated Administrator.
  - **Path:** Meera requests a password reset for Dev. Dev's Account becomes Awaiting Password and Dev's sessions end. Karan verifies Dev and issues a temporary password; Dev logs in and must change it.
  - **Climax:** Dev is back in and chooses a new password.
  - **Resolution:** the reset and the issuance are audited.
  - **Edge case:** Meera tries to reset another Administrator, or her own Direct Manager. The system refuses (403). An Administrator's password is reset only by Karan's recovery operation (FR-29).

- **UJ-8. Meera adds a second Administrator, then a third.**
  - **Persona + context:** Meera is the only Administrator. Arun and Tara are Employees who will become Administrators.
  - **Entry state:** Meera is authenticated; only one Administrator Account has ever existed.
  - **Path:** Meera makes Arun an Administrator directly, which the single-Administrator window allows once. Later she initiates a Privileged Request to make Tara an Administrator; Arun reviews it and approves.
  - **Climax:** on approval Tara becomes an Administrator in one step, and both actions are audited.
  - **Resolution:** from now on every creation or grant of an Administrator shows distinct Initiator and Approver.
  - **Edge case:** Meera tries to approve her own request, or to grant a fourth Administrator directly. Both are refused (403).

## 3. Glossary

*Downstream workflows must use these terms exactly.*

- **Account** — the login identity of a person in the system. One person has one Account. Has an active or deactivated state.
- **Employee** — a person with an Account who can take leave. Every Manager and every Administrator is also an Employee in the data model; whether they can take leave depends on having a Direct Manager.
- **Direct Manager** — the Employee an Administrator has assigned as a given Employee's manager. Each Employee has at most one, and the assignee must be active when assigned.
- **Direct Report** — an Employee whose Direct Manager is a given Employee.
- **Manager** — an Employee who is the Direct Manager of at least one active Employee. Manager capability is derived from that relationship, not stored as a role. `[ASSUMPTION]`
- **Administrator** — a person who manages Accounts, manager assignments and Leave Balances and reads Audit Records. The Administrator role confers no right to decide Leave Requests. Stored as a role. 
- **Operator** — the person with deployment and host access who runs Operator Operations. Not an Account and not a role in the API. `[ASSUMPTION: the Operator is trusted; operator identity is taken from host-level access, details in architecture.]`
- **Operator Operation** — an explicitly invoked operation run outside the API, with no HTTP endpoint: bootstrap (FR-27), temporary password issuance (FR-30) and Administrator recovery (FR-29).
- **Decider** — the Manager who approved or rejected a Leave Request. Recorded on the request and kept even if the Direct Manager later changes.
- **Top-Most Person** — an Employee with no Direct Manager, whether they were created without one or had it cleared.
- **Awaiting Password** — the Account state in which the Account has no password of its own: none has been set, or only a Temporary Password is outstanding. An Awaiting Password Account cannot log in with a password; it can log in once with a live Temporary Password (FR-2). It leaves this state only when change-password succeeds.
- **Temporary Password** — a password issued by an Operator Operation, with at least 128 bits of cryptographic randomness, valid for one login within 24 hours of issue. It is live until it is used, expires, or is voided by a newer issue, a reset, a deactivation or a reactivation.
- **Must-Change Session** — the session created by a login with a Temporary Password. It holds only a short Access Token and no Refresh Token, and may call only change-password and logout. It is a property of the session, not of the Account.
- **Single-Administrator Window** — the period before a second Administrator has ever existed. It is a persisted one-way flag, not derived from current roles or from Audit Records, and it is set closed when a second Administrator exists and never reopened.
- **Privileged Request** — a request to grant the Administrator role to an existing active Account, held until a different Administrator approves it (FR-31).
- **Initiator** — the Administrator who creates a Privileged Request.
- **Approver** — an active Administrator, different from the Initiator and from the target Account, who approves or rejects a Privileged Request. "Active Administrator" means an Administrator whose Account is active.
- **Leave Type** — a category of leave. The MVP has exactly one, generic.
- **Working Day** — a day Monday to Friday. Weekends are not counted. There is no public-holiday calendar in the MVP.
- **First Working Day** — the earliest Working Day in a Leave Request's range, start date included.
- **Company Time Zone** — the IANA time zone used to decide what "today" is. Set by Operator configuration; the default is `Asia/Kolkata`.
- **Today** — the current calendar date in the Company Time Zone.
- **Leave Request** — an Employee's request for a continuous range of whole Working Days, with a start date, an end date and an optional note.
- **Requested Days** — the number of Working Days in a Leave Request's range, inclusive of start and end.
- **Request Status** — one of Pending, Approved, Rejected, Cancelled.
- **Leave Balance** — an Employee's days, held as three whole-number figures: Total, Reserved and Available. Available = Total minus Reserved.
- **Total** — the days an Employee holds, set by an Administrator and reduced when leave is approved.
- **Reserved** — days held by the Employee's Pending requests.
- **Reservation** — the hold placed on Requested Days when a request is submitted.
- **Balance Adjustment** — an Administrator's change to Total, always with a reason.
- **Audit Record** — an entry, immutable except for the retention process, naming the actor, action, target, UTC time and relevant before-and-after values. Distinct from the request status history shown to Employees and Managers (FR-22).
- **Security-Class Event** — an Audit Record for an Account, role, manager, password, session-security, Privileged Request, Operator Operation, balance, configuration or retention-purge event. All others, including login success, Leave Request events and audited reads, are Ordinary Events.
- **Ordinary Retention Period** — how long Ordinary Events are kept. Set by Operator configuration; default one year.
- **Security Retention Period** — how long Security-Class Events are kept. Set by Operator configuration; default three years, and always longer than the Ordinary Retention Period.
- **Request Reader** — a caller entitled to read a given Leave Request: its owning Employee, that Employee's current Direct Manager, the Decider, or an Administrator.
- **Access Token** — a short-lived JWT proving who the caller is and which session it belongs to. Authorization does not rely on any role it carries (SEC-13).
- **Refresh Token** — a longer-lived, single-use credential exchanged for a new Access Token and a new Refresh Token.

## 4. Features

### 4.1 Account and Organisation Management

**Description:** Accounts are provisioned only by an Administrator; there is no public sign-up, so "registration" means Administrator account creation. The first Administrator is created by the Bootstrap Operation (FR-27). An Account has no password until the Operator issues a Temporary Password (FR-30); the API never returns a password. Creating an Administrator or granting the role is limited by the Single-Administrator Window and then by two-person approval (FR-31). There are no hard deletes. Realizes UJ-3, UJ-6, UJ-8.

**Functional Requirements:**

#### FR-1: Create an Account
An Administrator can create an Account with name, unique work email, an optional Direct Manager and an optional Administrator role flag. The Account is created in the Awaiting Password state. The Administrator role flag is accepted only during the Single-Administrator Window (FR-31); afterwards it is refused (403). Realizes UJ-3.

**Consequences (testable):**
- A caller without the Administrator role receives 403.
- An Administrator cannot create an Account with themselves as its Direct Manager (403).
- Email uniqueness is case-insensitive on the trimmed, normalised address, enforced by the database; a duplicate is refused (409) and no Account is created.
- Name has at most 100 characters and email at most 254, stored as plain text; longer input is refused (400).
- The creation response contains no password and no credential of any kind.
- An Audit Record exists for the creation.

**Out of Scope:** Self-registration; bulk import; invitation email.

#### FR-2: Temporary password and first login
An Account that is Awaiting Password can log in once, with a live Temporary Password issued by the Operator (FR-30), to complete a mandatory password change.

**Consequences (testable):**
- A Temporary Password works for one login only and expires 24 hours after issue. Login with an expired, used or voided Temporary Password returns the same generic 401 as any other login failure (FR-7).
- A login with a live Temporary Password consumes it and creates a Must-Change Session: an Access Token only, with the normal 15-minute lifetime and no Refresh Token. Refresh is refused (401). The session can call only change-password and logout; any other call is refused with 403 and a stable error code.
- A successful change-password in a Must-Change Session moves the Account out of Awaiting Password and returns a normal session (Access Token and Refresh Token). The new password must satisfy SEC-2 and must differ from the Temporary Password.
- If the Must-Change Session ends or expires before the password is changed, or the Temporary Password is not used within 24 hours, the Account simply remains Awaiting Password with no live Temporary Password. Nothing needs to trigger a state change, and the Operator can issue a new one at any time (FR-30).
- Issuing a new Temporary Password, a reset (FR-28 or FR-29), a deactivation and a reactivation each void any live Temporary Password and end any Must-Change Session.

#### FR-3: Assign, reassign or clear a Direct Manager
An Administrator can set, change or clear an Employee's Direct Manager. Realizes UJ-3.

**Consequences (testable):**
- An Employee cannot be their own Direct Manager. An Administrator cannot assign themselves as another Employee's Direct Manager, including at Account creation (FR-1), and cannot change or clear their own Direct Manager (403).
- A reporting cycle (A manages B manages A, directly or transitively) is refused (409).
- The manager must be an active Account (409 otherwise). The check is part of the same transaction as the write.
- On reassignment, the Employee's Pending requests move to the new Direct Manager; Approved and Rejected requests keep their Decider.
- Clearing requires a reason of at most 500 characters. It makes the Employee a Top-Most Person, who cannot submit leave (L-1). Clearing cancels the Employee's Pending requests and releases their Reservations in the same transaction, recorded with the Administrator as actor and the reason "manager cleared".
- Every reassignment and clearing is audited with the old and new manager. No Employee loses a Direct Manager except through an explicit, audited clearing.

#### FR-4: Deactivate and reactivate an Account
An Administrator can deactivate an Account and later reactivate it.

**Consequences (testable):**
- A deactivated Account cannot log in and its sessions and Refresh Tokens are revoked immediately; its Access Token is rejected with 401 on its next use (SEC-13).
- Its history is retained and remains visible to those who could see it before.
- An Administrator cannot deactivate their own Account (403). The last active Administrator cannot be deactivated (409).
- Deactivating an Account that still has active Direct Reports is refused (409). The response states how many active Direct Reports remain. The Administrator must first reassign or clear each one (FR-3); nothing is changed automatically.
- Deactivation cancels the Account's own Pending requests and releases their Reservations in the same operation; if any step fails, nothing changes. Each cancellation is recorded with the deactivating Administrator as actor and the reason "account deactivated".
- Reactivation returns the Account to the Awaiting Password state, because deactivation invalidates its password, and voids any live Temporary Password. It does not restore cancelled requests and is refused (409) while the Account's Direct Manager is not active, until an Administrator reassigns or clears it (FR-3).
- Deactivating or reactivating another Administrator is a single-Administrator action in the MVP. It is audited as a Security-Class Event and recorded as limitation L-9.

#### FR-5: Manage the Administrator role
An Administrator can grant or remove the Administrator role on an Account. Granting is governed by FR-31; removal is a single-Administrator action.

**Consequences (testable):**
- The last active Administrator cannot lose the role (409). The check is serialised.
- An Administrator cannot change their own roles (403).
- Every change is audited as a Security-Class Event. Removing another Administrator's role is recorded as limitation L-9.

#### FR-6: View accounts
An Administrator can list and view Accounts including role, manager and active state. A Manager has a separate list of their own Direct Reports.

**Consequences (testable):**
- A caller without the Administrator role receives 403 on every Account list and Account lookup by identifier; non-Administrators have no lookup of other Accounts.
- The Direct Report list is scoped to the caller, shows each active and deactivated Direct Report's name and email only, and is empty for a caller with no Direct Reports.

#### FR-27: Bootstrap the first Administrator
The system provides an explicitly invoked Operator Operation that creates the first Administrator Account. Realizes UJ-6.

**Consequences (testable):**
- No default Administrator exists in source, configuration or migrations, and no password is hardcoded anywhere.
- The Account is created in the Awaiting Password state and a Temporary Password is issued under FR-30, shown only to the Operator on a channel that is not logged.
- The operation succeeds only when no Administrator Account exists. Running it again, including concurrently, creates nothing and returns a clear refusal. Initialization is atomic.
- Once an Administrator exists, any leftover bootstrap input is ignored.
- The operation has no HTTP endpoint.
- An Audit Record exists for the initialization, and a refused repeat attempt is also recorded.

**Notes:** Whether the Bootstrap Operation is a command-line operation or another mechanism is decided in architecture; this requirement fixes only its behaviour.

#### FR-31: Creating or granting an Administrator
Making a person an Administrator is limited as follows. After the Single-Administrator Window closes, an Administrator is made by creating an ordinary Account (FR-1) and then granting the role through a Privileged Request. Realizes UJ-8.

**Consequences (testable):**
- During the Single-Administrator Window, the sole Administrator may create an Account with the Administrator role flag (FR-1) or grant the role to an existing Account (FR-5) directly, once. This is audited. The window is then closed for good, and two concurrent attempts succeed once with the loser receiving 409.
- After the window closes, a direct create-with-flag (FR-1) or direct grant (FR-5) is refused with 403 and a stable error code. The only way to grant the role is a Privileged Request.
- An Administrator creates a Privileged Request naming an existing active Account. A different active Administrator, who is not the target, approves or rejects it. Administrators can list and read pending Privileged Requests; a caller without the Administrator role receives 403, and a request that does not exist returns 404.
- The Initiator cannot approve or reject their own request, and the target cannot be Initiator or Approver (403). A request that is not pending, or has expired, returns 409 on approval.
- A Privileged Request not approved within 24 hours expires by a system process and has no effect. Approval executes the grant in the same transaction, after re-checking that Initiator, Approver and target are still active and valid. If anything has changed, approval fails with 409 and changes nothing. Two concurrent approvals succeed once.
- Every step (created, approved, rejected, expired, executed) is a Security-Class Event naming the Initiator and the Approver, or the system process for expiry.

**Notes:** Resetting an Administrator's password is not part of this requirement; see FR-29. If role removal or deactivation leaves one active Administrator after the window has closed, the Operator can restore an Administrator through FR-29 (L-12).

### 4.2 Authentication and Sessions

**Description:** Login with email and password issues an Access Token and a Refresh Token. Refresh Tokens rotate on each use. Passwords are issued by Operator Operations, never by the API. Realizes UJ-1, UJ-2, UJ-3, UJ-7.

**Functional Requirements:**

#### FR-7: Log in
An active Account can log in with email and its password, or with a live Temporary Password (FR-2),  and receive an Access Token and a Refresh Token.

**Consequences (testable):**
- Wrong email, wrong password, an expired, used or voided Temporary Password, an Awaiting Password Account without a live Temporary Password, and a deactivated Account all return the same generic 401.
- A login with a live Temporary Password follows FR-2.
- Repeated failures trigger the progressive back-off and throttling in SEC-5, answered with the same 429 for known and unknown emails.

#### FR-8: Refresh the session
A caller can exchange a valid Refresh Token for a new Access Token and a new Refresh Token; the old Refresh Token is then invalid.

**Consequences (testable):**
- Reusing an already-used Refresh Token is refused and revokes that token family, forcing a new login.
- A repeat of the same refresh within the short grace window returns the same successor pair, never a second one.
- An expired or revoked Refresh Token is refused. A Must-Change Session has no Refresh Token (FR-2).

#### FR-9: Log out
A logged-in caller can end their current session.

**Consequences (testable):**
- After logout the session's Refresh Token cannot be used and its Access Token is rejected with 401 on its next use (SEC-24).

#### FR-10: Change own password
A caller can change their own password by supplying the current one. In a Must-Change Session the session that consumed the Temporary Password is the proof, no current password is needed, and FR-2 applies.

**Consequences (testable):**
- Success ends all of that Account's other sessions and Refresh Tokens.
- The new password must satisfy SEC-2 and must differ from the current password.

#### FR-28: Reset an ordinary Account's password
An Administrator can request a password reset for an active Employee who is not an Administrator. A deactivated target is refused (409). Realizes UJ-7.

**Consequences (testable):**
- The reset invalidates the Account's password, voids any live Temporary Password, ends all its sessions and revokes its Refresh Tokens, clears its throttle state, and puts it in the Awaiting Password state. It returns no password; the Operator then issues a Temporary Password (FR-30).
- An Administrator cannot use this operation on themselves, on any Administrator, or on their own Direct Manager (403).
- A caller without the Administrator role receives 403. The reset is a Security-Class Event naming the Administrator and the target.

#### FR-29: Recover Administrator access (Operator Operation)
The Operator can reset an Administrator's password and restore access to an Administrator Account. This is the only way an Administrator's password is reset. Realizes UJ-7.

**Consequences (testable):**
- It is an explicitly invoked Operator Operation with no HTTP endpoint, and it requires a stated reason.
- It puts the named Administrator Account in the Awaiting Password state, voids any live Temporary Password, ends its sessions, revokes its Refresh Tokens, clears its throttle state, and issues a Temporary Password under FR-30. Only if the Operator explicitly asks, it also reactivates the Account (subject to FR-4's active-manager check, 409) or restores the Administrator role to an Account that previously held it.
- The stated reason has at most 500 characters.
- Each use and each refusal is a Security-Class Event with the Operator's identity (architecture decides how it is obtained) and the stated reason.
- A written recovery procedure (when to use it, how to verify the requester, how to record the reason) is a deliverable of the first increment.

#### FR-30: Issue a Temporary Password (Operator Operation)
The Operator can issue a Temporary Password for an active Account that is Awaiting Password, whether or not an earlier Temporary Password is outstanding; the new one voids the earlier. A deactivated Account is refused. A ticket-based issuance workflow is out of scope.

**Consequences (testable):**
- The Operator must confirm the recipient's identity before issuing, following the written procedure of FR-29's deliverable, and the Security-Class Event records that this confirmation was given.
- The password has at least 128 bits of cryptographic randomness.
- It is shown only to the Operator, on a channel that is not logged. It never appears in any API response, log, Audit Record, error message or event. It is stored only as a hash.
- It works for one login only and expires 24 hours after issue; issuing a new one voids the previous one.
- Issuing clears throttle state.
- The operation has no HTTP endpoint.
- Each issuance is a Security-Class Event naming the Operator and the target, and never the password.

### 4.3 Leave Balance

**Description:** Each Employee has one Leave Balance (one Leave Type). Administrators set and adjust Total manually. Pending requests Reserve days; approval converts the Reservation into a reduction of Total; rejection or cancellation releases it. Balances are whole days and never go negative. Realizes UJ-1, UJ-3.

**Functional Requirements:**

#### FR-11: Set the opening balance
Every Account is created with Total 0. An Administrator can set the opening balance once, while Total is 0 and no request exists, with a reason.

**Consequences (testable):**
- Total must be a non-negative whole number of days, at most 1000, and the reason at most 500 characters; otherwise 400.
- An Administrator cannot set their own balance (403).
- Audited with actor, value and reason.
- Viewing a balance before it is set returns Total 0, Reserved 0, Available 0.
- Setting the opening balance and a Balance Adjustment (FR-12) on the same Account are serialised; the loser receives 409.

#### FR-12: Adjust a balance
An Administrator can adjust Total up or down with a mandatory reason. An adjustment is a signed whole-number change (a delta) applied to the current Total inside the same serialised transaction, never an absolute overwrite. Realizes UJ-3.

**Consequences (testable):**
- An adjustment that would make Total lower than Reserved at commit time is refused (409).
- Two concurrent adjustments both apply, or the second is refused with 409; neither overwrites the other. An adjustment racing an approval or a submission is serialised the same way.
- The resulting Total is a non-negative whole number of at most 1000, the delta is at most 1000 in size, and the reason is at most 500 characters; otherwise 400.
- An Administrator cannot adjust their own balance (403).
- Audited with before and after values and reason.

#### FR-13: View balance
An Employee can view their own Total, Reserved and Available.

**Consequences (testable):**
- Available always equals Total minus Reserved.
- A Manager never sees a Direct Report's Total or Reserved. On a Direct Report's Leave Request the Manager sees only the Employee's Available days, which already exclude that request's Reservation.
- An Administrator can view any Employee's balance, and each such read is audited (FR-25).

### 4.4 Leave Request Submission

**Description:** An Employee submits a request for a continuous range of whole Working Days. The system validates all rules at submission, atomically with the Reservation. A submitted request cannot be edited: a Pending request must be cancelled before a replacement is submitted. Realizes UJ-1, UJ-4, UJ-5.

**Functional Requirements:**

#### FR-14: Submit a Leave Request
An Employee can submit a Leave Request with start date, end date and optional note. Realizes UJ-1, UJ-5.

**Consequences (testable):**
- Dates are calendar dates in `YYYY-MM-DD` form; a date-time, an offset or an end date before the start date is refused (400).
- The First Working Day is not before Today (in the Company Time Zone); otherwise 409.
- Requested Days is the count of Monday to Friday days from start to end inclusive and must be at least 1; a range containing only weekend days is refused (409).
- The end date is at most 366 days after the start date, and the start date is at most 2 years after Today; otherwise 400. There is no year-boundary rule.
- Requested Days must not exceed Available; otherwise the request is refused (409) and the shortfall is named.
- When several 409 refusals apply, they are reported in this order: no or inactive Direct Manager, First Working Day passed, no Working Day in the range, overlap, insufficient balance.
- On success the status is Pending, Reserved increases by Requested Days, and the request is assigned to the Employee's Direct Manager.
- An Employee with no Direct Manager is refused (409) with a reason naming the missing manager, whether that is a Top-Most Person or an Employee not yet assigned a manager (L-1). An Employee whose Direct Manager is not active is refused in the same way.
- Validation, the overlap check and the Reservation are one atomic operation: two concurrent requests that together exceed the balance or overlap cannot both succeed, and a request cannot be submitted by an Account that is being deactivated.
- The note has at most 500 characters; longer is refused (400).
- There is no operation to edit a submitted request.

#### FR-15: Prevent overlapping requests
The system must refuse a Leave Request whose dates overlap any of the same Employee's Pending or Approved requests.

**Consequences (testable):**
- Two requests overlap if they share at least one Working Day. A conflict is refused (409) and the response names the conflicting request. Ranges that share only a weekend day do not overlap (for example Wednesday to Friday followed by Saturday to Tuesday).
- Rejected and Cancelled requests do not block new ones.
- Adjacent (non-overlapping) ranges are allowed.
- Because a Pending request blocks overlap, a replacement for it can be submitted only after the original is cancelled.

#### FR-16: Cancel a pending request
An Employee can cancel their own Pending request. Realizes UJ-4.

**Consequences (testable):**
- The status becomes Cancelled and the Reservation is released.
- Cancelling an Approved, Rejected or already Cancelled request is refused (409).
- Only the owning Employee can cancel. Any other caller gets the response defined in SEC-11: 403 if they are a Request Reader (an Administrator, the Direct Manager or the Decider), otherwise 404.

#### FR-17: Track request status
An Employee can view any of their own requests, including status, decision time, Decider and rejection reason.

**Consequences (testable):**
- A caller who is not a Request Reader receives 404, identical to the response for a nonexistent request.
- The response includes the Direct Manager's name and no other manager data; the Decider is shown separately once the request is decided.

### 4.5 Manager Review

**Description:** A Manager decides Pending requests assigned to them. Only the Direct Manager decides; there is no delegation and no Administrator override. Realizes UJ-2.

**Functional Requirements:**

#### FR-18: List requests to review
A Manager can list requests of their Direct Reports and filter by status.

**Consequences (testable):**
- Only requests of current Direct Reports are returned.
- Each item shows the Employee's name, dates, Requested Days, note and the Employee's Available days (FR-13).
- A caller with no Direct Reports receives an empty list rather than an error.

#### FR-19: Approve a request
A Manager can approve a Pending request assigned to them. Realizes UJ-1.

**Consequences (testable):**
- Status becomes Approved; Total decreases by Requested Days and Reserved decreases by the same amount; Available is unchanged.
- A request whose First Working Day is before Today (in the Company Time Zone) cannot be approved (409). It can still be rejected, and the Employee can still cancel it.
- Approval is atomic with the balance change; two concurrent decisions on one request succeed exactly once.
- Response codes follow FR-21 and SEC-11. A request that is no longer Pending returns 409.

#### FR-20: Reject a request
A Manager can reject a Pending request assigned to them with a mandatory reason. Realizes UJ-2.

**Consequences (testable):**
- A missing, blank or over-500-character reason is refused (400), after the authorization checks of SEC-11.
- Status becomes Rejected and the Reservation is released.
- The reason is visible to the Employee. Response codes follow FR-21 and SEC-11.

#### FR-21: Decision authority
Only the request's current Direct Manager can decide it.

**Consequences (testable):**
- A caller who is not a Request Reader receives 404, whether or not they are a Manager.
- A Request Reader who is not the current Direct Manager of the owner (the owner, an Administrator who is not that Direct Manager, or a former Direct Manager who decided it) receives 403.
- The current Direct Manager of the owner may decide, whether or not they also hold the Administrator role.
- When a decision races with a reassignment, deactivation or cancellation, the caller's entitlement is evaluated on the committed state: a caller who is still the Direct Manager receives 409, and one who no longer is receives the 403 or 404 that SEC-11 gives them.
- A request that is no longer Pending returns 409 for the current Direct Manager.

### 4.6 History and Visibility

**Description:** Employees see their own history; Managers see their Direct Reports' requests; Administrators have audited read-only visibility. Realizes UJ-1, UJ-2.

**Functional Requirements:**

#### FR-22: Leave history
An Employee can list their own requests, filtered by status and date range, newest first, paged.

**Consequences (testable):**
- The result never contains another Employee's data.
- Each request exposes its status history (submitted, decided, cancelled) with times and actors. This is request history, not the Audit Record, and carries no other Audit data.

#### FR-23: Report visibility
A Manager can list the full request history of each current Direct Report, showing the Employee's name with each request.

**Consequences (testable):**
- After a reassignment, the former Manager no longer sees the Employee's other requests. Requests they decided stay readable to them as the Decider, showing the Employee's name, dates, status, reason and decision time, and never the Employee's balance. This holds even if they no longer have Direct Reports.
- Deactivated Direct Reports still appear in the Manager's history.

#### FR-24: Administrator visibility
An Administrator can view any Employee's requests, read-only.

**Consequences (testable):**
- The Administrator role confers no operation to approve, reject or cancel a request; an Administrator who is not the request's Direct Manager gets 403 when attempting one. (Deactivation, FR-4, and clearing a manager, FR-3, cancel the affected Employee's Pending requests as a side effect and are not decisions.)
- Each Administrator read of another Employee's request is audited (FR-25).

### 4.7 Audit and Operator Configuration

**Description:** Every security-relevant and business-relevant change is recorded in an audit trail that is immutable except for the retention process, and readable only by Administrators. Retention and the Company Time Zone are set by Operator configuration, never through the API.

**Functional Requirements:**

#### FR-25: Record audit events
The system records an Audit Record for each of the following events: Operator Operation uses and refusals (bootstrap, temporary password issuance, recovery); Account creation; Administrator creation or grant, including Privileged Requests (created, approved, rejected, expired, executed) and the Single-Administrator Window use; Administrator role removal; manager change or clearing, and the Pending requests it moves or cancels; deactivation or reactivation, and the requests deactivation cancels; login success and lockout transitions; failed logins in aggregate (see below); refresh-token reuse; password change; Administrator reset of an ordinary Account (FR-28); balance set or adjusted; each Leave Request creation, approval, rejection and cancellation; each Administrator read of another Employee's requests or balance; each Audit Record query (FR-26); the retention purge; and the effective Company Time Zone and retention values recorded at start-up, with any change from the last recorded values (FR-32).

**Consequences (testable):**
- Each record has actor, action, target, UTC timestamp, and before-and-after values where relevant. The actor of an Operator Operation is the Operator; the actor of a system process is named as such.
- Audit Records cannot be edited or deleted through the API, and the application's database role has no permission to delete or update them (SEC-23).
- An Audit Record for a business change is committed in the same transaction as the change; if the record cannot be written, the change fails.
- A failed login does not create one record per attempt. Lockout transitions and a bounded failure count per source and time window are recorded, and a failed login against an unknown email stores no raw submitted identifier. Field lengths are capped at 256 characters.
- Audit Records never contain passwords, Temporary Passwords, tokens or token hashes.
- Ordinary Events older than the Ordinary Retention Period and Security-Class Events older than the Security Retention Period may be removed only by the retention process (SEC-23), never through the API. Each purge writes a Security-Class summary record stating the cutoff dates, counts and configured periods, and that summary is kept for the Security Retention Period.

**Notes:** Retention values are defaults for this learning project. Actual legal or organisational retention requirements are undetermined (Q-A, L-4).

#### FR-26: Read audit records
An Administrator can query Audit Records by target, actor, action and time range.

**Consequences (testable):**
- A caller without the Administrator role receives 403.
- Results are paged.
- No other role, including the subject of a record, can read Audit Records.
- Each query is itself audited with its parameters, not its results.

#### FR-32: Operator-controlled configuration
The Company Time Zone and the two retention periods are set only by Operator configuration, not through the API.

**Consequences (testable):**
- No API operation changes them, for any role.
- At start-up the system rejects an invalid IANA time zone, an Ordinary Retention Period under 30 days, and a Security Retention Period not longer than the Ordinary Retention Period.
- At start-up the system records the effective values and any change from the last recorded values as a Security-Class Event with actor "operator configuration".
- A changed Company Time Zone applies from the next start; Pending requests are re-evaluated against it when they are next decided.

### 4.8 User Stories

Acceptance criteria for each story are the testable consequences of the FRs it maps to. FR-27 and FR-31 belong to §4.1, FR-28 to FR-30 belong to §4.2, and FR-32 belongs to §4.7. They are numbered out of order, so ranges such as "FR-1 to FR-6" and "FR-7 to FR-10" do not include them.

| Story | As a... | I can... | So that... | FRs |
|---|---|---|---|---|
| US-1 | Administrator | create an Account and assign, change or clear a Direct Manager | a new joiner can use the system and reporting lines are right | FR-1, FR-2, FR-3 |
| US-2 | Administrator | deactivate or reactivate an Account, after reassigning or clearing its Direct Reports | leavers lose access but history is kept and nobody is orphaned | FR-4 |
| US-3 | Administrator | set and adjust an Employee's balance with a reason | balances are correct and explainable | FR-11, FR-12 |
| US-4 | Administrator | view Accounts and query the Audit Records | I can answer who did what and when | FR-6, FR-26 |
| US-5 | Any user | log in, stay signed in, log out and change my password | I can use the system securely | FR-7 to FR-10 |
| US-6 | Employee | see my Total, Reserved and Available days | I know what I can request | FR-13 |
| US-7 | Employee | submit a Leave Request that is checked for balance and overlap | I never submit leave that cannot be granted | FR-14, FR-15 |
| US-8 | Employee | cancel my Pending request and submit a corrected one | I can change my mind and get the days back | FR-16 |
| US-9 | Employee | track the status and history of my requests | I do not need to ask anyone | FR-17, FR-22 |
| US-10 | Manager | see requests from my Direct Reports | I know what is waiting on me | FR-18 |
| US-11 | Manager | approve a request | the Employee's leave is confirmed and the balance updated | FR-19 |
| US-12 | Manager | reject a request with a reason | the Employee understands the decision | FR-20 |
| US-13 | Manager | see my Direct Reports' request history | I can plan team cover | FR-23 |
| US-14 | Any user | be prevented from seeing or changing data I am not entitled to | data stays private | FR-17, FR-21, SEC-7 to SEC-13 |
| US-15 | Operator | create the first Administrator safely and only once | the system can be set up without hardcoded passwords | FR-27 |
| US-16 | Administrator | read any Employee's requests, read-only and audited | I can support Employees without deciding for them | FR-24 |
| US-17 | Administrator | rely on every important change being audited | I can reconstruct what happened | FR-25 |
| US-18 | Administrator | request a password reset for an ordinary Account | a forgotten password can be recovered without exposing it | FR-28 |
| US-19 | Administrator | create or grant a second Administrator once, grant the role thereafter with a second Administrator's approval, and remove the role | there is more than one Administrator and no single one can keep adding more | FR-5, FR-31 |
| US-20 | Operator | verify a person, issue a Temporary Password, and recover Administrator access with a written procedure | people can get in without the API ever handling a password | FR-29, FR-30 |
| US-21 | Operator | set the Company Time Zone and retention periods outside the API | no Administrator can shorten the audit trail | FR-32 |

## 5. Security Requirements

**Authentication**
- SEC-1. Passwords are stored only as salted, slow, one-way hashes using a modern password-hashing algorithm; plaintext is never stored or logged.
- SEC-2. Password length is 12 to 128 characters, checked before hashing; there are no composition rules; common passwords from a named offline list are rejected. `[ASSUMPTION: breached-password screening mechanism decided in architecture.]`
- SEC-3. Access Tokens are short-lived, with a lifetime of 15 minutes (plus bounded clock skew), and carry identity and a session reference (a role claim is a convenience only, see SEC-13). Signing algorithm pinning, issuer and audience validation, and key rotation must be decided in architecture.
- SEC-4. Refresh Tokens are single-use, rotated on every refresh, stored only as hashes, revocable, and expire: 7 days idle and 30 days absolute (indicative). Reuse of a spent token revokes its whole family; a repeat inside a short grace window (decided in architecture) returns the same successor pair.
- SEC-5. **Throttling.** Controls do not depend on one signal. (a) The connection address is one signal; forwarded headers are trusted only when they come from configured proxies. (b) Each known account has progressive back-off, independent of source and stored with the Account so it is never evicted: after 5 consecutive failures the account is refused for 1 minute, doubling with each further failure up to a ceiling of 15 minutes, and the count resets after a successful login or 30 minutes without failures (indicative). (c) A per-source ceiling (20 failed attempts per 15 minutes) and a global ceiling (1000 failed attempts per minute) bound total attempts (indicative); state for unknown emails is bounded in size and time to live and may be evicted. Refusals return 429 identically for known and unknown emails, with a dummy password verification for unknown ones so timing does not reveal the difference. Login, change-password and refresh are all throttled. An Administrator reset (FR-28), Temporary Password issuance (FR-30) and recovery (FR-29) clear an Account's throttle state.
- SEC-6. Logout, password change, password reset and deactivation end sessions and revoke Refresh Tokens.

**Authorization and data isolation**
- SEC-7. Every endpoint except login, refresh and health requires authentication, and access is denied by default.
- SEC-8. Authorization is enforced on the server by role and by relationship (owner, Direct Manager); the client's claims about ownership are never trusted.
- SEC-9. An Employee can read or modify only their own requests and balance.
- SEC-10. A Manager can act only on requests of current Direct Reports.
- SEC-11. **Response-code policy.** Rules apply in this order:
  1. 429 for a throttled login, refresh or change-password call (SEC-5).
  2. 401 for a missing, invalid or expired token, a session that has ended, a refresh attempted in a Must-Change Session, and every failed login (including an expired or used Temporary Password).
  3. 403 for an operation restricted to a role whose existence is not secret (administrative endpoints, Administrator-only reads), and for a Must-Change Session calling anything but change-password and logout.
  4. 404 when the caller is not a Request Reader, or otherwise not entitled to know the resource exists.
  5. 403 when the caller may read the resource or target but lacks permission for the operation (for example an Administrator or the owner trying to decide, a Direct Manager trying to cancel, an Administrator acting on a restricted target).
  6. 400 for malformed or invalid input and out-of-range values (formats, lengths, bounds on span, horizon and amounts), evaluated after rules 2 to 5 so that an unauthorized caller learns nothing from validation.
  7. 409 for a state conflict, a business-rule refusal (insufficient balance, overlap, no Direct Manager, request not Pending, First Working Day passed, remaining Direct Reports, duplicate email, reporting cycle, last Administrator) or a lost race.
  Where rules 4 and 5 could both apply, 404 wins. A lost race is answered by evaluating the caller's entitlement on the committed state: still entitled gives 409, no longer entitled gives the 403 or 404 that SEC-11 yields.
- SEC-12. The Administrator role confers no right to approve or reject leave. An Administrator who is legitimately the Direct Manager of an Employee may decide that Employee's requests as a Manager, but cannot assign themselves as another Employee's Direct Manager, change or clear their own Direct Manager or roles, deactivate themselves, or set or adjust their own balance. Creating or granting an Administrator follows FR-31. No role can read passwords or tokens.
- SEC-13. On every authenticated call the server re-reads the Account's active state, Administrator role, password state (including whether the session is a Must-Change Session) and session validity, and any manager relationship the operation depends on, from current data. The Access Token proves identity only; the role it carries is never trusted for authorization. A deactivated Account is refused with 401 on its next call, and a demoted Administrator loses Administrator rights on their next call. Refresh also re-checks active state and password state.

**Input and API hardening**
- SEC-14. All input is validated server-side, with maximum lengths on every free-text field (names, notes, reasons), stored and returned as plain text; request fields are bound to explicit input models so a caller cannot set status, balance, ownership or role by mass-assignment.
- SEC-15. Only HTTPS is accepted in any deployed environment; secrets and signing keys are never in source control and come from configuration or a secret store.
- SEC-16. Error responses do not leak stack traces, SQL or internal identifiers beyond what a client needs.
- SEC-17. CORS is restricted to configured origins; the OWASP recommended secure response headers are set. The OpenAPI document and any API explorer are disabled or authenticated in deployed environments and are also produced as a build artifact for the front-end developer. The health endpoint returns only a minimal up/down status.
- SEC-18. Identifiers are non-guessable (at least 128 bits of randomness or a random-UUID equivalent), so they do not leak counts or allow enumeration.

**Audit, credentials and privacy**
- SEC-19. Audit Records are immutable except for the retention process, readable only by Administrators, and exclude secrets (FR-25, FR-26).
- SEC-20. **Privacy-conscious design (not a compliance claim).** Personal data stored is limited to name, work email and leave data. Data is exposed on a need-to-know basis (SEC-9, SEC-10, FR-13). Jurisdiction-specific compliance is an open consideration (Q-A).
- SEC-21. Logs avoid personal data and never contain tokens or passwords.
- SEC-22. **Temporary Passwords.** A Temporary Password is generated by a cryptographically secure generator with at least 128 bits of randomness, issued only by an Operator Operation after the Operator verifies the recipient, stored only as a hash, shown only to the Operator on a channel that is not logged, valid for one login within 24 hours, and replaced at first login. No API response, log, Audit Record or error contains one.
- SEC-23. **Audit protection.** The application's database role can insert and read Audit Records but cannot update or delete them. The retention process runs as a separate process and database role. Retention and time-zone values come from Operator configuration only. Tamper evidence for the audit trail, such as a hash chain or an external append-only sink, is a requirement on the architecture, which must define it before the audit module is built.
- SEC-24. **Session binding.** Each Access Token belongs to a session. Logout, password change, password reset and deactivation end the session or sessions, and the server rejects an Access Token whose session has ended on its next use. This is checked in the per-call lookup of SEC-13.

## 6. Non-Functional Requirements (MVP)

- **Correctness under concurrency:** every state-changing operation re-validates its preconditions inside the same serialised transaction as its write: request status, ownership and manager relationship, active flags, last-Administrator count, the reporting-cycle check, Single-Administrator Window and Privileged Request state, and Account password state. This covers at least: approve versus cancel; approve versus manager reassignment, clearing or deactivation of the Employee; cancel versus reassignment; submit versus deactivation or manager clearing; two concurrent reassignments that would together form a cycle; reactivation of an Employee versus deactivation of that Employee's Direct Manager; assigning a manager versus deactivating that manager; two Administrators removing each other's role or deactivating each other; two concurrent approvals of one Privileged Request, or two concurrent uses of the Single-Administrator Window; a Privileged Request versus a change to its Initiator or target; login with a Temporary Password versus issuing a new one or an Administrator reset; setting the opening balance versus a Balance Adjustment; and two concurrent adjustments or an adjustment versus an approval. Losers are answered under SEC-11 with a stable error code (409 where the caller is still entitled). Reserved and Total never become inconsistent or negative.
- **Performance:** routine reads respond in under 500 ms at the 95th percentile for up to 200 Employees on a single small instance. `[ASSUMPTION: scale of about 200 employees, one organisation.]`
- **Testability:** each increment ships with automated tests that cover its rules, including authorization and isolation.
- **API quality:** one documented error shape for all failures, paging on all lists with a default page size of 25 and a maximum of 100, and a documented OpenAPI contract so the later React app can rely on it.
- **Maintainability:** modules have clear boundaries; the monolith must be splittable later without rework. Detail goes to architecture.
- **Time:** all timestamps are stored and returned in UTC. Leave dates are calendar dates without a time of day. Date-sensitive rules (FR-14 and FR-19) use Today in the Company Time Zone, set by Operator configuration (FR-32), default `Asia/Kolkata`, and the server clock is the only source of Today.
- **Retention:** Ordinary Retention Period (default one year) and Security Retention Period (default three years) are set by Operator configuration (FR-32). Retention of other data is not defined in the MVP (L-4).
- **Portability:** local development on macOS and VS Code with a containerised PostgreSQL; deployable to Azure later. Detail goes to architecture.

## 7. Non-Goals (Explicit)

- Not a payroll, timesheet, attendance or HR-records system.
- Not a multi-tenant or multi-company product.
- No multiple Leave Types, accrual, carry-over, yearly rollover, half-days, public-holiday calendar or per-country rules.
- No backdated requests; no cancelling an Approved request; no editing a submitted request. A Pending request is cancelled and a replacement submitted instead.
- No delegation, escalation, multi-level approval of leave, or Administrator override of leave decisions.
- No leave for the Top-Most Person or for any Employee without a Direct Manager (L-1).
- No email-based password reset, invitation or notification of any kind in the MVP; self-service password reset is deferred to a later phase.
- No ticket-based or workflow-driven password issuance; the Operator verifies the recipient by following the written procedure.
- No approval workflows for privileged account changes other than creating or granting an Administrator.
- No reporting, dashboards, exports, team calendars or calendar sync.
- No MFA, SSO or external identity providers.
- No claim of GDPR or other regulatory compliance.
- No front end and no Azure deployment in the MVP; no code is produced at this stage.

## 8. MVP Scope

### 8.1 In Scope
Accounts, roles, manager assignment and creating or granting an Administrator (FR-1 to FR-6, FR-31); Operator Operations (FR-27, FR-29, FR-30); authentication and sessions (FR-7 to FR-10, FR-28); balance (FR-11 to FR-13); submit, cancel and track requests (FR-14 to FR-17); manager review (FR-18 to FR-21); history and visibility (FR-22 to FR-24); audit and Operator configuration (FR-25, FR-26, FR-32); security requirements (§5).

### 8.2 Out of Scope for MVP
Everything in §7. Deferred, in rough order of likely value: self-service password reset by email, notifications, approved-request cancellation, a leave path for the Top-Most Person, holiday calendar, multiple leave types and accrual, reporting, SSO and MFA.

### 8.3 Increment Order *(final slicing is a ticketing step)*
The order approved earlier (AP-9) is kept, with the dependency corrections of decision Q-D D1: credentials, login, the Operator Operations and the audit write mechanism come before the Administrator endpoints that need them, and request-dependent side effects land with the requests.
1. Foundations: Account, role and manager data, the Bootstrap Operation, Temporary Password issuance and recovery (FR-27, FR-29, FR-30), Operator configuration (FR-32), login, logout, refresh and change-password, and the audit write mechanism.
2. Administrator endpoints: create and view Accounts, manager assignment, reassignment and clearing, creating or granting an Administrator (FR-31), ordinary password reset (FR-28), roles, and deactivation without the request side effects.
3. Balance set, adjust and view.
4. Submit with overlap and balance validation, then cancel and track (FR-14 to FR-17); the request side effects of FR-3 and FR-4 (moving or cancelling Pending requests) are completed here.
5. Manager list, approve and reject.
6. History views and visibility rules.
7. Audit read endpoint, audited Administrator reads, and hardening pass.

## 9. Success Metrics

**Primary**
- **SM-1:** The journeys UJ-1 to UJ-8 complete end to end via the API and the Operator Operations in an automated test. Validates FR-1 to FR-4, FR-7, FR-11, FR-13 to FR-17, FR-19, FR-20, FR-27 to FR-31.
- **SM-2:** A concurrency stress test (iteration and parallelism counts set in architecture) over the races listed in §6 ends with the database invariants intact: no overlapping Pending or Approved requests; Reserved equal to the sum of Pending Requested Days; Total and Reserved never negative; every active Employee's Direct Manager is active or absent; at least one active Administrator remains; and every executed Privileged Request has distinct Initiator and Approver. Validates FR-3, FR-4, FR-5, FR-12, FR-14, FR-15, FR-19, FR-31.
- **SM-3:** An authorization test matrix (every endpoint by every role by own, report and foreign resource, plus every self-dealing and restricted-target case) passes with no isolation breach and with the response codes defined in SEC-11. Validates SEC-7 to SEC-13, FR-17, FR-21, FR-28.
- **SM-4:** Every event listed in FR-25 produces an Audit Record with actor and time, and a business change whose audit write fails is rolled back, verified by test. Validates FR-25.
- **SM-5:** The Bootstrap Operation creates exactly one Administrator under repeated and concurrent invocation. Validates FR-27.
- **SM-6:** After the Single-Administrator Window closes, no Administrator is created or granted without two distinct Administrators, verified by test including the self-approval, target-approval, expiry and race cases. Validates FR-31.
- **SM-7:** No API response, log line, Audit Record or error contains a password or Temporary Password, verified by tests and by scanning captured logs, and every Temporary Password has at least 128 bits of randomness and follows the lifecycle of FR-2. Validates FR-2, FR-30, SEC-22.

**Secondary**
- **SM-8:** Each increment is built, tested and demonstrable before the next starts.
- **SM-9:** The OpenAPI document describes every MVP endpoint and is usable by a front-end developer without reading the code.

**Counter-metrics (do not optimize)**
- **SM-C1:** Count of endpoints or features shipped. More endpoints must not displace the correctness and isolation tests. Counterbalances SM-8.
- **SM-C2:** Approval speed must not be bought by weakening the checks (for example skipping the relationship check on decide). Counterbalances SM-1.
- **SM-C3:** Operator effort per Temporary Password must not be reduced by exposing passwords in the API or in logs. Counterbalances SM-7.

## 10. Decisions, Limitations and Open Questions

### 10.1 Approved decisions *(owner-approved)*

| ID | Decision | Where applied |
|---|---|---|
| AP-1 | Resource-specific 404 where existence should be concealed; 403 where a permission denial may reveal existence. | SEC-11, FR-6, FR-16, FR-17, FR-19, FR-20, FR-21 |
| AP-2 | Deactivation cancels pending requests and releases reserved days; refused while the Employee has Direct Reports. | FR-4 |
| AP-3 | Managers see only the balance information needed to evaluate a request (Available days). | FR-13, FR-18 |
| AP-4 | Administrators may read requests but cannot approve or reject. | FR-21, FR-24, SEC-12 |
| AP-5 | Password minimum 12, login throttling, access token 15 minutes, secure hashing, refresh-token rotation and revocation. | SEC-1 to SEC-6 |
| AP-6 | Employees with no Direct Manager cannot submit leave. | FR-14 |
| AP-7 | Submitted requests cannot be edited; cancel a Pending request before submitting a replacement. | FR-14, FR-15, FR-16 |
| AP-8 | A balance adjustment cannot reduce Total below Reserved. | FR-12 |
| AP-9 | Incremental delivery order, with the dependency corrections in Q-D D1. | §8.3 |
| RV-1 | Administrator password reset (FR-28) and Operator recovery (FR-29) exist; lockout is not a permanent denial. Refined by RV-6, RV-8 and RV-11. | FR-28, FR-29, SEC-5 |
| RV-2 | The Administrator role confers no approval right; an Administrator who is the Direct Manager may decide as a Manager. No self-dealing: an Administrator cannot assign themselves as another Employee's Direct Manager (at creation or later), change or clear their own Direct Manager or roles, deactivate themselves, or set or adjust their own balance. (Clarifies AP-4.) | SEC-12, FR-1, FR-3, FR-4, FR-5, FR-11, FR-12, FR-21, FR-24 |
| RV-3 | Account state, Administrator role, password state and session validity are re-read on every authenticated call; the token proves identity only. (Refines AP-5; L-2 records the per-call lookup cost.) | SEC-3, SEC-13, SEC-24, FR-4, FR-5 |
| RV-4 | Request span at most 366 days, start at most 2 years ahead, balance values bounded; a request whose First Working Day has passed cannot be approved but can be rejected or cancelled. | FR-11, FR-12, FR-14, FR-19 |
| RV-5 | **Administrator lifecycle (simplified).** After the first, an Administrator is made by granting the role to an existing Account, which needs a second Administrator's approval. A sole Administrator may create or grant the second Administrator once (the Single-Administrator Window); two-person approval applies thereafter. The last-Administrator safeguard is kept. Other privileged account changes (deactivating or reactivating an Administrator, removing a role) are single-Administrator actions that are audited and recorded as limitation L-9. | FR-31, FR-4, FR-5, SEC-12, UJ-8, L-9 |
| RV-6 | **Password recovery.** Email-based reset is deferred. An Administrator cannot use the ordinary reset on themselves, any Administrator, or their own Direct Manager. An Administrator's password is reset only by the Operator's recovery operation. Temporary passwords are cryptographically generated, expire after 24 hours, work for one login, are replaced at first login, and never appear in API responses or logs. | FR-28, FR-29, FR-30, SEC-22 |
| RV-7 | **Manager relationships.** An audited clearing or reassignment is required before deactivating an Employee who has Direct Reports; nothing is changed silently. The rule that an Employee without a Direct Manager cannot submit leave is kept. | FR-3, FR-4, L-1 |
| RV-8 | **Throttling.** The connection address is one signal; per-account progressive back-off with a defined ceiling; temporary passwords carry at least 128 bits of cryptographic randomness; controls do not depend solely on IP. | SEC-5, SEC-22 |
| RV-9 | **Retention and audit protection.** Retention and the Company Time Zone are Operator configuration; the application's database role cannot delete Audit Records; Security-Class Events and purge summaries have a longer retention than the ordinary one-year default; stronger tamper evidence is an architecture requirement. | FR-32, SEC-23, FR-25 |
| RV-10 | **Operator issuance.** An authorized Operator verifies the recipient and issues temporary passwords. A ticket-based issuance workflow is out of scope. The API never returns or logs a plaintext temporary password. | FR-30, SEC-22, §7 |
| RV-11 | **Temporary password lifecycle.** 24 hours, one login, used to complete a mandatory password change in a short Must-Change Session without a Refresh Token. The Account stays Awaiting Password until the change succeeds, so if the session ends first or the password is unused after 24 hours the Operator can reissue. | FR-2, FR-7, FR-8, FR-10, FR-30, SEC-11, SEC-13 |
| Q1 (Who approves the Top-Most Person's leave?) | The Top-Most Person cannot submit leave in the MVP. Documented as limitation L-1. No Administrator override, no delegation. | FR-14, §7, UJ-5 |
| Q2 (Which time zone defines "today"?) | Configurable Company Time Zone, default `Asia/Kolkata`. UTC timestamps; company zone for date-sensitive rules. Set by Operator configuration (RV-9). | §3, §6, FR-14, FR-32 |
| Q4 (Audit retention and who may read it?) | Audit immutable except for the retention process, Administrator-only readable, one-year default for ordinary events and a longer default for Security-Class Events (RV-9). Legal retention undetermined. | FR-25, FR-26, SEC-19 |
| Q6 (May an adjustment go below reserved days?) | Resolved by AP-8. | FR-12 |
| Q7 (Do GDPR or similar rules apply?) | Fictional learning project, no compliance claim. Privacy-conscious design; jurisdictional compliance an open consideration. | SEC-20, §0 |
| Q8 (How is the first Administrator created?) | First Administrator created by an explicit, secure, repeat-safe Bootstrap Operation (a repeat run is refused and changes nothing); no hardcoded passwords; no secrets logged. | FR-27, UJ-6 |

**Accepted decisions Q-E1 to Q-E8.** All were accepted at the stated defaults, adjusted only where RV-5 and RV-11 now say otherwise.

| ID | Accepted |
|---|---|
| Q-E1 | The API never returns a password. The Operator issues every Temporary Password through an Operator Operation, for each new Account and each ordinary reset. |
| Q-E2 | Restricted reset targets for FR-28: the Administrator themselves, any Administrator, and the Administrator's own Direct Manager (the person who decides their leave). |
| Q-E3 | Two-person approval covers granting the Administrator role (after the second Administrator, an Administrator is made by creating an ordinary Account and then granting the role). The target can be neither Initiator nor Approver. A Privileged Request expires after 24 hours. Resetting an Administrator's password is not two-person (see RV-5, RV-6). |
| Q-E4 | Clearing a Direct Manager cancels that Employee's Pending requests and releases the days. |
| Q-E5 | Temporary Password lifetime is 24 hours, valid for one login. |
| Q-E6 | Security-Class Events default to a three-year retention; ordinary events one year; start-up floors of 30 days for ordinary and longer-than-ordinary for security. |
| Q-E7 | Back-off: after 5 consecutive failures an account is refused for 1 minute, doubling to a 15-minute ceiling, resetting after success or 30 minutes quiet; per-source and global ceilings. |
| Q-E8 | Stale Pending requests (First Working Day passed, never decided) stay Reserved until a Manager rejects them or the Employee cancels (L-10). |

**Accepted defaults Q-C (formerly §10.3).**

| ID | Question | Accepted default |
|---|---|---|
| Q3 | Can one person hold both Administrator and Employee roles and take leave? | Yes. Roles combine; the Administrator role grants no approval right. An Administrator who also takes leave needs a Direct Manager like anyone else (L-1). |
| Q5 | Are there minimum-notice or blackout periods? | No. Any First Working Day from Today onwards is allowed. |
| Q9 | Is balance held in whole days only? | Yes. Total, Reserved, Available and adjustments are whole numbers. |
| Q10 | Should the Employee see the Direct Manager's name on a request? | Yes, name only. |

**Accepted defaults Q-D (D1 to D16).**

| # | Default | Where |
|---|---|---|
| D1 | Increment order corrected: login, credentials, Operator Operations and the audit write mechanism first; request side effects with the requests. | §8.3 |
| D2 | A new Account starts with Total 0; the opening balance is set once. | FR-11 |
| D3 | Two requests overlap only if they share a Working Day. | FR-15 |
| D4 | No editing of a person's name or email. | L-8 |
| D5 | Indicative session lifetimes: refresh token 7 days idle, 30 days absolute. | SEC-4 |
| D6 | Password length 12 to 128; common-password screening from an offline list. | SEC-2 |
| D7 | Page size default 25, maximum 100. | §6 |
| D8 | Numeric bounds: total and adjustment at most 1000 days; request span at most 366 days; start at most 2 years ahead. | FR-11, FR-12, FR-14 |
| D9 | Free-text limits: name 100, email 254, note 500, reason 500 characters. | FR-1, FR-14, FR-20 |
| D10 | The First Working Day, not the start date, drives the not-in-the-past and approval rules. | FR-14, FR-19 |
| D11 | Administrator reads of other people's requests and balances, and every audit query, are themselves audited. | FR-13, FR-24, FR-25, FR-26 |
| D12 | Access tokens are bound to a session so logout, password change, reset and deactivation take effect immediately; a repeated refresh in the grace window returns the same pair. | SEC-24, FR-8, FR-9 |
| D13 | Failed logins are audited in aggregate (lockout transitions and bounded counts), not per attempt. | FR-25 |
| D14 | Dates are `YYYY-MM-DD`; date-times and offsets are refused. | FR-14 |
| D15 | Input errors and out-of-range values return 400, business-rule refusals and lost races 409, with authorization checked first. | SEC-11 |
| D16 | Reactivation returns an Account to Awaiting Password. | FR-4 |

### 10.2 Known limitations of the MVP

- **L-1. No leave for the Top-Most Person.** Anyone without a Direct Manager, including the Top-Most Person, the bootstrapped first Administrator, an Employee whose manager was cleared and any Employee not yet assigned a manager, cannot submit leave requests. There is no Administrator override and no delegation. Leave for such people is handled outside the system. Revisit when a designated-approver or self-approval policy is wanted.
- **L-2. Per-call account lookup.** Because the Access Token is never trusted for account state (SEC-13), every authenticated call reads Account state from the database. This costs one lookup per call and is accepted for the MVP.
- **L-3. No notifications.** Employees and Managers must look at the API (or later the UI) to learn of changes.
- **L-4. Retention beyond audit is undefined.** Deactivated Accounts, leave data and balances are retained indefinitely in the MVP; erasure or anonymisation is not designed. Audit retention periods are defaults and not legally validated.
- **L-5. No regulatory compliance claim.** Privacy-conscious design only; GDPR and any other jurisdiction-specific obligations are not assessed.
- **L-6. Passwords depend on the Operator.** Every new Account, every ordinary password reset and every recovery needs an Operator to issue a Temporary Password, because the API never exposes passwords and email-based reset is deferred. This is an operational burden accepted for the MVP.
- **L-7. Time zone is organisation-wide.** One Company Time Zone applies to everyone; per-employee or per-country zones are not supported.
- **L-8. No profile edit.** A mistyped name or email cannot be corrected through the API in the MVP, and a person who returns after leaving is reactivated with their old history and balance.
- **L-9. Administrator and Operator trust.** After the second Administrator exists, only creating or granting an Administrator needs two people. One Administrator can deactivate, reactivate or remove the role of another, and can quietly cancel or re-route Employees' leave through manager clearing or deactivation. These are audited, not prevented. Two Administrators can collude. An Administrator who is legitimately a Direct Manager has full Manager powers over their reports. The Operator is trusted: the Operator sees every Temporary Password, verifies recipients by hand, and can recover any Administrator. Per-account back-off means an attacker can keep a named account in back-off up to the 15-minute ceiling, and throttling does not stop guessing spread across many sources. The Operator can shorten retention by configuration, within the start-up floors. An Administrator's own Direct Manager can be reset or deactivated only after another Administrator changes that Administrator's Direct Manager.
- **L-10. Stale Pending requests.** A Pending request whose First Working Day has passed cannot be approved, but it keeps its days Reserved until a Manager rejects it or the Employee cancels it. There is no automatic expiry.
- **L-11. Audit expiry.** Ordinary audit events (including Leave Request events) expire after the Ordinary Retention Period while balances persist, so an old balance may lack an explaining Ordinary Event. Balance and security events are kept for the longer Security Retention Period.
- **L-12. After the window closes, a sole remaining Administrator cannot add Administrators alone.** If role removal or deactivation leaves one active Administrator, the Operator restores an Administrator through FR-29 (reactivation or restoring the role) rather than any new grant.

### 10.3 Open questions *(non-blocking for this learning MVP)*

1. **Q-A. Jurisdiction-specific compliance and legal retention.** Which privacy or labour rules would apply if this were deployed for a real organisation, and what retention they require. *Owner:* project owner. *Revisit:* before any real-data use or production deployment.
2. **Q-B. Policy for the Top-Most Person's leave** (designated approver, audited self-approval, or stays outside the system). *Owner:* project owner. *Revisit:* when L-1 becomes a real problem.

No open question blocks architecture, UX or ticketing.

## 11. Assumptions Index

- §3 Manager: Manager capability is derived from having at least one active Direct Report, not stored as a role.
- §3 Operator: the Operator is trusted; identity comes from host-level access (architecture).
- §5 SEC-2: breached-password screening mechanism decided in architecture.
- §6: scale of about 200 employees and one organisation.
- Brief A1 to A4: overlap checks Pending and Approved requests only, over Working Days; the Administrator role confers no approval right; single organisation and one Company Time Zone; Manager is derived.
