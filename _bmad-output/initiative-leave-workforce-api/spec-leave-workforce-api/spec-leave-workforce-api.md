---
id: SPEC-leave-workforce-api
companions:
  - capability-map.md
  - ../prd-leave-workforce-api/prd-leave-workforce-api.md
  - ../architecture-leave-workforce-api/architecture-leave-workforce-api.md
  - ../architecture-leave-workforce-api/solution-structure.md
sources:
  - ../brief-leave-workforce-api/brief-leave-workforce-api.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Leave and Workforce Management API (MVP)

## Why

An organisation that runs employee leave by email or spreadsheets suffers drifting balances, overlapping or over-balance leave, managers who lose track of requests, and no trustworthy record of who approved what. This is a pain to solve, and also a deliberate learning build of a secure, incremental ASP.NET Core system. It affects Employees, Managers and Administrators of one organisation, plus the Operator who deploys it and a later React front-end developer. It matters now because the requirements and architecture are final and the build can start one increment at a time.

## Capabilities

- **CAP-1**
  - **intent:** An Operator can bootstrap the first Administrator, issue Temporary Passwords after verifying the recipient, and recover Administrator access, entirely outside the API.
  - **success:** Running bootstrap on an empty database creates exactly one Administrator in the Awaiting Password state and prints a Temporary Password once; a repeat or concurrent run changes nothing and is audited; no API response, log line or audit record ever contains a password.
- **CAP-2**
  - **intent:** A person can log in, complete a mandatory password change after a Temporary Password, stay signed in through rotating refresh tokens, log out, and change their password, under throttling that cannot be used to lock others out.
  - **success:** Automated tests show login, first-login change, refresh rotation with reuse detection, logout, throttling and the generic 401 and 429 behaviour, including that a lapsed first-login session leaves the Account Awaiting Password so a new Temporary Password can be issued.
- **CAP-3**
  - **intent:** An Administrator can create, list, deactivate and reactivate Accounts and request a password reset for an ordinary Account.
  - **success:** Creating an Account yields an Awaiting Password Account with a zero balance and no returned credential; deactivation is refused while active Direct Reports remain and otherwise cancels the Account's Pending requests and ends its sessions; restricted reset targets are refused with 403.
- **CAP-4**
  - **intent:** Administrators are made and removed under controlled rules: the sole Administrator may add the second Administrator once, then a second Administrator must approve every grant, and the last Administrator can never be lost.
  - **success:** After the Single-Administrator Window closes, a direct grant or create-with-role is refused and a grant takes effect only through an approved Privileged Request with distinct Initiator and Approver; tests cover self-approval, target approval, expiry and concurrent approval.
- **CAP-5**
  - **intent:** An Administrator can assign, reassign or clear an Employee's Direct Manager, and Managers can list their Direct Reports.
  - **success:** Cycles and inactive managers are refused; reassignment moves Pending requests to the new manager; clearing cancels them with an audited reason and leaves a Top-Most Person who cannot submit leave.
- **CAP-6**
  - **intent:** An Administrator can set and adjust Leave Balances with a reason, and an Employee can view their own Total, Reserved and Available days.
  - **success:** Adjustments are signed deltas that never push Total below Reserved or outside bounds, are serialised against concurrent approvals and submissions, and Available always equals Total minus Reserved.
- **CAP-7**
  - **intent:** An Employee can submit, cancel and track Leave Requests for continuous whole Working Days without exceeding the balance or overlapping other Pending or Approved leave.
  - **success:** Concurrent submissions that together exceed the balance or overlap never both succeed, proved at the database; Pending requests Reserve days, cancellation releases them, and all validation, ordering and 409 behaviour match FR-14 to FR-17.
- **CAP-8**
  - **intent:** A Manager can list and approve or reject requests of their current Direct Reports, and no one else can decide them.
  - **success:** Approval moves Reserved into a reduction of Total atomically, rejection needs a reason and releases days, requests whose First Working Day has passed cannot be approved, and every other caller receives the 403 or 404 defined by SEC-11.
- **CAP-9**
  - **intent:** Employees, Managers and Administrators see exactly the leave history they are entitled to.
  - **success:** An Employee's history never contains another Employee's data; a former Manager sees only requests they decided and never balances; Administrator reads are read-only and audited.
- **CAP-10**
  - **intent:** Authorization is enforced on the server by role and relationship, with account state re-read on every call and consistent response codes.
  - **success:** An authorization matrix (every endpoint by role by own, report and foreign resource, plus every self-dealing and restricted-target case) passes with the codes of SEC-11, and a deactivated or demoted caller is refused on the next call.
- **CAP-11**
  - **intent:** Every security-relevant and business-relevant change is recorded in an audit trail that Administrators can query and nobody can edit or delete through the application.
  - **success:** Each event in FR-25 produces an Audit Record with actor and time in the same transaction as the change, the application's database role cannot update or delete audit rows, and records contain no secrets.
- **CAP-12**
  - **intent:** The Operator controls the Company Time Zone and retention periods through configuration outside the API.
  - **success:** Invalid values stop start-up, the effective values and any change are recorded as a Security-Class audit event, and no API operation changes them.
- **CAP-13**
  - **intent:** A developer can build, run and verify the system locally one increment at a time on macOS and VS Code.
  - **success:** From a clean checkout, a local PostgreSQL container, a passing architecture-test project and an integration-test run against a real PostgreSQL container work before any feature slice is added, and each later increment keeps all tests green.

## Constraints

- ASP.NET Core Web API on .NET 10, EF Core, PostgreSQL, JWT access tokens and rotating refresh tokens; React and Azure come later and must not be precluded.
- Modular monolith of the modules in the architecture spine (People, Leave, Audit), each owning its schema, with only a `Contracts` namespace public.
- One database transaction per request; cross-module effects the PRD requires to be atomic happen inside it.
- Leave-balance and administrator-count invariants are enforced with row and advisory locks and database constraints, not application code alone.
- The API never returns or logs a plaintext password; Operator Operations have no HTTP endpoint.
- Audit is written in the business transaction and the application's database role cannot update or delete it.
- Response codes follow the ordered rules of SEC-11, and authorization never trusts a token claim for role or account state.
- Whole days only, Monday to Friday, one generic Leave Type, one organisation, one Company Time Zone (default `Asia/Kolkata`).
- Build and test one small increment at a time, in the order of PRD section 8.3 and the architecture increments.

## Non-goals

- Payroll, timesheets, attendance, HR records, reporting, dashboards, calendar sync.
- Multiple Leave Types, accrual, carry-over, half-days, public-holiday calendars, per-country rules.
- Backdated requests, editing or cancelling approved requests, delegation, escalation, Administrator override of leave decisions.
- Leave for anyone without a Direct Manager, including the Top-Most Person.
- Email-based password reset, notifications of any kind, MFA, SSO, multi-tenancy.
- Any claim of GDPR or other regulatory compliance.
- The React front end and Azure deployment in this MVP.

## Success signal

An Employee, a Manager, an Administrator and an Operator complete every journey UJ-1 to UJ-8 end to end through the API and Operator Operations in automated tests, and the concurrency, authorization-matrix, audit and no-password-leak tests all pass.

## Assumptions

- A Manager is derived from having at least one active Direct Report rather than stored as a role.
- The Operator is trusted, holds database credentials and identifies recipients by hand following a written procedure.
- The organisation has about 200 Employees; throttle, session and password-hash numbers marked indicative may be tuned during implementation.
- The bundled common-password list and its source are confirmed at scaffold.

## Open Questions

- Q-A. Which jurisdiction-specific privacy, labour or legal-retention rules would apply before any real-data use?
- Q-B. How should the Top-Most Person's leave be handled once it becomes a real need (designated approver, audited self-approval, or outside the system)?
