# Spec Delta

## ADDED Requirements

### Requirement: Retry once with corrective feedback
When a model response is rejected, the system SHALL request one corrected response that includes the rejection reasons. If that response is also rejected, the run MUST fail with reason VALIDATION_FAILED, and no findings from either response may be stored or returned.

#### Scenario: Corrected response is accepted
- **GIVEN** a first response rejected for EVIDENCE_NOT_FOUND
- **WHEN** the system requests a correction
- **THEN** the correction request includes the reason EVIDENCE_NOT_FOUND and the path of the failing field
- **AND** the run completes with the corrected response's findings

#### Scenario: Second rejection fails the run
- **GIVEN** a first response and a corrected response that are both rejected
- **WHEN** processing finishes
- **THEN** the run fails with reason VALIDATION_FAILED
- **AND** the run returns no findings

## MODIFIED Requirements

### Requirement: Enforce limits the schema cannot express
The system SHALL reject text fields that are empty or whitespace only, where a NUL character (U+0000) counts as whitespace because stored text cannot hold it (reason BLANK_FIELD), and a title longer than 120 characters or a summary longer than 1,000 characters (reason FIELD_TOO_LONG), because the output contract cannot express these limits.

#### Scenario: Blank suggested action is rejected
- **GIVEN** a finding whose suggested action is three spaces
- **WHEN** the response is validated
- **THEN** it is rejected with reason BLANK_FIELD at path `findings[0].suggested_action`

#### Scenario: Overlong title is rejected
- **GIVEN** a finding with a 200-character title
- **WHEN** the response is validated
- **THEN** it is rejected with reason FIELD_TOO_LONG at path `findings[0].title`
