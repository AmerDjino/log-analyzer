# Spec Delta

## ADDED Requirements

### Requirement: Reuse results for identical submissions
When a submission's content matches a completed run of the same person, analyzed with the prompt version and model now configured, the system SHALL answer with that run instead of creating a new run or calling the model. Another person's runs MUST NOT be reused.

#### Scenario: Identical resubmission reuses the run
- **GIVEN** a completed run of "ana@example.com" for a log analyzed with prompt "log-analysis/v1"
- **WHEN** Ana submits the same content again while "log-analysis/v1" is configured
- **THEN** the API responds 200 with the existing run's id
- **AND** no run is created and the model is not called

#### Scenario: New prompt version analyzes again
- **GIVEN** a completed run for a log analyzed with prompt "log-analysis/v1"
- **WHEN** the same content is submitted while "log-analysis/v2" is configured
- **THEN** a new run is created and the model is called

#### Scenario: Another person's identical log is analyzed again
- **GIVEN** a completed run of "ana@example.com" for a log
- **WHEN** "bob@example.com" submits the same content
- **THEN** the API responds 202 with a new run's id
- **AND** the model is called for it
