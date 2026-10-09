# Spec Delta

## ADDED Requirements

### Requirement: Reuse results for identical submissions
When a submission's content matches a completed run analyzed with the same prompt version and model, the system SHALL respond with that run instead of calling the model again.

#### Scenario: Identical resubmission reuses the run
- **GIVEN** a completed run for a log analyzed with prompt "log-analysis/v1"
- **WHEN** the same content is submitted again while "log-analysis/v1" is configured
- **THEN** the API responds 200 with the existing run's id
- **AND** the model is not called

#### Scenario: New prompt version analyzes again
- **GIVEN** a completed run for a log analyzed with prompt "log-analysis/v1"
- **WHEN** the same content is submitted while "log-analysis/v2" is configured
- **THEN** a new run is created and the model is called
