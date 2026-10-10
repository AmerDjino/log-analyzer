# Spec Delta

## ADDED Requirements

### Requirement: Mark redacted values
The evidence view SHALL display redaction markers visually distinct from log text and name the kind of value that was removed.

#### Scenario: Redaction marker is labeled
- **GIVEN** a cited line containing "[REDACTED:password]"
- **WHEN** a person views the finding's evidence
- **THEN** the marker is styled apart from the log text
- **AND** it is labeled as a removed password

### Requirement: Explain rate limits
When the API refuses a request with code RATE_LIMITED, the interface SHALL say how long to wait, taken from the Retry-After header, and MUST keep the person's input.

#### Scenario: Rate-limited submission keeps the pasted log
- **GIVEN** a person has pasted a log
- **WHEN** the API refuses the submission with code RATE_LIMITED and a Retry-After of 30 seconds
- **THEN** the interface says to try again in 30 seconds
- **AND** the pasted text is still in the paste area
