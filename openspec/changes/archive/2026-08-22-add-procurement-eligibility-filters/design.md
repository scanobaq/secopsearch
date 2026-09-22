# Gate new procurement candidates before costly processing

The smallest safe design adds one dependency-free Domain policy, one deterministic parser on the existing SECOP DTO boundary, and one guard in `SincronizarProcesosHandler`. The guard remains after the existing-ID check and before `MapearProceso`, `ConstructorTextoSemantico`, embedding, process persistence, similarity, scoring, score persistence, and alerts. The parser-approved `decimal` is passed into mapping, so `SecopProcesoDto.Presupuesto` is never parsed twice.

## Review path

1. Review the parser and Domain contracts below.
2. Review the guarded section of `SincronizarProcesosHandler.Handle` and the changed private mapper signature.
3. Review only focused test additions and the single eligible-budget default added to the existing DTO fixture.
4. Confirm that semantic matching, thresholds, repositories, entities, schema, channels, and configuration are untouched.

## Architecture decisions

| Topic | Decision |
|---|---|
| External amount boundary | Add `SecopProcesoDto.TryObtenerPresupuestoCop(out decimal presupuestoCop)`. It returns `false` for every unusable source representation and never defaults a rejected input into an accepted value. |
| Lexical grammar | Trim outer whitespace, then accept only `[0-9]+` or `[0-9]+\.[0-9]+`. Validate ASCII characters explicitly before parsing; do not use `char.IsDigit`, because it accepts non-ASCII digits. |
| Numeric conversion | After lexical validation, call `decimal.TryParse` with `NumberStyles.AllowDecimalPoint` and `CultureInfo.InvariantCulture`; require a result greater than zero. Because `decimal.TryParse` can round over-precise fractions, compare a canonical form of the source value with the parsed decimal rendered in invariant fixed-point form (`0.############################`, 28 fractional places). A mismatch, overflow, or other unrepresentable value returns `false`. No sign, grouping, comma, currency, exponent, or embedded whitespace style is enabled. |
| Domain invariant | Add static `Secop.Domain.Services.ElegibilidadProceso` with `public const decimal PresupuestoMinimoCop = 60_000_000m` and `public static bool EsElegible(ModalidadContrato modalidad, decimal? presupuestoCop)`. It returns true only when the amount is present and at least the inclusive minimum and the modality is not `ContratacionDirecta`. |
| Domain dependencies | The policy depends only on `ModalidadContrato` and `decimal?`; it has no DTO, repository, EF, AI, configuration, or external package dependency. |
| Existing IDs | Keep `await _procesos.ExisteAsync(dto.Id!, ct)` as the first statement inside the candidate-processing loop. Existing IDs `continue` before parsing or policy evaluation and are never mapped, updated, or reprocessed. |
| Gate placement | Evaluate amount and normalized modality immediately after the existing-ID check and before mapping. Keep supplier association and candidate deduplication unchanged before this loop; those operations have no process-level AI or persistence side effects. |
| Approved-value handoff | Change the private mapper to `MapearProceso(SecopProcesoDto dto, decimal presupuestoCop, ModalidadContrato modalidad)`. Remove only its current `decimal.TryParse(dto.Presupuesto, out var presupuesto)` line and pass `presupuestoCop` to the `Proceso` constructor. |
| Rejection result | Use `continue` with no new record, status, audit entry, review item, alert, or fallback value. No rejection-reason model is needed because every new rule is terminal. |
| Existing behavior | Do not change validity/open-state/RFI/special-regime filtering, provider embeddings, UNSPSC/keyword association, semantic text, similarity/scoring thresholds, warnings, score persistence, framework/channel behavior, or alert conditions. |

### Parser contract

Intended public signature on the existing DTO:

```csharp
public bool TryObtenerPresupuestoCop(out decimal presupuestoCop)
```

The implementation uses a small private ASCII lexical validator, invariant decimal parsing, and a private exactness check. Canonicalization strips redundant leading integer zeros and trailing fractional zeros; it does not change any significant digit. Comparing that canonical source with the parsed decimal's invariant fixed-point representation prevents .NET from silently accepting an over-precise literal by rounding it. The contract is:

| `Presupuesto` after outer trim | Result |
|---|---|
| `60000000`, `60000000.50`, `00060000000.00` | `true` with the corresponding positive decimal |
| null, empty, whitespace, `0`, `0.00` | `false`; output remains the default decimal |
| `-60000000`, `+60000000` | `false` |
| `.50`, `50.`, `1.2.3` | `false` |
| `60.000.000`, `60,000,000`, `60000000,50` | `false` |
| `$60000000`, `6e7`, embedded whitespace, non-ASCII digits | `false` |
| decimal overflow or an over-precise value requiring rounding | `false` |

This helper interprets a supported number as COP by contract; it does not detect, convert, store, or expose currency metadata.

### Domain contract

Intended dependency-free API:

```csharp
public static class ElegibilidadProceso
{
    public const decimal PresupuestoMinimoCop = 60_000_000m;

    public static bool EsElegible(
        ModalidadContrato modalidad,
        decimal? presupuestoCop);
}
```

The policy is intentionally boolean. Rejection reasons would add state and branching without changing any required outcome. `ContratacionDirecta` is always false, including with a high amount; null, non-positive, and below-minimum amounts are false; exactly `60_000_000m` is true for every non-direct modality.

## Data flow and ordering

```text
SECOP DTOs
  -> existing validity/open-state/RFI/special-regime filters
  -> existing UNSPSC/keyword supplier association and ID deduplication
  -> for each candidate:
       1. IProcesoRepository.ExisteAsync(id)
          -> true: continue unchanged
       2. dto.ObtenerModalidad()
       3. dto.TryObtenerPresupuestoCop(out parsedAmount)
       4. ElegibilidadProceso.EsElegible(modality, parsed-or-null)
          -> false: terminal continue
       5. MapearProceso(dto, approvedAmount, modality)
       6. ConstructorTextoSemantico.CrearParaProceso(proceso)
       7. IEmbeddingService.GenerarEmbeddingAsync
       8. Proceso.AsignarEmbedding
       9. IProcesoRepository.GuardarAsync; increment nuevos
      10. Existing provider loop:
          IEmbeddingService.CalcularSimilitudAsync
          -> existing similarity threshold
          -> IScoringService.CalcularAsync
          -> existing warnings
          -> IPuntajeRepository.GuardarAsync
          -> existing alert threshold/chat condition
          -> IAlertaService.EnviarAlertaProcesoAsync
```

The handler can represent a failed parse as a local `decimal?` only for the policy call. On acceptance it passes the already parsed non-null `decimal` and the already normalized modality to `MapearProceso`. The mapper continues to parse dates, award value/date, and competition counters exactly as it does now.

## Failure behavior

- Unsupported, malformed, non-positive, or decimal-unrepresentable amount text fails closed and terminates only that new candidate.
- A direct-contracting candidate terminates regardless of its parsed amount or any supplier/framework/channel attribute.
- A below-minimum amount terminates; `59_999_999.99m` is rejected and `60_000_000m` is accepted.
- Rejection performs no semantic-text construction and invokes none of the embedding, process-save, similarity, scoring, score-save, or alert contracts.
- Existing-ID repository failures and all downstream exceptions retain their current propagation behavior; this change adds no catch/retry policy.
- Rejected IDs may be evaluated again in a later poll because no rejection is persisted. This is an accepted product tradeoff.
- `Handle` still returns the count of newly saved processes; rejected and existing candidates do not increment it.

## File and symbol plan

| File | Intended change |
|---|---|
| `src/Secop.Domain/Services/ElegibilidadProceso.cs` | New static policy with the minimum constant and `EsElegible(ModalidadContrato, decimal?)`. |
| `src/Secop.Application/DTOs/SecopProcesoDto.cs` | Add `TryObtenerPresupuestoCop(out decimal)` plus a private ASCII grammar validator; reuse the existing `System.Globalization` import. Do not alter modality, state, date, or JSON mapping helpers. |
| `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs` | Add the post-`ExisteAsync` gate; change only the call and signature of private `MapearProceso`; remove its budget parse/fallback and pass the approved amount and modality into `Proceso`. Keep the candidate-building and downstream blocks byte-for-byte where practical. |
| `tests/Secop.Domain.Tests/Services/ElegibilidadProcesoTests.cs` | New focused policy tests for inclusive boundary, below-boundary, unconditional direct exclusion, and null/non-positive values. |
| `tests/Secop.Application.Tests/DTOs/SecopProcesoDtoTests.cs` | Add supported/unsupported grammar, positive-value, overflow, outer-whitespace, ASCII-only, and host-culture-independence tests. |
| `tests/Secop.Application.Tests/UseCases/SincronizarProcesosHandlerTests.cs` | Add terminal side-effect tests, existing-ID ordering, eligible continuity, and approved-decimal mapping coverage. Set `Presupuesto = "60000000"` in `CrearDtoAbierto` so unrelated existing pipeline tests remain eligible under the new rule. |

No constructor injection, interface registration, entity/property change, repository contract, EF configuration, migration, API/Worker trigger, or configuration edit is required.

## Strict TDD plan

1. **Domain red:** add policy tests for `60_000_000m`, `59_999_999.99m`, high-value direct contracting, null, zero, and negative amounts; then add the policy.
2. **Parser red:** add DTO theories for both supported forms under at least two host cultures, all unsupported forms named by the specification, decimal overflow, and an over-precise literal that `decimal.TryParse` would otherwise round; then add the deterministic helper and exactness check.
3. **Handler red:** first make `CrearDtoAbierto` explicitly eligible, then add rejection tests using an otherwise-matchable provider and a new ID. Verify `nuevos == 0` and `Times.Never` for `GenerarEmbeddingAsync`, `GuardarAsync(Proceso, ...)`, `CalcularSimilitudAsync`, `CalcularAsync`, `GuardarAsync(Puntaje, ...)`, and `EnviarAlertaProcesoAsync`.
4. Add an existing-ID test with a malformed amount or direct modality. Configure `ExisteAsync` to return true and verify no downstream call; source ordering in `Handle` confirms parsing/policy are after the repository check without introducing an injectable parser solely for observation.
5. Add an eligible test using `"60000000.50"` under a culture where dot is not the decimal separator, capture the saved `Proceso`, and assert `Presupuesto == 60_000_000.50m`. This proves invariant parsing and the single approved-value handoff.
6. Preserve the existing supplier/semantic tests as regression coverage. Run focused Domain and Application tests, then `dotnet test Secop.Buscador.sln` and `dotnet build Secop.Buscador.sln`.

`ConstructorTextoSemantico` is static and pure, so the design does not add an interface only to spy on it. Its non-invocation is guaranteed by lexical placement of the guard before both `MapearProceso` and `CrearParaProceso`; the first observable downstream seam is `IEmbeddingService.GenerarEmbeddingAsync`.

## Existing-work isolation

The current handler and tests contain semantic-matching work that is outside this change. Implementation must use a surgical edit around the existing candidate loop and private mapper rather than replacing or reformatting either file.

Do not modify:

- `ConstructorTextoSemantico.CrearParaProceso` or `CrearParaProveedor`;
- `ProcesoCoincideConPalabrasClave`, `ProcesoCoincideConCodigoUnspsc`, candidate merging, or `CandidatoProceso.EsKeywordOnly`;
- `UmbralSimilitudMinima`, `UmbralAlertaProponer`, score calculations, warnings, or alert conditions;
- framework agreement modality mapping or any demand-aggregation, TVEC, or Coupa behavior;
- unrelated modified/untracked files, especially `src/Secop.Api/appsettings.json`.

The observed workspace currently has concurrent threshold-related test/code edits (`UmbralSimilitudMinima` is `0.50f` in the handler while an existing semantic boundary test refers to `0.65f`). This change must not reconcile that discrepancy. Establish the focused-test baseline before implementation and escalate it as unrelated owner work if it blocks a green run.

## Budget and rollout

The expected implementation is about 35–55 production lines and 120–190 focused test lines, staying below the 400 changed-line review budget without generated files. Keep test data table-driven to control line count.

Deployment requires no feature flag, configuration, migration, backfill, or data conversion. Roll out as a normal application/worker deployment after focused and full-suite validation. Operationally, new rejected IDs leave no durable trace and may recur on later SECOP polls. Rollback is a source revert of the policy, parser handoff, and guard; persisted rows require no rollback because the change adds no schema or state.

## Review checklist

- [ ] Existing IDs exit before parser/policy evaluation and every downstream operation.
- [ ] The parser accepts only the exact ASCII grammar under invariant culture and requires a positive decimal.
- [ ] The Domain policy owns both the inclusive COP minimum and unconditional direct-contracting exclusion.
- [ ] The mapper receives the approved decimal and does not parse `dto.Presupuesto` again.
- [ ] Every rejection test verifies no observable downstream side effect.
- [ ] Eligible candidates retain all established supplier, semantic, threshold, scoring, and alert behavior.
- [ ] No entity, repository, migration, currency model, review workflow, channel-specific rule, configuration, or secret-bearing file changes.