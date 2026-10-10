# Spec Delta

## ADDED Requirements

### Requirement: Redact secrets before analysis
Before a log is stored or sent to the model, the system SHALL replace connection-string passwords, bearer tokens, API keys and email addresses with a marker naming the kind of value, without changing the number of lines.

#### Scenario: Connection string password is redacted
- **GIVEN** a line containing "Password=hunter2;"
- **WHEN** the log is submitted
- **THEN** the stored line contains "Password=[REDACTED:password];"
- **AND** no model request contains "hunter2"

#### Scenario: Bearer token is redacted
- **GIVEN** a line containing "Authorization: Bearer eyJhbGciOiJIUzI1NiJ9.e30.sig"
- **WHEN** the log is submitted
- **THEN** the token is replaced with "[REDACTED:token]" in storage and in every model request

#### Scenario: Line numbering is preserved
- **GIVEN** a 50-line log with secrets on lines 3 and 40
- **WHEN** the log is redacted
- **THEN** it still has 50 lines
- **AND** line 40 holds the same message with only the secret replaced

### Requirement: Limit submission rate
The system SHALL accept at most the configured number of submissions per signed-in person per minute (10 by default) and MUST answer that person's further submissions in that minute with status 429, code RATE_LIMITED and a Retry-After header.

#### Scenario: Eleventh submission is limited
- **GIVEN** the limit is 10 submissions per minute
- **WHEN** a person makes their 11th submission within one minute
- **THEN** the API responds 429 with code RATE_LIMITED and a Retry-After header
- **AND** no run is created

#### Scenario: Submission limit is counted per person
- **GIVEN** the limit is 10 submissions per minute and "ana@example.com" has made 10 submissions this minute
- **WHEN** "bob@example.com" submits a log
- **THEN** the API responds 202
