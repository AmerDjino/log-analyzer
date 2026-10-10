# response-validation Specification

## Purpose
Defines how every model response is checked against the analysis contract and the submitted log before the system stores or shows any part of it.

## Requirements

### Requirement: Reject incomplete responses
The system SHALL reject a response that the model service reports as cut off, because the model reached its output limit or its context window (reason TRUNCATED), or as a refusal (reason REFUSED), without parsing its content.

#### Scenario: Truncated response is rejected
- **GIVEN** a response whose stop reason says the output limit was reached
- **WHEN** the response is validated
- **THEN** it is rejected with reason TRUNCATED

#### Scenario: Refusal is rejected
- **GIVEN** a response whose stop reason is a refusal
- **WHEN** the response is validated
- **THEN** it is rejected with reason REFUSED

### Requirement: Require a single JSON document
The system SHALL reject a response whose text is not exactly one JSON object, including JSON wrapped in a markdown code fence or surrounded by prose, with reason NOT_JSON.

#### Scenario: Fenced JSON is rejected
- **GIVEN** a response whose text is a valid analysis wrapped in a markdown code fence
- **WHEN** the response is validated
- **THEN** it is rejected with reason NOT_JSON

#### Scenario: Prose around JSON is rejected
- **GIVEN** a response whose text is "Here is the analysis:" followed by a valid analysis
- **WHEN** the response is validated
- **THEN** it is rejected with reason NOT_JSON

#### Scenario: Malformed JSON is rejected
- **GIVEN** a response whose text ends before its closing brace
- **WHEN** the response is validated
- **THEN** it is rejected with reason NOT_JSON

### Requirement: Enforce the output contract
The system SHALL reject a response that breaks the published output contract (missing or unknown fields, wrong types, values outside a closed set) with reason SCHEMA_VIOLATION and the path of each violation. Closed-set values MUST be matched case-insensitively and stored in lowercase.

#### Scenario: Wrong type is rejected
- **GIVEN** a finding whose cited line numbers are the string "12-14"
- **WHEN** the response is validated
- **THEN** it is rejected with reason SCHEMA_VIOLATION at path `findings[0].line_refs`

#### Scenario: Unknown field is rejected
- **GIVEN** a finding with an extra field "confidence"
- **WHEN** the response is validated
- **THEN** it is rejected with reason SCHEMA_VIOLATION at path `findings[0].confidence`

#### Scenario: Closed-set values are matched case-insensitively
- **GIVEN** a finding with severity "Critical" and category "Resource"
- **WHEN** the response is validated
- **THEN** it is accepted with severity "critical" and category "resource"

### Requirement: Verify citations against the submitted log
Every cited line number SHALL be between 1 and the number of lines in the submitted log (otherwise reason LINE_OUT_OF_RANGE), and each finding's evidence SHALL appear verbatim, ignoring leading and trailing whitespace, on at least one of its cited lines (otherwise reason EVIDENCE_NOT_FOUND).

#### Scenario: Citation outside the log is rejected
- **GIVEN** a submitted log of 120 lines
- **AND** a finding that cites line 340
- **WHEN** the response is validated
- **THEN** it is rejected with reason LINE_OUT_OF_RANGE at path `findings[0].line_refs[0]`

#### Scenario: Line zero is rejected
- **GIVEN** a finding that cites line 0
- **WHEN** the response is validated
- **THEN** it is rejected with reason LINE_OUT_OF_RANGE

#### Scenario: Paraphrased evidence is rejected
- **GIVEN** line 12 reads "fail: Orders.Api[0] Database connection pool exhausted"
- **AND** a finding cites line 12 with evidence "The connection pool ran out"
- **WHEN** the response is validated
- **THEN** it is rejected with reason EVIDENCE_NOT_FOUND at path `findings[0].evidence`

#### Scenario: Evidence from an uncited line is rejected
- **GIVEN** a finding whose evidence appears verbatim on line 30
- **AND** the finding cites only line 12
- **WHEN** the response is validated
- **THEN** it is rejected with reason EVIDENCE_NOT_FOUND

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

### Requirement: Report every violation
The system SHALL report every violation it finds in a well-formed response, each with its reason code, path and a human-readable message, rather than stopping at the first. An incomplete or non-JSON response MUST be reported with that single reason.

#### Scenario: Two violations are both reported
- **GIVEN** a response whose first finding cites a line outside the log and whose second finding has a blank title
- **WHEN** the response is validated
- **THEN** it is rejected with LINE_OUT_OF_RANGE at `findings[0].line_refs[0]` and BLANK_FIELD at `findings[1].title`

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
