# Spec Delta

## ADDED Requirements

### Requirement: Serve a run's log lines
The system SHALL return any requested range of a run's stored log lines to the person who submitted the run, each with its 1-based line number. A range that extends past the end of the log MUST be clamped to the lines that exist. Another person's run MUST return status 404.

#### Scenario: Line range is returned with numbers
- **GIVEN** a run with a 500-line log
- **WHEN** the person who submitted it requests lines 117 to 123
- **THEN** the response contains those 7 lines, each with its line number

#### Scenario: Range past the end is clamped
- **GIVEN** a run with a 500-line log
- **WHEN** the person who submitted it requests lines 495 to 520
- **THEN** the response contains lines 495 to 500

#### Scenario: Another person's log lines are not found
- **GIVEN** a run submitted by "ana@example.com"
- **WHEN** "bob@example.com" requests its lines 1 to 10
- **THEN** the API responds 404
