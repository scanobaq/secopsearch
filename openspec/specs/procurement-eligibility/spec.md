# Procurement Eligibility Specification

## Purpose

Ensure that only new SECOP II procurement opportunities with a usable COP amount of at least COP 60,000,000, and no direct-contracting modality, can enter the existing useful-opportunity pipeline.

## Requirements

### Requirement: COP Amount Interpretation

The system MUST treat every `precio_base` value evaluated by SECOP synchronization as an amount in Colombian pesos (COP). The system MUST NOT detect, infer, convert, persist, or expose a separate currency or currency provenance for that value.

#### Scenario: Threshold value is interpreted as COP

- GIVEN a new otherwise-useful SECOP process whose `precio_base` is `60000000`
- WHEN synchronization evaluates its eligibility
- THEN the amount is evaluated as COP 60,000,000
- AND the process satisfies the minimum-amount boundary.

#### Scenario: No currency interpretation is derived from the source text

- GIVEN a new SECOP process with a supported `precio_base` numeric literal
- WHEN synchronization evaluates its eligibility
- THEN eligibility uses that numeric value as COP
- AND no currency detection or conversion outcome is created.

### Requirement: Deterministic SECOP Amount Parsing

The system MUST derive the eligibility amount only from the `precio_base` string mapped by `SecopProcesoDto`. After trimming outer whitespace, the only supported lexical forms SHALL be an ASCII-digit integer (`DIGITS`) or an ASCII-digit decimal (`DIGITS "." DIGITS`), where `DIGITS` is one or more ASCII digits. The value MUST be representable as a decimal and greater than zero to be usable. Parsing MUST produce the same result regardless of the host culture.

The system MUST reject as unusable a null, empty, whitespace-only, zero, negative, malformed, unsupported, or decimal-unrepresentable value. Unsupported forms include signs, grouping separators, decimal commas, currency symbols, exponent notation, and embedded whitespace.

#### Scenario: Socrata integer literal parses deterministically

- GIVEN a new process whose `precio_base` is `60000000`
- WHEN the amount is parsed under any host culture
- THEN the usable amount is decimal COP 60,000,000.

#### Scenario: Socrata decimal literal parses deterministically

- GIVEN a new process whose `precio_base` is `60000000.50`
- WHEN the amount is parsed under any host culture
- THEN the usable amount is decimal COP 60,000,000.50.

#### Scenario: Locale-dependent or decorated representations are unusable

- GIVEN a new process whose `precio_base` is respectively `60.000.000`, `60,000,000`, `60000000,50`, `$60000000`, or `6e7`
- WHEN synchronization parses the amount
- THEN each value is rejected as unusable.

#### Scenario: Missing and non-positive representations are unusable

- GIVEN a new process whose `precio_base` is null, empty, whitespace-only, `0`, `0.00`, or `-60000000`
- WHEN synchronization parses the amount
- THEN each value is rejected as unusable.

### Requirement: Inclusive COP Minimum

The system MUST accept a usable non-direct-contracting COP amount equal to or greater than COP 60,000,000 and MUST reject a usable amount below COP 60,000,000.

#### Scenario: Exact minimum is eligible

- GIVEN a new otherwise-useful process with normalized modality other than direct contracting and a usable amount of COP 60,000,000
- WHEN synchronization evaluates eligibility
- THEN the amount rule accepts the process.

#### Scenario: Amount immediately below the minimum is rejected

- GIVEN a new otherwise-useful process with a usable amount of COP 59,999,999.99
- WHEN synchronization evaluates eligibility
- THEN the amount rule terminally rejects the process.

### Requirement: Unconditional Direct-Contracting Exclusion

The system MUST terminally reject every new process whose normalized modality is `ContratacionDirecta`, without exception for amount, supplier match, procurement channel, or any other attribute.

#### Scenario: Direct contracting is rejected above the amount minimum

- GIVEN a new otherwise-useful process with `modalidad_de_contratacion` normalized as `ContratacionDirecta` and a usable amount of COP 60,000,000 or greater
- WHEN synchronization evaluates eligibility
- THEN the process is terminally rejected.

#### Scenario: Direct-contracting labels use the normalized modality outcome

- GIVEN a new process with `modalidad_de_contratacion` equal to `Contratación Directa`
- WHEN the DTO normalizes the modality and synchronization evaluates eligibility
- THEN the normalized modality is treated as `ContratacionDirecta`
- AND the process is terminally rejected.

### Requirement: Existing Process IDs Remain Untouched

The system MUST determine whether a candidate ID already exists before applying the new amount or modality eligibility decision. For an existing ID, the system MUST skip the candidate unchanged and MUST NOT correct, update, or reprocess its amount, modality, process, scores, or alerts.

#### Scenario: Existing ID is skipped before eligibility evaluation

- GIVEN a candidate whose ID is already persisted, including one whose current SECOP payload has a malformed amount or direct-contracting modality
- WHEN synchronization encounters the candidate
- THEN it skips the candidate without applying the new eligibility decision
- AND it makes no update or downstream processing call for that ID.

### Requirement: Terminal Rejection Has No Downstream Effects

For a new candidate, an unusable amount, a usable amount below COP 60,000,000, or a normalized direct-contracting modality MUST be a terminal rejection. Before returning from that rejection, the system MUST NOT construct process semantic text; generate a process embedding; persist a process; calculate similarity; calculate or persist a score; send an alert; or create a review, rejection, status, or audit record.

#### Scenario: Unusable amount has no downstream effects

- GIVEN a new candidate with a null, blank, malformed, zero, or negative `precio_base`
- WHEN synchronization evaluates eligibility
- THEN no process embedding, process persistence, similarity calculation, score calculation, score persistence, or alert occurs.

#### Scenario: Low amount has no downstream effects

- GIVEN a new otherwise-useful candidate with a usable COP amount below COP 60,000,000
- WHEN synchronization evaluates eligibility
- THEN no process embedding, process persistence, similarity calculation, score calculation, score persistence, or alert occurs.

#### Scenario: Direct contracting has no downstream effects

- GIVEN a new otherwise-useful direct-contracting candidate with a usable amount at or above the minimum
- WHEN synchronization evaluates eligibility
- THEN no process embedding, process persistence, similarity calculation, score calculation, score persistence, or alert occurs.

### Requirement: Eligible Candidates Preserve the Useful-Opportunity Pipeline

A new candidate that passes the amount and modality gates MUST continue through the existing useful-opportunity pipeline. Passing eligibility MUST NOT guarantee persistence: the candidate MUST still meet the unchanged validity, open-state, RFI/special-regime, supplier-association, UNSPSC/keyword, semantic-matching, similarity, scoring, and alert rules.

#### Scenario: Eligible useful candidate continues through the established pipeline

- GIVEN a new candidate at COP 60,000,000 or more with a non-direct modality that satisfies the existing useful-opportunity conditions
- WHEN synchronization evaluates the candidate
- THEN it continues to the existing embedding, persistence, similarity, scoring, score-persistence, and alert decisions.

#### Scenario: Eligibility does not override existing usefulness rules

- GIVEN a new non-direct candidate at COP 60,000,000 or more that fails an unchanged useful-opportunity condition
- WHEN synchronization evaluates the candidate
- THEN the existing rule prevents downstream processing as it did before this change.

### Requirement: Unrelated Procurement Behavior Remains Out of Scope

The system SHALL NOT add review persistence, schema state, migration-dependent behavior, currency support, or correction/backfill/reprocessing behavior for this capability. The system MUST leave the existing framework-agreement, demand-aggregation, TVEC, Coupa, semantic text construction, supplier matching, similarity thresholds, scoring rules, and alert rules unchanged except that new candidates rejected by this specification never reach them.

#### Scenario: Rejected candidate has no durable review trail

- GIVEN a new candidate terminally rejected by an amount or direct-contracting gate
- WHEN synchronization completes
- THEN no process record, review record, rejection record, status record, or audit record exists for that candidate.

#### Scenario: Non-direct framework and channel behavior has no new special rule

- GIVEN a new non-direct candidate associated with a framework agreement, demand aggregation, TVEC, or Coupa context
- WHEN synchronization evaluates it
- THEN this capability applies only its amount and direct-contracting gates
- AND no new framework- or channel-specific eligibility behavior is applied.

#### Scenario: Eligible candidate retains established semantic and scoring decisions

- GIVEN a new candidate that passes this specification's gates
- WHEN it reaches semantic matching, scoring, and alerts
- THEN those decisions use the same semantic text, supplier matching, thresholds, scoring rules, and alert rules as before this change.
