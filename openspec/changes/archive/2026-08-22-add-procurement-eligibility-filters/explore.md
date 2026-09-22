# Exploration: add-procurement-eligibility-filters

## Context and authoritative scope

This exploration supersedes the prior review-persistence assumptions. It proposes no implementation. The authoritative rules are:

- treat every SECOP amount as COP; do not detect, store, or infer currency;
- accept a usable amount of COP 60,000,000 or greater, and reject a usable lower amount;
- terminally discard missing, blank, zero, malformed, or otherwise unusable amounts;
- terminally discard every direct-contracting process, without exception;
- evaluate only IDs not already persisted; existing IDs remain untouched and are not corrected or reprocessed;
- persist only useful, eligible opportunities; and
- apply the new gates before embedding, similarity, scoring, score persistence, or alerts.

Framework agreements, demand aggregation, TVEC, and Coupa are out of scope and must remain unchanged.

## Observed flow

1. `SecopMonitorWorker` and `ProcesosController` dispatch `SincronizarProcesosCommand` to `SincronizarProcesosHandler`.
2. The handler loads suppliers and queries `ISecopApiClient.ObtenerProcesosRecientesAsync` in parallel for UNSPSC and general-keyword sources.
3. `SecopProcesoDto` maps `modalidad_de_contratacion` to `Modalidad` and `precio_base` to `Presupuesto`. `ObtenerModalidad()` maps text containing `DIRECTA` to `ModalidadContrato.ContratacionDirecta`.
4. The handler currently filters valid, open, and non-RFI/non-special-regime DTOs; associates them with suppliers that have embeddings and match UNSPSC or keywords; then skips already persisted IDs using `ExisteAsync`.
5. For each remaining candidate, it maps the DTO, generates and assigns an OpenAI embedding, persists the process, calculates similarity, persists qualifying scores, and may send an alert.
6. `MapearProceso` currently uses culture-dependent `decimal.TryParse`; a failed parse becomes `Proceso.Presupuesto = 0m`.
7. Recalculation, direct scoring, and queued-alert handlers act on persisted processes, but under the clarified rules ineligible processes will never be persisted by this change.

## Eligibility and ordering

The new decision applies only to candidate IDs that are not already persisted. For a new ID, the terminal gate is:

1. Normalize/parse `precio_base` as the single COP amount source. A blank, malformed, non-positive, or otherwise unusable value is rejected.
2. Reject `ModalidadContrato.ContratacionDirecta` regardless of amount.
3. Reject a usable amount below `60_000_000m`; accept the boundary exactly.
4. Only an accepted DTO continues through supplier association and the existing embedding, persistence, scoring, and alert path.

The ordering between the direct-contracting and unusable-amount checks is externally equivalent because both terminally discard. The implementation may check direct contracting first to avoid unnecessary parsing, provided neither branch persists or invokes AI/scoring/alerts.

The new-ID check must stay before eligibility evaluation that causes side effects, so existing persisted IDs are skipped unchanged. The eligibility gate must be before `ConstructorTextoSemantico`, `IEmbeddingService`, `IProcesoRepository.GuardarAsync`, `IPuntajeRepository.GuardarAsync`, and `IAlertaService`. It can be placed before supplier matching as well, reducing work; retaining the current candidate-only persistence semantics means an eligible process is still useful only when it matches an embedded supplier through the current UNSPSC/keyword rules.

## Smallest safe Clean Architecture boundary

Place the procurement invariant in `Secop.Domain` as a small policy/value object that accepts a normalized modality and a parsed nullable COP amount and returns eligible/rejected. The policy owns the inclusive COP 60,000,000 threshold and the direct-contracting exclusion, with no SECOP, EF, AI, or configuration dependency.

Keep conversion of `SecopProcesoDto.Presupuesto` from its external string representation at the Application boundary (the DTO helper or synchronization mapper). It need only return a usable positive `decimal` or no value; it must not introduce currency fields, provenance, detection, or review outcomes. The same parsed value feeds the policy and `MapearProceso`, eliminating the current fallback that silently maps parse failure to zero.

`SincronizarProcesosHandler` is the only production use case that needs modification. Since rejected items are never saved, there is no new process status, no entity/property change, no EF configuration, migration, snapshot update, repository change, or alternate-handler eligibility guard. Existing persisted IDs are intentionally out of scope for later scoring or correction because this change does not create such records.

Likely production touchpoints are therefore limited to:

- a new Domain eligibility policy/value object and its tests;
- `src/Secop.Application/DTOs/SecopProcesoDto.cs` if it owns amount parsing; and
- `src/Secop.Application/UseCases/Procesos/SincronizarProcesos/SincronizarProcesosHandler.cs` for new-ID gating, policy invocation, and passing the approved amount to mapping.

## Strict TDD surface

Write focused failing tests before production changes:

1. Domain tests for an amount exactly `60_000_000m` being eligible; usable amounts below it being rejected; direct contracting always being rejected; and absent, zero, or negative parsed amounts being rejected.
2. DTO/Application-boundary parsing tests covering null, blank, malformed, zero, and valid SECOP numeric lexical forms. Tests must establish the supported parse format without adding currency recognition.
3. `SincronizarProcesosHandlerTests` verifying that each terminal outcome makes no embedding, process-save, similarity, scoring, score-save, or alert call, and that an eligible new candidate reaches the existing pipeline only after the gate passes.
4. A handler test proving an existing ID is skipped before eligibility side effects and receives no update, embedding, score, or alert.
5. Update existing handler test builders/fixtures to supply an eligible `Presupuesto`, because their current open DTO helper leaves it null and the clarified rule now discards that input.

No tests for review queues, migrations, reprocessing, currency provenance, or changed recalculation/direct-score/alert behavior are required: those behaviors would require persisted ineligible records, which the authoritative rules prohibit.

## Existing-work isolation and delivery risk

The overlapping surface is the pre-existing semantic-matching work in `SincronizarProcesosHandler`, `ConstructorTextoSemantico`, `IEmbeddingService`, the 0.50/0.65 similarity gates, and related handler tests. Add the eligibility decision before that path; do not alter semantic text construction, supplier matching rules, similarity thresholds, framework-agreement handling, demand aggregation, TVEC, Coupa, or unrelated modified files. In particular, do not read or modify `src/Secop.Api/appsettings.json`.

The focused change should remain comfortably within the 400 changed-line budget. The only implementation detail to settle during proposal is the deterministic COP-number lexical format accepted by the Application parser; it is a parser contract, not a currency-detection or persistence requirement.

## Recommendation

Proceed to proposal/design with a terminal two-rule eligibility policy and a pre-embedding application gate. Do not add review persistence, currency data, schema work, correction paths, or reprocessing behavior.
