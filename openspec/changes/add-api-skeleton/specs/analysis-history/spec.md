# Spec Delta

## Purpose

Defines how each submission is tracked as a run, how runs and their model attempts are recorded, and how clients read them back.

## ADDED Requirements

### Requirement: Track each submission as a run
Each accepted submission SHALL create a run that moves through the statuses queued and running to either completed or failed. A failed run MUST carry a machine-readable failure reason.

#### Scenario: Run completes
- **GIVEN** a valid submission and a model that returns a valid analysis
- **WHEN** processing finishes
- **THEN** the run's status is completed

#### Scenario: Failed run carries its reason
- **GIVEN** a valid submission whose processing fails
- **WHEN** a client reads the run
- **THEN** its status is failed
- **AND** it includes a failure reason such as VALIDATION_FAILED

### Requirement: Read a run
The system SHALL return a run by id with its status, file name, submission time and statistics, plus its summary and validated findings once it has completed. An unknown id MUST return status 404.

#### Scenario: Completed run is returned with its findings
- **GIVEN** a completed run with two findings
- **WHEN** a client requests the run by id
- **THEN** the response includes status completed, the statistics, the summary and both findings

#### Scenario: Unknown run id
- **GIVEN** no run with id "7f3c" exists
- **WHEN** a client requests run "7f3c"
- **THEN** the API responds 404

### Requirement: List recent runs
The system SHALL list runs newest first, 20 per page by default, with each run's id, file name, submission time, status and highest finding severity.

#### Scenario: Runs are listed newest first
- **GIVEN** three runs submitted at 09:00, 10:00 and 11:00
- **WHEN** a client lists runs
- **THEN** they are returned in the order 11:00, 10:00, 09:00

#### Scenario: Runs are paged
- **GIVEN** 25 runs exist
- **WHEN** a client lists the first page
- **THEN** 20 runs are returned with a way to request the next page

### Requirement: Record every model attempt
Every model response the validator evaluates SHALL be stored as an attempt of its run, in order, with its outcome (accepted or rejected), any rejection reasons, the prompt version and model used, and its start and end times.

#### Scenario: Rejected then accepted attempts are both recorded
- **GIVEN** a run whose first model response is rejected for LINE_OUT_OF_RANGE and whose second is accepted
- **WHEN** the run completes
- **THEN** it has two attempts in order: rejected with reason LINE_OUT_OF_RANGE, then accepted
