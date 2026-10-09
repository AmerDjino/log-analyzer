# Spec Delta

## ADDED Requirements

### Requirement: Serve a run's log lines
The system SHALL return any requested range of a run's stored log lines, each with its 1-based line number. A range that extends past the end of the log MUST be clamped to the lines that exist.

#### Scenario: Line range is returned with numbers
- **GIVEN** a run with a 500-line log
- **WHEN** a client requests lines 117 to 123
- **THEN** the response contains those 7 lines, each with its line number

#### Scenario: Range past the end is clamped
- **GIVEN** a run with a 500-line log
- **WHEN** a client requests lines 495 to 520
- **THEN** the response contains lines 495 to 500
