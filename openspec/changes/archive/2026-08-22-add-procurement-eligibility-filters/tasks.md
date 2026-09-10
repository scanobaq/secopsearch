# Implementation Tasks: Procurement Eligibility Filters

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 155–245 (35–55 production; 120–190 focused tests) |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR: policy/parser/handler gate with their focused tests |
| Delivery strategy | ask-on-risk |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Low

## Scope guard

Production edits are limited to `src/Secop.Domain/Services/ElegibilidadProceso.cs`, `src/Secop.Application/DTOs/SecopProcesoDto.cs`, and `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs`. Test edits are limited to the focused Domain policy, DTO, and synchronization-handler test files. Do not alter `src/Secop.Api/appsettings.json`, any unrelated uncommitted semantic-matching work, `UmbralSimilitudMinima` (including the existing 0.50/0.65 discrepancy), semantic text, supplier matching, framework/TVEC/Coupa behavior, repositories, entities, configuration, migrations, review persistence, reprocessing, or currency models.

## Work unit 1 — Domain eligibility policy and tests

**Start:** Domain has no eligibility policy. **Finish:** a dependency-free policy admits only non-direct modalities with positive COP amounts at or above the inclusive minimum. **Rollback boundary:** remove only `src/Secop.Domain/Services/ElegibilidadProceso.cs` and `tests/Secop.Domain.Tests/Services/ElegibilidadProcesoTests.cs`.

- [x] **RED:** Create `tests/Secop.Domain.Tests/Services/ElegibilidadProcesoTests.cs` with failing facts/theories for `60_000_000m` acceptance, `59_999_999.99m` rejection, high-value `ContratacionDirecta` rejection, and null/zero/negative rejection; run `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj` and record the expected failure caused by the missing policy. <!-- sdd-owner: implementation -->
- [x] **GREEN:** Add `src/Secop.Domain/Services/ElegibilidadProceso.cs` with `PresupuestoMinimoCop = 60_000_000m` and `EsElegible(ModalidadContrato, decimal?)`, depending only on Domain types; rerun `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj` and record the passing result. <!-- sdd-owner: implementation -->
- [x] **TRIANGULATE:** Extend the same policy test table with a value above the minimum and another non-direct modality, proving the rule is not tied to one accepted amount or modality; rerun `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj` and record the passing result. <!-- sdd-owner: implementation -->
- [x] **REFACTOR:** Consolidate only duplicated Domain test setup while retaining named boundary cases, then rerun `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj`; confirm no DTO, configuration, or infrastructure dependency entered the policy. <!-- sdd-owner: implementation -->

## Work unit 2 — Deterministic SECOP COP parser and tests

**Start:** `SecopProcesoDto.Presupuesto` is raw source text. **Finish:** `TryObtenerPresupuestoCop(out decimal)` accepts only exact positive ASCII numeric forms under invariant culture and fails closed without currency metadata. **Rollback boundary:** remove only this helper and its focused tests from `src/Secop.Application/DTOs/SecopProcesoDto.cs` and `tests/Secop.Application.Tests/DTOs/SecopProcesoDtoTests.cs`.

- [x] **RED:** Add failing `TryObtenerPresupuestoCop` theories in `tests/Secop.Application.Tests/DTOs/SecopProcesoDtoTests.cs` for trimmed `60000000`, `60000000.50`, and `00060000000.00`; null/blank/zero/non-positive input; signs, decimal commas, grouping, currency, exponent, malformed decimals, embedded whitespace, and non-ASCII digits; decimal overflow; and an over-precise fraction that would round. Run `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` and record the expected failure before implementation. <!-- sdd-owner: implementation -->
- [x] **GREEN:** Add `TryObtenerPresupuestoCop(out decimal presupuestoCop)` and private helpers in `src/Secop.Application/DTOs/SecopProcesoDto.cs`: trim outer whitespace, explicitly validate `[0-9]+(\.[0-9]+)?`, parse with `NumberStyles.AllowDecimalPoint` and `CultureInfo.InvariantCulture`, require positivity, and reject overflow or rounded values through canonical exactness comparison. Rerun `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` and record the passing result. <!-- sdd-owner: implementation -->
- [x] **TRIANGULATE:** Run supported integer and decimal cases while temporarily using at least two host cultures, including one where dot is not the decimal separator, and assert identical decimal results; retain this culture-independence coverage in `tests/Secop.Application.Tests/DTOs/SecopProcesoDtoTests.cs`, then rerun its project command and record the passing result. <!-- sdd-owner: implementation -->
- [x] **REFACTOR:** Make the parser test data table-driven and simplify only its new private lexical/canonical helpers without broad DTO cleanup; rerun `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` and verify existing modality, state, date, and JSON helpers are unchanged. <!-- sdd-owner: implementation -->

## Work unit 3 — New-ID synchronization gate, approved-value handoff, and tests

**Start:** `SincronizarProcesosHandler` maps `Presupuesto` with a fallback before downstream processing. **Finish:** existing IDs skip first; new ineligible candidates stop before semantic text and observable downstream seams; eligible candidates map the parser-approved decimal once and retain the established pipeline. **Rollback boundary:** revert only the handler gate/mapper signature plus the focused handler tests and fixture budget default; no data or schema rollback is required.

- [x] **RED:** In `tests/Secop.Application.Tests/UseCases/SincronizarProcesosHandlerTests.cs`, set `CrearDtoAbierto` to `Presupuesto = "60000000"` to preserve unrelated existing pipeline cases, then add failing new-ID tests for malformed/null/zero/negative amount, `59_999_999.99`, and normalized `Contratación Directa` at or above the minimum. For each terminal path assert `nuevos == 0` and `Times.Never` for embedding generation, process save, similarity, scoring, score save, and alert calls; run `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` and record the expected failures. <!-- sdd-owner: implementation -->
- [x] **GREEN:** In `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs`, retain `ExisteAsync(dto.Id!, ct)` as the first candidate-loop statement, then obtain normalized modality and parser result, call `ElegibilidadProceso.EsElegible`, and `continue` on rejection before `MapearProceso` or semantic text. Change only the private mapper call/signature to receive approved `decimal presupuestoCop` and `ModalidadContrato modalidad`, remove its `decimal.TryParse(dto.Presupuesto, ...)` fallback, and pass the supplied values to `Proceso`; rerun the Application test project and record the passing result. <!-- sdd-owner: implementation -->
- [x] **TRIANGULATE:** Add an existing-ID case with malformed amount or direct modality that sets `ExisteAsync` true and verifies no mapping/downstream observable calls, plus an eligible `"60000000.50"` case under a dot-incompatible host culture that captures the saved `Proceso` and asserts `Presupuesto == 60_000_000.50m`; rerun `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` and record the passing result. <!-- sdd-owner: implementation -->
- [x] **REFACTOR:** Keep only surgical edits around the candidate loop, private mapper, and focused tests; remove new duplication without modifying `ConstructorTextoSemantico`, candidate merging, supplier/UNSPSC/keyword matching, thresholds, warnings, scoring, alerts, or channel behavior. Rerun `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj`, inspect the diff, and escalate—not fix—any focused baseline failure caused by the unrelated 0.50/0.65 semantic-threshold discrepancy. <!-- sdd-owner: implementation -->

## Final implementation verification

The exact repository validation commands from `openspec/config.yaml` are:

```bash
dotnet build Secop.Buscador.sln
dotnet test Secop.Buscador.sln
```

Runtime harness: N/A — the change has no new API, worker trigger, configuration, or external-service boundary; focused mock-based handler tests are the observable side-effect boundary.

- [x] Run `dotnet build Secop.Buscador.sln` followed by `dotnet test Secop.Buscador.sln`, record exact outcomes, and confirm the final diff stays within the stated scope and 400 changed-line budget; preserve and report any unrelated pre-existing test failure without editing its owner surface. <!-- sdd-owner: implementation -->

## Parent post-apply notes

Ordinary bounded review is not started automatically by SDD; review actors run only when an ordinary review transaction is explicitly started outside SDD.

Parent budget confirmation is routed through verify/lifecycle status, not an implementation task; the recorded build and full-suite results already support the under-400-line forecast.
