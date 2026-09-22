# Apply Progress: Hybrid UNSPSC Admission

**Mode**: Strict TDD
**Delivery**: `size:exception` approved for a single cohesive work unit.
**Status**: 15/15 tasks complete.

## Delivery-State Evidence

- No commit, staging, or push occurred for this implementation or this documentary correction; the implementation worktree remains uncommitted.
- This correction ran only read-only VCS checks: `git status --short` reported the implementation and OpenSpec artifacts as unstaged changes, and `git diff --cached --quiet` exited `0` (no staged changes).
- No `git add`, commit, or push command was executed.

## Completed Tasks

- [x] 1.1–1.7 Domain validation, admission policy, process persistence, and EF migration.
- [x] 2.1–2.6 SECOP normalization and synchronization, ad-hoc, and recalculation wiring.
- [x] 3.1–3.2 Shared-policy refactor and required verification.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1 | `CodigoUnspscTests.cs` | Unit | 36/36 passed | Compile failure for missing API | Focused tests passed | Valid, sentinel, malformed, non-ASCII, matching/nonmatching classes | Shared ASCII validator extracted |
| 1.2 | `CodigoUnspscTests.cs` | Unit | 36/36 passed | Tests written before implementation | Focused tests passed | Constructor and `TryCreate` paths | Shared validation retained |
| 1.3 | `PoliticaEvaluacionTests.cs` | Unit | 36/36 passed | Compile failure for missing policy | Focused tests passed | Below floor, unmatched, primary/additional, direct paths | Valid-code helper extracted |
| 1.4 | `PoliticaEvaluacionTests.cs` | Unit | 36/36 passed | Tests written before implementation | Focused tests passed | Multiple admission branches | One Domain policy |
| 1.5 | `ProcesoTests.cs` | Unit | 36/36 passed | Compile failure for missing property/parameter | Focused tests passed | Legacy and canonical-primary construction | Optional parameter appended |
| 1.6 | `ProcesoTests.cs` | Unit | 36/36 passed | Tests written before implementation | Focused tests passed | Legacy-null and supplied-primary paths | No additional refactor needed |
| 1.7 | EF generated migration | Infrastructure | 56/56 passed | Linked predecessor RED: task 1.5's `ProcesoTests.cs` compile failure for the missing nullable primary property/constructor parameter established the entity evidence that must persist; task 2.1 then exercised canonical primary mapping. The migration itself is generated structural work, so it has no independent behavioral RED. | Generated migration `AddProcesoCodigoPrincipalCategoria`, designer, and model snapshot; Infrastructure schema/model suite passed 56/56 and final `dotnet build Secop.Buscador.sln` completed with 0 errors. | Nullable column, generated designer, and model snapshot validation. | N/A — generated schema artifacts were retained rather than manually refactored; this avoids model-snapshot drift while preserving the predecessor entity behavior. |
| 2.1 | `SecopProcesoDtoTests.cs` | Unit | 115/115 passed | Compile failure for missing DTO method | Focused tests passed | `V1.`/`V1`, whitespace, sentinels, and non-ASCII | Canonicalization helper extracted |
| 2.2 | `SecopProcesoDtoTests.cs` | Unit | 115/115 passed | Tests written before implementation | Focused tests passed | Primary and additional canonical mapping | Source normalization stays in DTO |
| 2.3 | `SincronizarProcesosHandlerTests.cs` | Unit | 115/115 passed | Behavioral failures for primary and invalid evidence | Focused tests passed | Borderline primary match and invalid-provider rejection | No additional refactor needed |
| 2.4 | `SincronizarProcesosHandlerTests.cs` | Unit | 115/115 passed | Tests written before implementation | Focused tests passed | Direct and evidence-gated flows | Uses Domain policy once per provider |
| 2.5 | `EvaluacionProcesosHandlersTests.cs` | Unit | 115/115 passed | Borderline rejection assertion failed before wiring | Focused tests passed | Reject/no-save and legacy-additional parity | No delete/revoke behavior added |
| 2.6 | `EvaluacionProcesosHandlersTests.cs` | Unit | 115/115 passed | Tests written before implementation | Focused tests passed | Ad-hoc and recalculation parity | Shared policy replaces floor checks |
| 3.1 | Domain/Application tests | Unit | 55/55 Domain, 123/123 Application | Linked predecessor RED scenarios: task 1.3's missing-policy compile failure covered below-floor, unmatched borderline, primary/additional matches, and direct admission; task 2.3's behavioral failures covered sync primary/invalid evidence; task 2.5's failed borderline-rejection assertion covered ad-hoc/recalculation parity and historical safety. These constrained removal of duplicated caller checks. | Post-refactor `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj` passed 55/55 and `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` passed 123/123. | All policy branches exercised across synchronization, ad-hoc scoring, and recalculation callers. | Replaced duplicated admission checks in the three Application callers with the shared `PoliticaEvaluacion.Admitir` policy, preserving Domain→Application direction and existing alert behavior. |
| 3.2 | All test projects | Integration | N/A — verification-only task; no source file was modified in this step. | Linked aggregate predecessor RED evidence: tasks 1.1, 1.3, and 1.5 established value-object, policy, and entity failures before implementation; tasks 2.1, 2.3, and 2.5 established DTO and three-flow behavioral failures before wiring. No new behavior was introduced by this verification task. | Final GREEN: `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj` passed 55/55; `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` passed 123/123; `dotnet test Secop.Buscador.sln` passed Domain 55, Application 123, Infrastructure 56 with 0 failed; `dotnet build Secop.Buscador.sln` completed with 0 errors and 34 pre-existing package vulnerability warnings. | Aggregate Domain, Application, Infrastructure, solution, and build verification of every predecessor scenario. | N/A — verification introduced no behavior or refactor; it is the GREEN successor to task 3.1's refactor and confirms its preserved behavior. |

## Test Summary

- **Total tests written**: 12 focused test methods covering 27 parameterized cases across Domain and Application suites.
- **Total tests passing**: Domain 55, Application 123, Infrastructure 56, solution 234.
- **Layers used**: Unit and schema/model integration.
- **Approval tests**: Existing focused suites protected modified files before changes.
- **Pure functions created**: `CodigoUnspsc.TryCreate`, `CodigoUnspsc.ComparteClaseCon`, and `PoliticaEvaluacion.Admitir`.

## Work Unit Evidence

| Evidence | Result |
|---|---|
| Focused test command and exact result | `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj`: passed, 55/55. `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj`: passed, 123/123. |
| Runtime harness command/scenario and exact result | N/A — handlers are deterministic mocked unit boundaries and the EF migration was validated through the Infrastructure schema/model suite (56/56); no safe external SECOP, Telegram, or database runtime boundary is available. |
| Rollback boundary | Revert `CodigoUnspsc`, `PoliticaEvaluacion`, `Proceso`, DTO/three handler callers, `ProcesoConfiguration`, `20260910225248_AddProcesoCodigoPrincipalCategoria*`, snapshot, and the matching tests. This removes only new admission behavior; historical scores and alerts remain untouched. |

## Required Verification

- `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj`: passed — 55 passed, 0 failed.
- `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj`: passed — 123 passed, 0 failed.
- `dotnet test Secop.Buscador.sln`: passed — Domain 55, Application 123, Infrastructure 56; 0 failed.
- `dotnet build Secop.Buscador.sln`: passed — 0 errors, 34 pre-existing package vulnerability warnings.

## Deviations

None — implementation follows the approved design. The initial EF command correctly refused to run without `SECOP_DB`; generation succeeded with a syntactically valid non-production direct connection string and did not connect to a database.

## Review Workload

The implementation exceeds the 700-line estimate when generated EF migration artifacts and apply evidence are included. The accepted `size:exception` remains necessary because the generated designer, snapshot, tests, migration, and policy must land together to keep the schema and behavior coherent.

Native SDD runtime accounting records exactly 885 changed lines for the actual implementation. The maintainer explicitly approved `size:exception` for those exact 885 changed lines, preserving the cohesive single-PR boundary.
