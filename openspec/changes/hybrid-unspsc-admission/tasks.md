# Tasks: Hybrid UNSPSC Admission

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 500–700 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | Single PR only with `size:exception` |
| Delivery strategy | single-pr |
| Chain strategy | size-exception |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| 1 | Domain admission, mapping, migration, and three-flow parity | Single PR (`size:exception`) | `dotnet test Secop.Buscador.sln` | N/A: deterministic handlers use mocks; migration is schema-only | Revert policy, callers, migration, and snapshot; scores/alerts remain |

## Phase 1: Domain and Persistence Foundation

- [x] 1.1 RED: extend `tests/Secop.Domain.Tests/ValueObjects/CodigoUnspscTests.cs` for ASCII-only eight digits, null/sentinel rejection, and six-digit class comparison.
- [x] 1.2 GREEN: update `src/Secop.Domain/ValueObjects/CodigoUnspsc.cs` with shared strict constructor validation, `TryCreate`, and `ComparteClaseCon`.
- [x] 1.3 RED: add `tests/Secop.Domain.Tests/ValueObjects/PoliticaEvaluacionTests.cs` for `0.399999`, unmatched `0.40`, additional/primary matches, and unconditional `0.45` admission.
- [x] 1.4 GREEN: update `src/Secop.Domain/Constants/PoliticaEvaluacion.cs` with the pure policy using valid provider and process evidence only.
- [x] 1.5 RED: update `tests/Secop.Domain.Tests/Entities/ProcesoTests.cs` for nullable canonical primary evidence and positional-constructor compatibility.
- [x] 1.6 GREEN: update `src/Secop.Domain/Entities/Proceso.cs` with an appended optional primary-code parameter.
- [x] 1.7 Map the nullable column in `src/Secop.Infrastructure/Persistence/Configurations/ProcesoConfiguration.cs`; run `dotnet ef migrations add AddProcesoCodigoPrincipalCategoria --project src/Secop.Infrastructure --startup-project src/Secop.Worker` and retain its designer and `src/Secop.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs`.

## Phase 2: Mapping and Admission Wiring

- [x] 2.1 RED: extend `tests/Secop.Application.Tests/DTOs/SecopProcesoDtoTests.cs` for trimmed `V1.`/`V1` primary and additional normalization; invalid values yield no evidence.
- [x] 2.2 GREEN: update `src/Secop.Application/DTOs/SecopProcesoDto.cs` and `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs` to persist validated primary evidence while keeping retrieval broad.
- [x] 2.3 RED: update `tests/Secop.Application.Tests/UseCases/SincronizarProcesosHandlerTests.cs` for borderline match admission, malformed rejection with no save/score/alert, and unchanged high-confidence flow.
- [x] 2.4 GREEN: apply `PoliticaEvaluacion.Admitir` in `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs` before scoring/persistence.
- [x] 2.5 RED: update `tests/Secop.Application.Tests/UseCases/EvaluacionProcesosHandlersTests.cs` for ad-hoc/recalculation parity, legacy-null direct/additional paths, and no delete/revoke calls.
- [x] 2.6 GREEN: apply `PoliticaEvaluacion.Admitir` in `src/Secop.Application/UseCases/Puntajes/CalcularPuntaje/CalcularPuntajeHandler.cs` and `src/Secop.Application/UseCases/Puntajes/RecalcularPuntajes/RecalcularPuntajesHandler.cs`.

## Phase 3: Refactor and Verification

- [x] 3.1 REFACTOR: remove duplicated admission checks while preserving Domain→Application dependency direction and existing alert behavior.
- [x] 3.2 Run `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj`, `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj`, `dotnet test Secop.Buscador.sln`, and `dotnet build Secop.Buscador.sln`.
