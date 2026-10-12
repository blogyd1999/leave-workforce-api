---
type: epic
title: "A developer can build, run and verify the system locally"
parent: initiative-leave-workforce-api
covers: ["CAP-13"]
assignee: ""
risk: low
---

# A developer can build, run and verify the system locally

## Description

The solution builds from a clean checkout, a local PostgreSQL 18 runs, and tests run against a real PostgreSQL container, so every later epic starts on a green, enforced foundation. This is the platform baseline and the architecture's increment 1a; the spec owns the capability.

## Outcome

A solo developer on macOS and VS Code can add a feature slice with the safety nets already in place; the signal is the spec's CAP-13 success.

## Requirements

- R1 (CAP-13): Solution builds from a clean checkout with the pinned .NET 10 SDK, six source projects and three test projects named `LeaveWorkforce.<Module>` with the reference graph of the spine (solution-structure section 1; AD-1, AD-2, AD-20).
- R2 (CAP-13): A local PostgreSQL 18 runs through compose, and `deploy/db/roles.sql` creates the roles migrator, app_rw and retention_job with default privileges for schemas that migrations create later (AD-13, AD-25).
- R3 (CAP-13): Integration tests run through WebApplicationFactory against a Testcontainers PostgreSQL 18; the health endpoint returns only up or down (AD-18, AD-20).
- R4 (CAP-13): Architecture tests enforce the project reference graph, that only Contracts namespaces are public, and that no DbContext is used outside its module (AD-1, AD-2).
- R5 (CAP-13): A concurrency test project and harness run parallel tasks against PostgreSQL (AD-4, AD-20).
- R6 (CAP-13): BuildingBlocks hold Result and Error with the SEC-11 ProblemDetails mapper, the unit-of-work pipeline with rollback and the single retry, the advisory-lock helper and paging (AD-3, AD-4, AD-7, AD-22).

## Done when

1. From a clean checkout, `dotnet build` and `dotnet test` pass on macOS with the pinned SDK.
2. An integration test starts a PostgreSQL 18 container and calls the health endpoint; compose starts the same version locally and each of the three roles can connect.
3. The architecture tests fail when a forbidden project reference, public type or cross-module DbContext use is introduced.
4. Result-to-ProblemDetails mapping, the unit of work with its retry, and the advisory-lock helper pass tests, and the concurrency harness runs two parallel tasks against PostgreSQL.
5. The work is merged to the main branch with all tests green; there is no CI pipeline in this MVP, so green means a local run.

## Boundaries

The repository scaffold, local database, test harnesses and BuildingBlocks. Not any module, endpoint or table beyond health, and not CI or Azure (both in the architecture's Deferred list).

## References

- parent — initiative-leave-workforce-api.md, section Done when
- spec — _bmad-output/initiative-leave-workforce-api/spec-leave-workforce-api/spec-leave-workforce-api.md, CAP-13
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md, AD-1, AD-2, AD-3, AD-4, AD-7, AD-13, AD-18, AD-20, AD-22, AD-25
- architecture — _bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/solution-structure.md, sections 1 and 8

## Notes

- Decision: this epic is the platform baseline and opens the build order (2026-10-12).
- Decision: no CI pipeline in this epic; it is deferred in the architecture (2026-10-12).
- Tracer bullet: entry 1, the project shells and the health endpoint through to a running Api.
- Decision: sequencing is 1 and 2 first, then 3 after both, 4 and 6 after 1, 5 after 3 and 4, and 7 after 3 (2026-10-12).
- Decision: the architecture-test library is ArchUnitNET (human's decision, 2026-10-12); entry 6 uses it.
