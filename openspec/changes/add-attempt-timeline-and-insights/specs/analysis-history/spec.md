# Spec Delta

## ADDED Requirements

### Requirement: Expose a run's attempts
The system SHALL return a run's attempts in order to the person who submitted the run, each with its outcome, rejection reasons, prompt version, model, transport retries, token counts and latency. Raw model output MUST NOT be exposed. An unknown run id and another person's run MUST both return status 404.

#### Scenario: Attempts are returned in order
- **GIVEN** a run with a rejected attempt followed by an accepted one
- **WHEN** the person who submitted it requests the run's attempts
- **THEN** both attempts are returned in order with their outcome, reasons and usage

#### Scenario: Raw output stays private
- **GIVEN** a run with a rejected attempt
- **WHEN** the person who submitted it requests the run's attempts
- **THEN** the response contains no raw model output

#### Scenario: Another person's attempts are not found
- **GIVEN** a run submitted by "ana@example.com"
- **WHEN** "bob@example.com" requests its attempts
- **THEN** the API responds 404

### Requirement: Aggregate reliability and cost
For a chosen period, the system SHALL report, over the signed-in person's own runs, per prompt version and model: the number of runs and attempts, the rejection rate overall and per reason, the average tokens per run, and the median and 95th-percentile latency.

#### Scenario: Rejection rate is calculated
- **GIVEN** a person's runs have 10 attempts with prompt "log-analysis/v1" in the period, 2 of them rejected for EVIDENCE_NOT_FOUND
- **WHEN** that person requests metrics for the period
- **THEN** "log-analysis/v1" shows a rejection rate of 20%, all of it for EVIDENCE_NOT_FOUND

#### Scenario: Empty period
- **GIVEN** a person has no runs in the chosen period
- **WHEN** they request metrics for it
- **THEN** the response is an empty result rather than an error

#### Scenario: Other people's runs are not counted
- **GIVEN** Ana's runs have 10 attempts with prompt "log-analysis/v1" in the period, 2 of them rejected
- **AND** Bob's runs have 10 attempts with prompt "log-analysis/v1" in the period, all of them rejected
- **WHEN** Ana requests metrics for the period
- **THEN** "log-analysis/v1" shows 10 attempts and a rejection rate of 20%
