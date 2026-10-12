---
title: 'Result, Error and the SEC-11 ProblemDetails mapper'
type: 'feature'
ticket: '4'
created: '2026-10-12'
status: 'built'
baseline_revision: 'd37941fcd6f8596d314cb0d1c01dd03d0763cbcf'
route: 'full'
route_source: 'auto'
risk: 'medium'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md'
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/prd-leave-workforce-api/prd-leave-workforce-api.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Handlers have no shared way to return an expected failure, and nothing maps failures to the status codes of SEC-11 as RFC 9457 ProblemDetails. Without it each endpoint would invent its own codes and might leak internals.

**Approach:** Add a small Result type, an Error with one of AD-7's six kinds and a stable code, and one mapper from Error to a ProblemDetails response. Add the built-in exception handler so an unhandled exception returns a generic 500 ProblemDetails.

## Boundaries & Constraints

**Always:** Put the types in `LeaveWorkforce.BuildingBlocks`. Use exactly the kinds Unauthenticated (401), Throttled (429), Forbidden (403), NotFound (404), Validation (400), Conflict (409). Every Error has a non-blank stable machine-readable `code`, returned as the `code` extension. The response is `application/problem+json` with `status`, a fixed `title` per kind, optional `detail`, `code`, and for Validation an optional `errors` map of field to messages. `detail` is caller-authored client-safe text, never an exception message. Unhandled exceptions return 500 with `code` `internal_error` and no stack, exception message or exception type, in every environment. Provide the code constant `lock_timeout` for the Conflict that entry 1.5 will raise.

**Never:** No authentication or authorization code, no database or EF Core code (translating SQL errors to Error belongs to entry 1.5), no custom exception middleware, no validation framework, no changes to the architecture, no endpoints other than health, no secrets or internal details in any response.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Each kind | An Error of that kind with a code | Its status (401, 429, 403, 404, 400, 409), fixed title, `code` | No error expected |
| Validation fields | Validation Error with field errors | 400 with an `errors` map | No error expected |
| Lock timeout | Conflict with code `lock_timeout` | 409 with `code` `lock_timeout` | No error expected |
| Blank code | Error created with a blank code | Construction is refused | ArgumentException |
| Success | A successful Result | No Error; reading Error is not possible | InvalidOperationException on misuse |
| Unhandled exception | An endpoint throws with a secret in its message | 500 ProblemDetails, `code` `internal_error`, body has no message or stack | Handled by the built-in handler |

</frozen-after-approval>

## Code Map

- `src/BuildingBlocks/LeaveWorkforce.BuildingBlocks.csproj` -- empty project today; needs the ASP.NET Core framework reference for ProblemDetails and IResult.
- `src/Api/Program.cs` -- minimal host with `/health`; no exception handling exists, so add the built-in handler here only.
- `tests/Api.IntegrationTests/` -- xunit.v3 project with `HealthEndpointTests` using `WebApplicationFactory<Program>`; follow its style (`TestContext.Current.CancellationToken`, `using var`).
- Spine AD-7 (kinds, codes, order) and AD-22; PRD SEC-11 (status codes). `Program` is `public partial class Program`.

## Tasks & Acceptance

**Execution:**
- [x] `src/BuildingBlocks/LeaveWorkforce.BuildingBlocks.csproj` -- add the Microsoft.AspNetCore.App framework reference -- needed for the mapper
- [x] `src/BuildingBlocks/Results/` -- `ErrorKind`, `Error` with one factory per kind, `ErrorCodes.LockTimeout`, `Result` and `Result<T>` -- the Result pattern
- [x] `src/BuildingBlocks/Http/` -- one extension turning an Error into an `IResult` ProblemDetails -- the SEC-11 mapping
- [x] `src/Api/Program.cs` -- `AddProblemDetails` customised to add `internal_error`, and `UseExceptionHandler` -- generic 500
- [x] `tests/Api.IntegrationTests/` -- unit tests for Result and Error; HTTP tests of the mapper through a test-only host with one endpoint per kind; one test through the real Api factory for an unhandled exception -- covers every matrix row

**Acceptance Criteria:**
- Given an Error of each kind, when it is mapped and returned from an endpoint, then the status, title and `code` match and the content type is `application/problem+json`.
- Given a Validation Error with field errors, when mapped, then the body has an `errors` map and the other kinds have none.
- Given an endpoint that throws with a secret in the message, when called through the real Api, then the response is 500 with `internal_error` and the body contains neither the message nor `StackTrace`.
- Given the solution, when `dotnet build LeaveWorkforce.slnx` and `dotnet test --solution LeaveWorkforce.slnx` run, then the build has 0 warnings and every test in Api.IntegrationTests passes.

## Implementation Notes

- Added in `src/BuildingBlocks`: `Results/` (`ErrorKind`, `ErrorCodes` with `lock_timeout` and `internal_error`, `Error`, `Result`, `Result<T>`) and `Http/ErrorHttpExtensions.cs` (`Error.ToProblem()`); BuildingBlocks now references Microsoft.AspNetCore.App.
- `Program.cs` gained `AddProblemDetails` (clears `detail` and sets `code` `internal_error` on 500) and `UseExceptionHandler()`. No exception handler existed before, so nothing was duplicated.
- Titles are fixed per kind: Unauthenticated, Too many requests, Forbidden, Not found, Validation failed, Conflict.
- Beyond the plan: `Microsoft.AspNetCore.TestHost` 10.0.3 added to central packages and the test project for the test-only host; the mapper calls `Microsoft.AspNetCore.Http.Results.Problem` fully qualified because the `LeaveWorkforce.BuildingBlocks.Results` namespace shadows `Results`; the throwing test endpoint is added after `next(app)` so it sits behind the Api's exception handler.
- Review patches: the unhandled-exception test now runs in Development and Production; `Error.Validation` copies the field-error arrays; a test covers the copy.
- Not implemented on purpose: translating SQL errors to Error (entry 1.5), `WWW-Authenticate` and `Retry-After` headers (auth and throttling entries), a `code` on framework-generated problem responses (deferred).
- Result: 21 of 21 tests in Api.IntegrationTests pass; the build has 0 warnings. Architecture.Tests and Concurrency.Tests still report zero tests (exit 8) until entries 1.6 and 1.7. README needed no change.

## Plan Change Log

## Review Triage Log

Quick review, pass 1: 4 findings (high 0, medium 0, low 3 real or deferred, false 1).

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| The 500 test runs only in Development, but the plan says every environment | low | patch | Test is now a theory over Development and Production; both pass. |
| `Error.Validation` copies the map shallowly, so field-message arrays stay mutable | low | patch | Arrays are now copied; a new test mutates the source array and the Error is unchanged. |
| `Result<T>` implicit conversions are ambiguous for `T` convertible from Error, and the factories are duplicated | false | rejected | Only a compile error for a nonsensical `Result<Error>`; nothing misbehaves at runtime and the duplicate factories are deliberate ergonomics. |
| Framework-generated problem responses (for example 415) carry no `code` | low | defer | Not caused by this change; the ticket covers Error-mapped responses. Recorded in `deferred-work.md` for epic 8 entry 5. |

## Design Notes

The test-only host is a `WebApplication` built in the test with `UseTestServer`, so the real Api stays free of fake endpoints. The unhandled-exception test adds a throwing endpoint to the real Api through an `IStartupFilter` registered in `WithWebHostBuilder`.

## Verification

**Commands:**
- `dotnet build LeaveWorkforce.slnx` -- expected: Build succeeded, 0 warnings, 0 errors
- `dotnet test --solution LeaveWorkforce.slnx` -- expected: Api.IntegrationTests passes; Architecture.Tests and Concurrency.Tests still report zero tests (exit 8) until entries 1.6 and 1.7
