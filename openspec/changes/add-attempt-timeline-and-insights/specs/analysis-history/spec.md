# Spec Delta

## ADDED Requirements

### Requirement: Expose a run's attempts
The system SHALL return a run's attempts in order, each with its outcome, rejection reasons, prompt version, model, transport retries, token counts and latency. Raw model output MUST NOT be exposed.

#### Scenario: Attempts are returned in order
- **GIVEN** a run with a rejected attempt followed by an accepted one
- **WHEN** a client requests the run's attempts
- **THEN** both attempts are returned in order with their outcome, reasons and usage

#### Scenario: Raw output stays private
- **GIVEN** a run with a rejected attempt
- **WHEN** a client requests the run's attempts
- **THEN** the response contains no raw model output

### Requirement: Aggregate reliability and cost
For a chosen period, the system SHALL report per prompt version and model the number of runs and attempts, the rejection rate overall and per reason, the average tokens per run, and the median and 95th-percentile latency.

#### Scenario: Rejection rate is calculated
- **GIVEN** 10 attempts with prompt "log-analysis/v1" in the period, 2 of them rejected for EVIDENCE_NOT_FOUND
- **WHEN** a client requests metrics for the period
- **THEN** "log-analysis/v1" shows a rejection rate of 20%, all of it for EVIDENCE_NOT_FOUND

#### Scenario: Empty period
- **GIVEN** no runs in the chosen period
- **WHEN** a client requests metrics for it
- **THEN** the response is an empty result rather than an error
