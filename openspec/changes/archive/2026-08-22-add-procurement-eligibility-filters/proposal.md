# Filter new procurement processes before costly processing

Only new SECOP II processes that are useful procurement opportunities should enter the existing embedding, persistence, scoring, and alert pipeline. This change introduces two terminal eligibility gates: reject every direct-contracting process and reject any process whose usable COP amount is below COP 60,000,000. Missing or unusable amounts are also rejected rather than stored for review.

## Intent

Prevent low-value, direct-contracting, and amount-invalid processes from consuming storage, AI services, scoring work, or alert attention. The resulting synchronization flow should retain only useful opportunities while leaving previously persisted data and existing semantic matching behavior unchanged.

## Product outcome

For each process ID not already persisted, synchronization will make a deterministic eligibility decision before any process embedding, persistence, scoring, or alert side effect:

| Input condition | Outcome |
|---|---|
| Direct contracting, regardless of amount | Reject and do not persist |
| Amount missing, blank, zero, negative, malformed, or otherwise unusable | Reject and do not persist |
| Usable amount below COP 60,000,000 | Reject and do not persist |
| Usable amount exactly COP 60,000,000 | Eligible to continue |
| Usable amount above COP 60,000,000 | Eligible to continue |
| ID already persisted | Skip unchanged; do not evaluate or reprocess |

Eligibility allows a new process to continue; it does not guarantee persistence. The process must still satisfy the existing validity, open-state, supplier association, UNSPSC/keyword, and downstream scoring rules that determine whether it is a useful opportunity.

## Scope

### In scope

- Treat every SECOP amount used by this flow as Colombian pesos (COP), without currency detection or conversion.
- Parse the SECOP amount into a usable positive value or treat it as unusable.
- Apply an inclusive minimum amount of COP 60,000,000.
- Exclude all direct-contracting processes with no exceptions.
- Apply the rules only to process IDs that are not already persisted.
- Run the eligibility gates before process embedding or any other AI cost, process persistence, similarity/scoring, score persistence, and alerts.
- Preserve the existing requirement that an eligible process must also be useful under current supplier and semantic matching behavior before it is persisted and processed downstream.
- Add focused automated coverage for the boundary, terminal rejection paths, side-effect prevention, and existing-ID behavior under strict TDD.

### Out of scope and unchanged

- Framework agreements, demand aggregation, TVEC, and Coupa.
- Review queues, review statuses, or persisting rejected processes for later review.
- Currency detection, currency provenance, currency fields, conversion, or multi-currency support.
- Database schema changes, migrations, or new persistence states.
- Correction, backfill, or reprocessing of existing records.
- Changes to semantic text construction, supplier matching, similarity thresholds, scoring rules, or alert rules for eligible processes.
- Eligibility changes in recalculation, direct-scoring, or queued-alert handlers; ineligible new processes never reach those persisted-record workflows.

## Acceptance boundaries

1. COP 60,000,000 is eligible; COP 59,999,999.99 is ineligible.
2. A direct-contracting process is rejected even when its amount is at or above the threshold.
3. Null, empty, whitespace-only, zero, negative, malformed, and unsupported amount representations are terminally rejected.
4. Rejection causes no process embedding, process save, similarity calculation, score save, or alert.
5. An existing process ID is skipped before the new eligibility evaluation and receives no update or downstream side effect.
6. An eligible new process follows the existing useful-opportunity pipeline without altered semantic matching or thresholds.
7. Rejected inputs do not create a process record, review record, status, or audit record.

## Affected areas

- **Domain policy:** the minimum-value and direct-contracting invariants belong in the dependency-free Domain layer.
- **SECOP application boundary:** external amount text needs deterministic parsing into a positive COP amount or an unusable result, with no currency inference.
- **Synchronization use case:** new-ID detection and eligibility must precede expensive or persistent downstream work.
- **Automated tests:** Domain boundary cases, amount parsing, handler side-effect isolation, eligible flow continuity, and existing-ID skipping require focused coverage.
- **Operations:** fewer low-value or non-actionable opportunities should be stored, scored, and alerted; rejected processes will have no durable review trail by design.

No entity, repository contract, EF configuration, database migration, API contract, worker trigger, or secrets/configuration change is expected.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| SECOP amount text is parsed inconsistently across environments | Valid opportunities could be rejected, or invalid values could pass | Define and test one deterministic lexical parsing contract at the application boundary; reject rather than default failed parsing to zero |
| Modality normalization misses a direct-contracting label | A direct contract could enter the costly pipeline | Use the existing normalized modality and cover representative direct-contracting inputs before integrating the policy |
| Gates are placed after AI or persistence work | The business cost and data-quality outcome would not be achieved | Verify no downstream service or repository interaction occurs on every rejection path |
| Existing records are accidentally re-evaluated | Historical data or alerts could change outside approved scope | Preserve ID-first skipping and test that existing records remain untouched |
| Rejected IDs recur in later SECOP polls because no rejection is stored | The same input may be evaluated repeatedly and has no audit trail | Accept this as the tradeoff of terminal non-persistence; do not introduce a review or rejection store in this change |
| Eligibility work alters semantic matching behavior | Useful eligible opportunities could produce different scores or alerts | Keep semantic construction, matching rules, and thresholds unchanged and verify the eligible path continues through existing behavior |

## Rollback

Revert or disable the new eligibility gate and deterministic amount handoff, restoring the prior synchronization behavior for future observations. No schema or stored-data rollback is required because the change adds no persisted fields or statuses and rejected processes create no records. Processes rejected while the gate was active may enter the prior pipeline later only if SECOP returns them again after rollback; already persisted records remain unaffected throughout.

## Success criteria

- No newly observed direct-contracting process is persisted, embedded, scored, or alerted.
- No newly observed process with an unusable, non-positive, or below-threshold amount is persisted, embedded, scored, or alerted.
- A new otherwise-useful process at exactly COP 60,000,000 can proceed through the unchanged downstream pipeline.
- Existing persisted IDs remain skipped, untouched, and unreprocessed.
- Eligibility rejection occurs before all process-level AI cost and persistence side effects.
- The implementation requires no migration, currency model, review workflow, or semantic matching change.
- Focused tests establish the rules before production changes, and the full solution test suite remains green.

## Proposal question round

Execution is automatic, so this proposal does not pause for an interactive round. The product questions that would normally be asked have authoritative answers from the validated exploration and final user decision:

1. **Is the goal to review rejected records or to avoid storing non-useful opportunities?** Avoid storing them; all rejection outcomes are terminal.
2. **Does the amount threshold include COP 60,000,000, and how should invalid amounts behave?** The boundary is inclusive, while missing or unusable amounts are rejected.
3. **Are any direct-contracting cases exceptions?** No; every direct-contracting process is rejected.
4. **Should historical records be corrected or reprocessed?** No; only new IDs are evaluated and existing records remain untouched.
5. **Should related procurement channels or semantic matching rules change?** No; those concerns remain explicitly out of scope.

The only implementation assumption still needing precise definition during specification/design is the deterministic set of SECOP numeric lexical forms accepted by the amount parser. Whatever forms are selected must not add currency inference, and every unsupported form must fail closed as unusable.
