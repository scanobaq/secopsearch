## Exploration: hybrid UNSPSC admission

### Current State
New-process synchronization fetches broadly because `SincronizarProcesosHandler` calls `ObtenerProcesosRecientesAsync` without UNSPSC filters. It embeds every otherwise eligible new process, admits each provider pair at the single inclusive `0.40` similarity threshold, persists the process only when at least one pair qualifies, then persists a score and may send a Telegram alert. Recalculation and single-pair scoring independently use the same threshold.

`CodigoUnspsc` already validates an 8-digit code and exposes the first-six-digit class, but no admission path uses it. The process DTO maps the primary category but does not normalize or persist it; it only persists additional categories, which may include malformed or sentinel values. Therefore recalculation cannot apply a primary-plus-additional code rule consistently today. The false positive `CO1.REQ.10974377` at `0.44467236` is admitted by the current threshold; its primary `V1.80101600` does not class-match the provider code `80101500`.

### Affected Areas
- `src/Secop.Domain/ValueObjects/CodigoUnspsc.cs` — needs a non-throwing validation path or companion policy support for malformed source values.
- `src/Secop.Domain/Entities/Proceso.cs` — must retain the normalized primary UNSPSC code for later recalculation.
- `src/Secop.Domain/Constants/PoliticaEvaluacion.cs` — currently represents only the 0.40 global threshold.
- `src/Secop.Application/DTOs/SecopProcesoDto.cs` — owns SECOP `V1` prefix removal and maps primary/additional categories.
- `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs` — applies initial provider-process admission and process mapping.
- `src/Secop.Application/UseCases/Puntajes/RecalcularPuntajes/RecalcularPuntajesHandler.cs` — must share the admission policy to avoid reintroducing rejected pairs.
- `src/Secop.Application/UseCases/Puntajes/CalcularPuntaje/CalcularPuntajeHandler.cs` — must use the same policy for ad-hoc scoring.
- `src/Secop.Infrastructure/Persistence/Configurations/ProcesoConfiguration.cs` and migrations — require a nullable primary-category column and model snapshot update.
- `tests/Secop.Domain.Tests/ValueObjects/CodigoUnspscTests.cs`, `tests/Secop.Application.Tests/DTOs/SecopProcesoDtoTests.cs`, and handler tests — need boundary, malformed, primary/additional, and recalculation coverage.

### Approaches
1. **Shared domain admission policy with persisted primary code** — Add a pure Domain policy that admits `similarity >= 0.45`, or `0.40 <= similarity < 0.45` only when valid provider and process codes share a six-digit class. Persist the normalized process primary code and evaluate it with persisted additional categories.
   - Pros: One deterministic rule for synchronization, recalculation, and ad-hoc scoring; preserves broad retrieval; handles malformed values as no evidence; fixes the primary-code persistence gap.
   - Cons: Requires a small schema migration and explicit behavior for pre-migration rows.
   - Effort: Medium.

2. **Handler-local checks using DTO categories** — Add class matching directly in synchronization and keep the process model unchanged.
   - Pros: Smaller initial code diff.
   - Cons: Recalculation and ad-hoc scoring cannot reliably reproduce the decision; duplicates parsing and validation; leaves historical behavior inconsistent.
   - Effort: Low initially, High in maintenance risk.

### Recommendation
Choose the shared Domain policy. Keep `0.40` as the lower semantic floor and add a `0.45` independently admissible threshold. Source-format normalization belongs in `SecopProcesoDto` (remove `V1`), while generic eight-ASCII-digit validation and class comparison belong in the Domain. The policy should ignore null, sentinel (`UNSPECIFIED`, `No definido`), malformed, and nonmatching codes rather than granting the lower threshold. Add a nullable canonical primary code to `Proceso`; do not use UNSPSC to restrict `SecopApiClient` retrieval or its primary-only query options.

Recalculation must apply the policy to every active persisted pair. It currently skips below-threshold pairs and has no delete operation, so a decision is required for already persisted `0.40–<0.45` scores that no longer qualify: either explicitly remove/inactivate them during recalculation or preserve them only until an intentional reset/backfill. The recommended product behavior is explicit removal/inactivation, but it needs repository and alert-history semantics specified before implementation.

### Risks
- A new primary-category column is required because existing persisted processes cannot prove class evidence from the DTO after synchronization; pre-migration rows will default to the `0.45` rule unless backfilled.
- Current score upsert cannot remove a now-inadmissible pair, so recalculation alone can leave stale alertable evaluations visible.
- `CodigoUnspsc` uses `char.IsDigit`, which accepts non-ASCII numerals; the policy must define valid codes as eight ASCII digits if source normalization is meant to be strict.
- Existing tests intentionally admit unrelated borderline matches; they must be replaced with named class-evidence scenarios rather than retained as threshold-only assertions.

### Ready for Proposal
Yes — propose a single shared admission policy, persisted normalized primary category, and explicit stale-score reconciliation decision. The expected implementation is likely within the 400-line single-PR budget if reconciliation remains limited to evaluation persistence; broader historical backfill or alert revocation should be a separate change.
