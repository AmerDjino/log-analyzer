# Spec Delta

## Purpose

Defines how each submission is tracked as a run, how runs and their model attempts are recorded, and how the person who submitted a run reads it back.

## ADDED Requirements

### Requirement: Track each submission as a run
Each accepted submission SHALL create a run that belongs to the signed-in person who submitted it and moves through the statuses queued and running to either completed or failed. A failed run MUST carry exactly one of these failure reasons: VALIDATION_FAILED, MODEL_UNAVAILABLE, MODEL_CONFIGURATION_ERROR, INTERRUPTED or INTERNAL_ERROR.

#### Scenario: Run completes
- **GIVEN** a valid submission and a model that returns a valid analysis
- **WHEN** processing finishes
- **THEN** the run's status is completed

#### Scenario: Failed run carries its reason
- **GIVEN** a valid submission whose processing fails
- **WHEN** the person who submitted it reads the run
- **THEN** its status is failed
- **AND** it includes a failure reason such as VALIDATION_FAILED

#### Scenario: Model failure fails the run with its reason
- **GIVEN** a valid submission and a model service that cannot be reached
- **WHEN** processing finishes
- **THEN** the run's status is failed
- **AND** its failure reason is MODEL_UNAVAILABLE

### Requirement: Recover runs after a restart
A run that is still running when the API stops SHALL fail with reason INTERRUPTED once the API starts again, and runs that are still queued SHALL be processed after the restart.

#### Scenario: Interrupted run fails after a restart
- **GIVEN** a run that was running when the API stopped
- **WHEN** the API starts again
- **THEN** the run's status is failed with reason INTERRUPTED

#### Scenario: Queued run is processed after a restart
- **GIVEN** a run that was still queued when the API stopped
- **WHEN** the API starts again
- **THEN** the run is processed and its status becomes completed

### Requirement: Read a run
The system SHALL return a run by id to the person who submitted it, with its status, file name, submission time and statistics, plus its summary and validated findings once it has completed. An unknown id, and the id of another person's run, MUST both return status 404.

#### Scenario: Completed run is returned with its findings
- **GIVEN** a completed run with two findings
- **WHEN** the person who submitted it requests the run by id
- **THEN** the response includes status completed, the statistics, the summary and both findings

#### Scenario: Unknown run id
- **GIVEN** no run with id "7f3c" exists
- **WHEN** a signed-in person requests run "7f3c"
- **THEN** the API responds 404

#### Scenario: Another person's run is not found
- **GIVEN** a run submitted by "ana@example.com"
- **WHEN** "bob@example.com" requests it by id
- **THEN** the API responds 404, the same answer as for an unknown id

### Requirement: List recent runs
The system SHALL list the signed-in person's own runs newest first, 20 per page by default and at most 100, with each run's id, file name, submission time, status and highest finding severity. A page cursor that the API cannot read MUST be rejected with status 400 and code INVALID_CURSOR.

#### Scenario: Runs are listed newest first
- **GIVEN** a person submitted runs at 09:00, 10:00 and 11:00
- **WHEN** they list their runs
- **THEN** they are returned in the order 11:00, 10:00, 09:00

#### Scenario: Runs are paged
- **GIVEN** a person has 25 runs
- **WHEN** they list the first page
- **THEN** 20 runs are returned with a way to request the next page

#### Scenario: Other people's runs are not listed
- **GIVEN** "ana@example.com" has 2 runs and "bob@example.com" has 1
- **WHEN** Ana lists her runs
- **THEN** only her 2 runs are returned

#### Scenario: Invalid page cursor is rejected
- **GIVEN** a page cursor that the API cannot read, such as "not-a-cursor"
- **WHEN** a person lists their runs with it
- **THEN** the API responds 400 with code INVALID_CURSOR

### Requirement: Record every model attempt
Every model response the validator evaluates SHALL be stored as an attempt of its run, in order, with its outcome (accepted or rejected), its rejection reasons (each a reason code and the path of the failing field), the prompt version and model used, and its start and end times. The response text MUST NOT be stored.

#### Scenario: Rejected then accepted attempts are both recorded
- **GIVEN** a run whose first model response is rejected for LINE_OUT_OF_RANGE and whose second is accepted
- **WHEN** the run completes
- **THEN** it has two attempts in order: rejected with reason LINE_OUT_OF_RANGE, then accepted
