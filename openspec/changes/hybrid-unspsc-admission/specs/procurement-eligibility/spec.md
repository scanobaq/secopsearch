# Delta for Procurement Eligibility

## ADDED Requirements

### Requirement: Hybrid Semantic and UNSPSC Admission

The system MUST admit a provider-process pair with similarity greater than or equal to `0.45` without UNSPSC evidence. It MUST admit a pair from `0.40` inclusive to below `0.45` only when any valid provider code and the process primary or any additional category share the same six-digit UNSPSC class. It MUST reject similarity below `0.40`, even with a class match.

#### Scenario: High-confidence similarity is admitted

- GIVEN a pair with similarity `0.45` and no valid UNSPSC codes
- WHEN the admission decision is evaluated
- THEN the pair is admitted.

#### Scenario: Borderline similarity requires any category match

- GIVEN similarity `0.40` and a valid provider code class-matching an additional process category
- WHEN the admission decision is evaluated
- THEN the pair is admitted.

#### Scenario: Lower-bound and below-floor boundaries are enforced

- GIVEN pairs at `0.40` without a class match and at `0.399999` with a class match
- WHEN admission is evaluated
- THEN both pairs are rejected.

### Requirement: Canonical UNSPSC Evidence

The system MUST treat a code as valid only when its canonical value contains exactly eight ASCII digits. Source process categories MUST remove leading `V1.` or `V1` after outer-whitespace trimming; the resulting canonical primary code MUST be persisted as nullable, and additional categories MUST also be considered. Malformed values, non-ASCII numerals, `UNSPECIFIED`, `No definido`, missing values, and invalid provider codes MUST provide no class evidence.

#### Scenario: Prefixed primary code is persisted and matches

- GIVEN a process primary category ` V1.80101500 ` and provider code `80101599`
- WHEN the process is mapped and borderline admission is evaluated
- THEN primary code `80101500` is persisted and the pair is admitted.

#### Scenario: Invalid evidence cannot lower the threshold

- GIVEN a borderline pair whose primary, additional, or provider values are malformed, non-ASCII numeric, `UNSPECIFIED`, or `No definido`
- WHEN admission is evaluated
- THEN those values supply no class match and the pair is rejected unless other valid codes match.

### Requirement: Consistent Admission and Historical Preservation

Synchronization, ad-hoc scoring, and recalculation MUST apply the same admission decision. SECOP retrieval MUST remain broad and MUST NOT gain lexical, primary-only, or upstream UNSPSC filters. Legacy processes with no persisted primary code MUST remain evaluable through the `0.45` path or valid additional evidence. The system MUST preserve existing scores and alerts and MUST NOT automatically purge, backfill, revoke, or reconcile historical records.

#### Scenario: Every scoring path has parity

- GIVEN the same provider, process categories, and similarity in each scoring path
- WHEN synchronization, ad-hoc scoring, and recalculation evaluate it
- THEN each produces the same admission outcome.

#### Scenario: Legacy and historical data remain intact

- GIVEN a legacy process with a null primary code and an existing previously admitted borderline score or alert
- WHEN recalculation or synchronization runs after this change
- THEN no historical record is purged, backfilled, revoked, or reconciled.
