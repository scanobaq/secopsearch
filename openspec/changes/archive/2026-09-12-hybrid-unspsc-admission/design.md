# Design: Hybrid UNSPSC Admission

## Technical Approach

Add one pure Domain admission decision used before scoring/persistence in synchronization, ad-hoc scoring, and recalculation. Per the delta spec, a pair is admitted at `>= 0.45`; from `>= 0.40` to `< 0.45`, it also needs a six-digit UNSPSC class match between any valid provider code and the process primary or additional codes. Retrieval remains broad, and no score total is introduced.

## Architecture Decisions

| Decision | Options and trade-off | Choice and rationale |
|---|---|---|
| Admission ownership | Handler-local checks duplicate behavior; Application service adds an unnecessary dependency. | Extend `PoliticaEvaluacion` with a pure admission method. Domain already owns the floor; one deterministic dependency-free policy prevents flow drift. |
| Code handling | Throwing constructor rejects untrusted legacy/source values; permissive Unicode digit checks admit invalid evidence. | Add non-throwing `CodigoUnspsc.TryCreate` using exactly eight ASCII digits, plus class comparison. Keep the constructor strict and make it use the same validation. Invalid/null/sentinel values contribute no evidence. |
| SECOP normalization | Domain-aware `V1` stripping would couple generic validation to one source. | `SecopProcesoDto` trims then removes only leading `V1.` or `V1` for primary and additional source fields. Domain accepts only canonical eight-digit values. |
| Persistence compatibility | Reordering constructor parameters breaks positional callers. | Add nullable `CodigoPrincipalCategoria` to `Proceso` and append its optional constructor parameter. EF hydrates legacy null rows; new mapping persists only a validated canonical primary code. |
| Historical behavior | Recalculation deletion/revocation changes history. | Recalculation skips newly inadmissible pairs without deletion. Existing scores and alerts are neither purged, backfilled, revoked, nor reconciled. |

## Data Flow

```text
SECOP DTO --source V1 normalization--> Proceso(primary nullable, additional)
                                       |
Provider codes + process codes + similarity
                                       v
                           PoliticaEvaluacion.Admitir(...)
                            | false              | true
                         no scoring          scoring -> upsert/alert
```

`0.399999` always rejects. `0.40` is inclusive but needs evidence; `0.45` is independently inclusive and needs none. A match compares `CodigoClase` (first six digits), across every provider code and the primary plus every additional process code. A legacy null primary can still admit at `>= 0.45` or through valid additional evidence.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/Secop.Domain/ValueObjects/CodigoUnspsc.cs` | Modify | ASCII validation, non-throwing parse, six-digit comparison. |
| `src/Secop.Domain/Constants/PoliticaEvaluacion.cs` | Modify | `0.40` floor, `0.45` direct-admission constant, pure policy. |
| `src/Secop.Domain/Entities/Proceso.cs` | Modify | Nullable canonical primary code; backward-compatible constructor. |
| `src/Secop.Application/DTOs/SecopProcesoDto.cs` | Modify | Source-only `V1` normalization and validated primary mapping. |
| `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs` | Modify | Map primary and call policy per provider. |
| `src/Secop.Application/UseCases/Puntajes/{CalcularPuntaje,RecalcularPuntajes}/*Handler.cs` | Modify | Replace floor-only checks with the policy. |
| `src/Secop.Infrastructure/Persistence/Configurations/ProcesoConfiguration.cs` | Modify | Nullable `codigo_principal_categoria` column mapping. |
| `src/Secop.Infrastructure/Persistence/Migrations/*` | Create/Modify | Add nullable column; generated designer and model snapshot. |
| `tests/Secop.{Domain,Application}.Tests/**` | Modify | Policy, DTO/entity, and all-flow parity coverage. |

## Interfaces / Contracts

```csharp
public const float UmbralSimilitud = 0.40f;
public const float UmbralAdmisionDirecta = 0.45f;
public static bool Admitir(
    float similitud, IEnumerable<string> codigosProveedor,
    string? codigoPrincipalProceso, IEnumerable<string> categoriasAdicionales);

public static bool TryCreate(string? valor, out CodigoUnspsc? codigo);
public bool ComparteClaseCon(CodigoUnspsc otro);
```

The policy returns `false` for invalid inputs rather than throwing. It must not alter eligibility, actionability, recommendation, embeddings, querying, or alert rules.

## Testing Strategy

| Layer | What to Test | Approach |
|---|---|---|
| Domain | `0.399999`, `0.40`, `0.45`; primary/additional/provider class matches; ASCII-only and sentinel/malformed evidence | RED tests for `CodigoUnspsc` and `PoliticaEvaluacion`, then implement. |
| Application | `V1` primary persistence; constructor/EF-null compatibility; sync, ad-hoc, and recalculation parity | RED focused handler/DTO tests; assert no scoring/save for rejected new pairs and no delete/revoke calls. |
| Infrastructure | Nullable mapping and generated migration/schema snapshot | Validate migration generation and focused Infrastructure tests if present. |
| Verification | Focused Domain/Application projects after each green step, then `dotnet test Secop.Buscador.sln` and `dotnet build Secop.Buscador.sln` | Strict RED-GREEN-REFACTOR; preserve unrelated work. |

## Threat Matrix

N/A — no routing, shell, subprocess, VCS/PR automation, executable-file classification, or process-integration boundary.

## Migration / Rollout

Deploy the nullable-column migration before application code that writes it; null preserves all legacy rows. Deploy code next. Roll back code first, then only roll back the schema after confirming no deployed code requires the column. No purge, backfill, feature flag, or historical score/alert revocation is permitted.

## Open Questions

None.
