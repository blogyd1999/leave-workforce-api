---
title: "Product Brief: Leave and Workforce Management API"
status: final
created: 2026-10-12
updated: 2026-10-12
---

# Product Brief: Leave and Workforce Management API

## Executive Summary

The Leave and Workforce Management API is a backend service that lets an organisation run employee leave through one trusted system. Employees submit leave requests and see their balance and history. Managers decide the requests assigned to them. Administrators create accounts and set opening balances, and making further Administrators needs a second Administrator.

It is built API-first, so a React front end and Azure hosting can follow without rework. The first version takes one narrow path end to end: provision an account, log in, request leave, get a decision, and see the balance change. Every step is protected by role-based access, employee data isolation and an audit trail.

The project is also a deliberate, incremental build: the platform is ASP.NET Core on .NET 10 as a modular monolith with PostgreSQL, each increment built and tested before the next.

## The Problem

Leave handled by email, chat or spreadsheets has predictable failures: balances drift, overlapping or over-balance leave slips through, managers lose track of what is waiting on them, and nobody can say later who approved what or when. Employees cannot see their own entitlement; HR cannot answer disputes with evidence.

## The Solution

A secured REST API that owns the leave lifecycle and enforces the rules in one place:

- A request is rejected at submission if it overlaps the employee's other pending or approved leave, or exceeds the available balance.
- Pending requests reserve balance, approval deducts it, and rejection or cancellation releases it.
- Each request goes to the employee's direct manager, who approves or rejects it. A rejection carries a reason.
- Employees see only their own data. Managers see only their direct reports' requests. Administrators manage accounts and balances, but the Administrator role gives no right to decide leave. An Administrator who is also an employee's direct manager decides as that manager.
- A sole Administrator may add the second Administrator once; after that, creating an Administrator or granting the role needs a different Administrator to approve it.
- The API never returns a password. An operator verifies the recipient and issues 24-hour, one-login temporary passwords outside the API.
- Every state change and balance change is recorded with who, what and when, in an audit trail the application cannot delete.

## What Makes This Different

Nothing here is novel, and this brief does not claim a moat. The value is correctness and a clean foundation: rules enforced server-side, a security model designed in rather than bolted on, and a modular structure that can later grow into wider workforce management (more leave types, holiday calendars, reporting) without a rewrite.

## Who This Serves

- **Employee:** submits leave, tracks status, cancels a pending request, sees balance and history. Success: knows where a request stands without asking anyone.
- **Manager:** reviews requests from direct reports and decides them. Success: a clear queue and enough information (dates, days, balance impact) to decide quickly.
- **Administrator:** provisions and deactivates accounts, assigns and clears managers, sets and adjusts balances, and co-approves new Administrators. Success: accounts and balances are correct and every change is explainable.
- **Operator:** the developer who deploys the system. Runs the bootstrap, verifies people and issues temporary passwords, recovers Administrator access and sets configuration, all outside the API. Success: no hardcoded secrets and a documented recovery path.
- **Secondary:** a future front-end developer, who needs a stable, well-documented API contract.

## Success Criteria

- An employee, a manager and an administrator can complete every MVP journey end to end through the API and the operator operations.
- After the second Administrator exists, no Administrator is created or granted without two distinct Administrators, and no password appears in any API response, log or audit record.
- It is impossible to create an overlapping request or one exceeding the available balance, including under concurrent submissions.
- An employee can never read or change another employee's data, and a manager can never act on a non-report's request. This is proved by automated tests.
- Every request status change and balance change has an audit record naming the actor and time.
- Each increment builds and passes its automated tests before the next begins.

## Scope

**In for the MVP**
- A secure, explicitly invoked, one-time bootstrap to create the first Administrator; admin-provisioned accounts thereafter (no public sign-up); operator-issued temporary passwords; and login with JWT access tokens and rotating refresh tokens.
- Creating a second Administrator once by the sole Administrator, then two-person approval for creating an Administrator or granting the role; the operator resets an Administrator's password through a documented recovery operation; restricted administrator password resets for ordinary accounts.
- Employee-manager relationship; one direct manager per employee.
- One generic leave type, counted in working days (Monday to Friday, full days only).
- Submit, view, track and cancel (while pending) leave requests; submitted requests are not edited, so a pending request is cancelled and replaced; manager approve and reject (reason required).
- Balance validation with reserve-on-submit and deduct-on-approve; no negative balances.
- Overlap prevention; request history; audit trail.
- Role-based authorization and employee data isolation.
- Admin sets and adjusts balances (reason recorded); audited reassignment or clearing of a manager, required before deactivating anyone who has direct reports; soft-deactivation of accounts.
- Retention periods and the company time zone set by operator configuration, with security events kept longer than ordinary ones.

**Out for the MVP**
- Multiple leave types, accrual, carry-over, yearly rollover, half-days, public-holiday calendar.
- Leave for anyone without a direct manager, including the top-most person (a known MVP limitation).
- Backdated requests, or approving a request whose first working day has passed; cancelling an approved request; manager delegation; admin override of decisions.
- Email-based password reset, notifications (email or push), reporting and dashboards, calendar integrations.
- MFA, SSO or external identity providers; multi-tenancy.
- The React front end and Azure deployment (planned later; the API must not preclude them).

## Vision

If the MVP holds, it becomes the foundation of a broader workforce module: configurable leave types and policies, holiday calendars, notifications, team calendars and reports, a React front end, and enterprise sign-in, all added as modules on the same core.

## Decisions (owner-approved)

The PRD (§10.1) is the authoritative decision log; each artifact also has a `.memlog.md`. The brief's earlier D1 to D18 numbering is retired. This table summarises the PRD's decision IDs.

| PRD ID | Decision |
|---|---|
| AP-1 | 404 where a resource's existence should be concealed; 403 where revealing it is acceptable. |
| AP-2 | Deactivation cancels pending requests and is refused while the employee has direct reports. |
| AP-3, AP-4 | Managers see only the balance information needed to evaluate a request; administrators read requests but the role gives no right to decide. |
| AP-5 | Password minimum 12, login throttling, access token 15 minutes, secure hashing, rotating and revocable refresh tokens. |
| AP-6, AP-7, AP-8 | No leave without a direct manager; submitted requests cannot be edited; an adjustment cannot reduce the total below reserved days. |
| AP-9 | Incremental delivery order, corrected so credentials, login and audit writing come first. |
| RV-1 | Administrator reset and operator recovery exist; lockout is not permanent. |
| RV-2, RV-3 | No self-dealing by administrators; account state, role and password state re-checked on every call. |
| RV-4 | Request span at most 366 days, start at most 2 years ahead; a request whose first working day has passed cannot be approved (it can be rejected or cancelled). |
| RV-5 | The sole Administrator may add the second Administrator once; afterwards creating or granting an Administrator needs a second Administrator. Last-administrator safeguard kept; other privileged account changes are audited limitations. |
| RV-6 | Email-based reset deferred; restricted administrator resets; an Administrator's password is reset only by operator recovery. |
| RV-7 | Audited manager clearing or reassignment before deactivating anyone with direct reports; no manager means no leave submission. |
| RV-8 | Throttling uses the connection address plus per-account progressive back-off with a ceiling; temporary passwords carry at least 128 bits of randomness. |
| RV-9 | Retention and company time zone are operator configuration; the application cannot delete audit records; security events are kept longer; tamper evidence is an architecture requirement. |
| RV-10 | An authorized operator verifies the recipient and issues temporary passwords; ticket-based issuance is out of scope; the API never returns or logs a password. |
| RV-11 | Temporary passwords last 24 hours and work for one login to complete a mandatory change; if the short session ends first, the account returns to awaiting a password for reissue. |
| Q1, Q2, Q4, Q6, Q7, Q8 | Top-most person cannot submit leave; company time zone `Asia/Kolkata`, UTC timestamps; audit readable by administrators only; adjustment limit via AP-8; fictional learning project with no compliance claim; secure repeat-safe bootstrap. |
| Q-E1 to Q-E8 | Accepted: operator-only issuance, restricted reset targets, two-person scope and 24-hour expiry, clearing a manager cancels pending requests, 24-hour passwords, three-year security retention, back-off values, no automatic expiry of stale pending requests. |
| Q-C, Q-D | Accepted: roles combine, no notice period, whole days, manager's name shown, and the 16 review-driven defaults D1 to D16. |

## Known Limitations

- **The top-most person, and anyone with no direct manager, cannot submit leave in the MVP.** There is no admin override and no delegation. This includes the bootstrapped first Administrator and anyone whose manager was cleared.
- Every new account, ordinary password reset and recovery needs an operator, because the API never exposes passwords and email-based reset is deferred.
- Deactivating an Administrator or removing the role cannot leave the system without one, but if only one remains after the second-Administrator step, the operator restores access. Beyond granting the Administrator role, privileged account changes are single-Administrator actions that are audited, not prevented. Two Administrators can collude, and the operator is trusted.
- A pending request whose first working day has passed keeps its days reserved until it is rejected or cancelled.
- Deactivating an employee or clearing a manager cancels their pending requests and releases the reserved days.
- No notifications, no editing of a name or email, and no retention rules beyond audit records. Ordinary audit events expire before balances do.
- Actual legal and regulatory requirements (retention, privacy, labour rules) are undetermined.

## Assumptions

- A1. Overlap is checked against pending and approved requests only, over working days.
- A2. The administrator role confers no approval right.
- A3. Single organisation and one company time zone.
- A4. Manager is derived from having direct reports, not a stored role.

## Open Questions (non-blocking for this learning MVP)

- Q-A. Jurisdiction-specific compliance and legal retention, before any real-data use.
- Q-B. A policy for the top-most person's leave, if it becomes a real need.
