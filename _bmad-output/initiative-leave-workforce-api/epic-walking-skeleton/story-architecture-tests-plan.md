---
title: 'Architecture tests'
type: 'chore'
ticket: '6'
created: '2026-10-12'
status: done
baseline_revision: '9e31f6bd95d98ea46e2b0bab71e9d47cb00bb3c5'
route: 'full'
route_source: 'auto'
risk: 'low'
review: 'quick'
review_source: 'pinned'
lenses_ran: ['quick']
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/architecture-leave-workforce-api.md'
  - '{project-root}/_bmad-output/initiative-leave-workforce-api/architecture-leave-workforce-api/solution-structure.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** AD-1 and AD-2 fix the module layout, the dependency direction and the public surface, but nothing enforces them. Without tests the boundaries will erode as modules grow.

**Approach:** Fill `Architecture.Tests` with rules written in ArchUnitNET (the human's chosen library, decided 2026-10-12) plus a project-reference check, each written as a function that is applied to the real solution and, to prove it can fail, to deliberately bad fixture types.

## Boundaries & Constraints

**Always:** Cover exactly the rules of AD-1 and AD-2: (1) the project reference graph of the spine: People references Leave, Audit and BuildingBlocks; Leave references Audit and BuildingBlocks; Audit references BuildingBlocks; BuildingBlocks references nothing; Api and Operator reference People, Leave, Audit and BuildingBlocks; (2) type-level dependency direction: no module type depends on a higher module (Leave on People, Audit on Leave or People), BuildingBlocks depends on no module, and one module reaches another only through its `LeaveWorkforce.<Module>.Contracts` namespace; (3) public surface: in People, Leave and Audit only types in `LeaveWorkforce.<Module>.Contracts` are public; (4) DbContext containment: a type deriving from EF Core `DbContext` lives in a module assembly, is internal, and is not used by any other module, host or BuildingBlocks; (5) naming: each `src` assembly is `LeaveWorkforce.<Name>` and every type's namespace starts with it. Write each rule as a reusable function over an architecture and a module definition, so the same rule runs on fixtures. Use `TngTech.ArchUnitNET.xUnitV3`, central package versions and the existing xunit.v3 style. Never use `Skip`.

**Never:** No production code changes, no new project, no second architecture-test library, no rules beyond AD-1 and AD-2 (slice layout, naming of files, test rules), no weakening a rule so a violation passes, no change to the Api or modules.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Real solution | The six `src` assemblies and projects | Every rule passes | No error expected |
| Forbidden project reference | A reference graph with Leave referencing People | The graph rule fails and names the edge | Not applicable |
| Forbidden type dependency | Fixture type in a lower module using a higher module, or using another module's non-Contracts type | The dependency rule fails | Not applicable |
| Allowed dependency | Fixture type using another module's Contracts type in the allowed direction | The dependency rule passes | Not applicable |
| Public surface | Fixture module with a public type outside Contracts | The surface rule fails | Not applicable |
| DbContext | Fixture DbContext that is public, or used by another module | The containment rule fails | Not applicable |
| Naming | Fixture type whose namespace does not start with its assembly name | The naming rule fails | Not applicable |

</frozen-after-approval>

## Code Map

- `tests/Architecture.Tests/` -- empty xunit.v3 shell referencing all six `src` projects (and, transitively, EF Core Relational through BuildingBlocks); add the package, rules, tests and fixtures here.
- `src/*/` -- People, Leave and Audit each hold one public `Contracts/<Module>Module.cs`; BuildingBlocks holds public Result, Error, Paging and UnitOfWork types; Api and Operator are hosts. The module and host assemblies are `LeaveWorkforce.People`, `.Leave`, `.Audit`, `.BuildingBlocks`, `.Api`, `.Operator`.
- `Directory.Packages.props` -- add `TngTech.ArchUnitNET.xUnitV3` 0.13.4 (verified on NuGet, targets .NET Standard 2.0, depends on `xunit.v3.assert` and the core package).
- Spine AD-1, AD-2 and the dependency diagram; `solution-structure.md` section 1 table.
- Fixtures live inside the test assembly under a `Fixtures` namespace and stand in for modules, so no extra project is needed; the project-reference rule is a pure function over a parsed reference map, so a bad map proves it.

## Tasks & Acceptance

**Execution:**
- [x] `Directory.Packages.props`, `tests/Architecture.Tests/*.csproj` -- add the ArchUnitNET xUnit v3 package -- the library choice
- [x] `tests/Architecture.Tests/` -- module definitions, the five rules as reusable functions, a project-reference reader for `src/*/*.csproj`, and the tests that apply them to the real solution -- the enforcement
- [x] `tests/Architecture.Tests/Fixtures/` and negative tests -- bad and good fixture types proving each rule fails and passes as the matrix says -- proof the rules can fail
- [x] `README.md` -- one short note on what the architecture tests enforce -- documentation

**Acceptance Criteria:**
- Given the solution, when `dotnet test` runs Architecture.Tests, then every rule passes on the real assemblies and projects.
- Given each bad fixture, when its rule runs, then it fails with a message naming the offender.
- Given the good fixture, when the dependency rule runs, then it passes.
- Given the solution, when `dotnet build LeaveWorkforce.slnx` runs, then it has 0 warnings and the other test projects still pass.

## Implementation Notes

- Library decision: ArchUnitNET, chosen by the human on 2026-10-12 and recorded in the epic notes; package `TngTech.ArchUnitNET.xUnitV3` 0.13.4, central version in `Directory.Packages.props`, referenced from `Architecture.Tests`.
- `tests/Architecture.Tests/`: `ModuleDefinition.cs` (module and module-set definitions), `RealSolution.cs` (the six `src` assemblies and the spine's expected reference graph), `ProjectReferenceReader.cs` (reads `src/*/*.csproj`), `ArchitectureRules.cs` (the five rules as reusable functions: reference graph as a pure function over a parsed map, and dependency direction with Contracts-only reach, public surface, DbContext containment and naming through ArchUnitNET custom conditions), `ArchitectureRuleTests.cs` (6 tests on the real solution and 13 fixture tests), `Fixtures/FixtureTypes.cs` and `FixtureSets.cs` (bad and good fixture types inside the test assembly, one namespace root per scenario). README has a short "Architecture tests" section.
- Interpretation of the ticket's "test fixture project": the plan adds fixture types inside `Architecture.Tests` instead of a fourth project, so the solution keeps its three test projects; the project-reference rule is a pure function, so a bad map proves it fails.
- Deviation: the naming rule exempts the compiler-generated global-namespace `Program` entry point of the Api and Operator; without it the rule fails on the real solution and the plan bars production changes. The README mentions it.
- Known limits: no real `DbContext` exists in `src` yet, so DbContext containment has no real-world check until one does (fixtures prove it can fail); the dependency-direction rule covers module types and BuildingBlocks (the plan says "module") and hosts are covered by the reference-graph, DbContext and naming rules; a test checks that each real assembly has types so the rules cannot pass vacuously.
- Verification: build 0 warnings; 82 of 82 tests pass (19 in Architecture.Tests, 63 in Api.IntegrationTests); Concurrency.Tests still reports zero tests (exit 8) until entry 1.7. Mutation checks by hand: adding a public type outside `Contracts` to Leave made `Only_Contracts_is_public_in_a_module` fail naming `LeaveWorkforce.Leave.Oops`; adding a Leave-to-People project reference already fails MSBuild with a circular-dependency error. Both mutations were reverted.

## Plan Change Log

## Review Triage Log

Quick review, pass 1: 4 findings (high 0, medium 0, low 3, bookkeeping 1).

| Finding | Verdict | Route | Evidence |
|---|---|---|---|
| The assembly-name half of the naming rule builds the expected name from the same string on the real solution | low | rejected | A renamed assembly makes `Assembly.Load("LeaveWorkforce.<Name>")` fail, so the run still fails loudly (as a load error); a renamed project is unlikely and the fix is a new test. |
| Hosts are excluded from the dependency-direction rule and no fixture shows a host using a DbContext | low | rejected | The plan scopes rule 2 to modules; the DbContext rule's branch for types outside the module's namespace is exercised by the `BadDbUsed` fixture. |
| Nested types are skipped in the public-surface rule | false | rejected | A nested type is reachable only through its public parent, which the rule flags. |
| Plan checkboxes and status not updated | n/a | n/a | Bookkeeping, done with this commit. |

## Design Notes

ArchUnitNET reads assemblies, so it can check types and their dependencies but not `ProjectReference` entries; the reference graph is therefore read from the project files. Module rules are parameterised by a small definition (name, assembly and namespace prefix) so the real modules and the fixtures use the same code.

## Verification

**Commands:**
- `dotnet build LeaveWorkforce.slnx` -- expected: 0 warnings, 0 errors
- `dotnet test --solution LeaveWorkforce.slnx` -- expected: Architecture.Tests and Api.IntegrationTests pass; Concurrency.Tests reports zero tests (exit 8) until entry 1.7
