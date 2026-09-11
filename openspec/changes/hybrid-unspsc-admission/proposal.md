# Proposal: Hybrid UNSPSC Admission

## Intent

Reduce irrelevant Telegram opportunity notifications without losing high-confidence semantic matches. Borderline similarity must require valid UNSPSC class evidence while preserving broad SECOP discovery.

## Scope

### In Scope
- Apply one Domain admission policy: admit similarity `>= 0.45`; admit `>= 0.40` and `< 0.45` only with a shared six-digit UNSPSC class.
- Normalize and persist the process primary UNSPSC code; evaluate it together with additional categories and provider codes.
- Reuse the policy in synchronization, ad-hoc scoring, and recalculation; invalid, malformed, missing, and sentinel values provide no class evidence.

### Out of Scope
- Narrowing SECOP retrieval with lexical, primary-only, or upstream UNSPSC filters.
- Automatic purge, inactivation, backfill, revocation, or reconciliation of historical scores and alerts.
- Changes to eligibility, actionability, recommendations, embeddings, or alert delivery outside admission.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `procurement-eligibility`: Define hybrid semantic/UNSPSC admission for the useful-opportunity pipeline and preservation of historical evaluations.

## Approach

Extend `CodigoUnspsc` with strict, non-throwing ASCII eight-digit validation and class comparison. Add a pure Domain policy around the existing `0.40` floor and new independently admissible `0.45` threshold. Normalize source `V1` prefixes in `SecopProcesoDto`, store the nullable canonical primary code on `Proceso`, and pass persisted primary plus additional codes to all three scoring paths. Keep incomplete historical rows subject to the `0.45` path during current recalculation; leave existing now-inadmissible rows untouched.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `src/Secop.Domain/ValueObjects/CodigoUnspsc.cs` | Modified | Strict validation and class evidence helpers. |
| `src/Secop.Domain/Constants/PoliticaEvaluacion.cs` | Modified | Shared hybrid admission policy and thresholds. |
| `src/Secop.Domain/Entities/Proceso.cs` | Modified | Canonical nullable primary UNSPSC code. |
| `src/Secop.Application/DTOs/SecopProcesoDto.cs` | Modified | Primary-code normalization. |
| `src/Secop.Application/UseCases/{Procesos,Puntajes}/` | Modified | Shared admission in sync, recalculation, and ad-hoc scoring. |
| `src/Secop.Infrastructure/Persistence/` | Modified | Column configuration and migration. |
| `tests/Secop.{Domain,Application}.Tests/` | Modified | Boundary, malformed, primary/additional, and parity coverage. |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Legacy rows lack primary evidence | Medium | Preserve rows; document reconciliation as a later decision. |
| Source values are malformed | Medium | Treat them as no evidence; never lower-threshold admission. |
| Policy divergence across flows | Low | Centralize the pure Domain policy and test all callers. |

## Rollback Plan

Revert the migration and policy/caller changes as one PR rollback. Existing scores and alert history remain intact; no data restoration is required.

## Dependencies

- PostgreSQL migration deployment before code relying on the new nullable column.

## Success Criteria

- [ ] `>= 0.45` remains admissible regardless of UNSPSC evidence.
- [ ] `0.40–<0.45` is admitted only by a valid six-digit class match across primary or additional process categories.
- [ ] Synchronization, ad-hoc scoring, and recalculation use the same decision; historical scores remain unchanged.
