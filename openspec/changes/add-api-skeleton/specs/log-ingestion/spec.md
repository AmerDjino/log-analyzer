# Spec Delta

## Purpose

Defines how logs enter the system: which submissions are accepted, how their lines are numbered, and which facts about a log are computed by code rather than by the model.

## ADDED Requirements

### Requirement: Accept text log submissions
The system SHALL accept a log as pasted text or as an uploaded file containing UTF-8 text, and SHALL number its lines from 1 in the order received. Content that is not valid UTF-8 or contains NUL bytes MUST be rejected with status 400 and code UNSUPPORTED_CONTENT.

#### Scenario: Pasted log is accepted
- **GIVEN** a client pastes a 3-line log
- **WHEN** the log is submitted
- **THEN** the API responds 202 with the new run's id
- **AND** the run records 3 lines

#### Scenario: Uploaded log file is accepted
- **GIVEN** a client uploads "orders-api.log" containing UTF-8 text
- **WHEN** the file is submitted
- **THEN** the API responds 202 with the new run's id
- **AND** the run records the file name "orders-api.log"

#### Scenario: Binary file is rejected
- **GIVEN** a client uploads a file containing NUL bytes
- **WHEN** the file is submitted
- **THEN** the API responds 400 with code UNSUPPORTED_CONTENT
- **AND** no run is created

### Requirement: Reject empty input
The system SHALL reject a submission with no non-whitespace content with status 400 and code EMPTY_INPUT, without creating a run or calling the model.

#### Scenario: Whitespace-only paste is rejected
- **GIVEN** a client pastes only spaces, tabs and newlines
- **WHEN** the log is submitted
- **THEN** the API responds 400 with code EMPTY_INPUT
- **AND** no run is created and the model is never called

#### Scenario: Empty file is rejected
- **GIVEN** a client uploads a zero-byte file
- **WHEN** the file is submitted
- **THEN** the API responds 400 with code EMPTY_INPUT

### Requirement: Enforce a maximum submission size
The system SHALL reject a submission larger than the configured size limit (10 MB by default) with status 413 and code INPUT_TOO_LARGE, without creating a run or calling the model.

#### Scenario: Oversized upload is rejected
- **GIVEN** the size limit is configured as 1 MB
- **WHEN** a client uploads a 1.5 MB log
- **THEN** the API responds 413 with code INPUT_TOO_LARGE
- **AND** no run is created and the model is never called

#### Scenario: Upload at the limit is accepted
- **GIVEN** the size limit is configured as 1 MB
- **WHEN** a client uploads a log of exactly 1 MB
- **THEN** the API responds 202

### Requirement: Compute log statistics in code
For every run the system SHALL compute the line count, the number of lines at each recognized log level, and the first and last timestamps it can parse. These statistics MUST NOT be taken from model output.

#### Scenario: Level counts come from the log
- **GIVEN** a 10-line log with 3 lines at error level and 2 at warning level
- **WHEN** the run completes
- **THEN** its statistics report 10 lines, 3 errors and 2 warnings, whatever the model returned

#### Scenario: Log without recognizable levels or timestamps
- **GIVEN** a log with no level markers and no timestamps
- **WHEN** the run completes
- **THEN** its statistics report the line count, zero for every level and no time range
