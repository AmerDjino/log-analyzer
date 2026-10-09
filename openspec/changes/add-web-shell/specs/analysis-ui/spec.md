# Spec Delta

## Purpose

Defines what a person can do in the web interface: submit a log, follow its analysis, read findings next to the log lines they cite, and browse past runs.

## ADDED Requirements

### Requirement: Submit a log
The interface SHALL let a person paste a log or choose a file and submit it for analysis. Submitting MUST be disabled while the input is empty, and a file over the size limit MUST be refused before anything is uploaded.

#### Scenario: Submit is disabled for empty input
- **GIVEN** the paste area is empty and no file is chosen
- **WHEN** the submit page is shown
- **THEN** the submit button is disabled

#### Scenario: Oversized file is refused before upload
- **GIVEN** the size limit is 10 MB
- **WHEN** a person chooses a 12 MB file
- **THEN** a message explains the limit
- **AND** no request is sent to the API

#### Scenario: Successful submission opens the run
- **GIVEN** a person has pasted a valid log
- **WHEN** they submit it
- **THEN** the interface navigates to the new run's page

### Requirement: Follow a run until it finishes
The run page SHALL show the run's current status and update it without a page reload until the run completes or fails. A failed run MUST show a plain-language explanation of its failure reason.

#### Scenario: Results appear when the run completes
- **GIVEN** a person is on the page of a queued run
- **WHEN** the run completes
- **THEN** the summary and findings appear without reloading the page

#### Scenario: Failure reason is explained
- **GIVEN** a run that failed with reason VALIDATION_FAILED
- **WHEN** a person opens its page
- **THEN** the page explains in plain language that the model's answers did not pass validation

### Requirement: Show findings by severity
The run page SHALL show the summary followed by the findings ordered from critical to info, with a filter by severity. A completed run with no findings MUST say that no problems were found.

#### Scenario: Findings are ordered by severity
- **GIVEN** a completed run with one warning, one critical and one error finding
- **WHEN** a person opens its page
- **THEN** the findings appear in the order critical, error, warning

#### Scenario: Filtering by severity
- **GIVEN** a completed run with critical and warning findings
- **WHEN** a person filters to critical
- **THEN** only the critical findings are shown

#### Scenario: Clean log message
- **GIVEN** a completed run with no findings
- **WHEN** a person opens its page
- **THEN** the page says that no problems were found

### Requirement: Show the evidence for a finding
Selecting a finding SHALL show its cited lines with their line numbers and three lines of context on each side, with the evidence excerpt highlighted.

#### Scenario: Evidence is highlighted in context
- **GIVEN** a finding that cites line 120 with evidence "connection pool exhausted"
- **WHEN** a person selects the finding
- **THEN** lines 117 to 123 are shown with their line numbers
- **AND** "connection pool exhausted" is highlighted on line 120

### Requirement: Browse past runs
The interface SHALL list past runs newest first with their file name, submission time, status and highest severity, and selecting one SHALL open its run page.

#### Scenario: Past run opens from the list
- **GIVEN** a completed run submitted earlier
- **WHEN** a person selects it in the run list
- **THEN** its run page opens with its findings

### Requirement: Explain request errors
When the API rejects a request, the interface SHALL show a plain-language message for the error code and MUST keep the person's input so they can correct it.

#### Scenario: Server-side size rejection keeps the input
- **GIVEN** a person has pasted a log
- **WHEN** the API responds 413 with code INPUT_TOO_LARGE
- **THEN** the interface explains the size limit
- **AND** the pasted text is still in the paste area
