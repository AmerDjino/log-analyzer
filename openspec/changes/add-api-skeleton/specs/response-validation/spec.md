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
