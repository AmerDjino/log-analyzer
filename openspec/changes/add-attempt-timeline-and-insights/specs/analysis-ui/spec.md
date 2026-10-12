# Spec Delta

## ADDED Requirements

### Requirement: Show the attempt timeline
The run page SHALL show every attempt in order with its outcome, its rejection reasons in plain language, its tokens and its latency.

#### Scenario: Rejected attempt explains why
- **GIVEN** a run whose first attempt was rejected for LINE_OUT_OF_RANGE and whose second was accepted
- **WHEN** a person opens the attempt timeline
- **THEN** the first attempt is marked rejected with an explanation that a cited line did not exist in the log
- **AND** the second attempt is marked accepted

### Requirement: Show reliability and cost insights
The insights page SHALL show the aggregated metrics of the person's own runs per prompt version for a chosen period and let a person compare two prompt versions side by side.

#### Scenario: Two prompt versions are compared
- **GIVEN** metrics exist for "log-analysis/v1" and "log-analysis/v2"
- **WHEN** a person selects both for comparison
- **THEN** their rejection rates, average tokens per run and latencies are shown side by side
