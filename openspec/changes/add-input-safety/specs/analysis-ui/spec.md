# Spec Delta

## ADDED Requirements

### Requirement: Mark redacted values
The evidence view SHALL display redaction markers visually distinct from log text and name the kind of value that was removed.

#### Scenario: Redaction marker is labeled
- **GIVEN** a cited line containing "[REDACTED:password]"
- **WHEN** a person views the finding's evidence
- **THEN** the marker is styled apart from the log text
- **AND** it is labeled as a removed password
