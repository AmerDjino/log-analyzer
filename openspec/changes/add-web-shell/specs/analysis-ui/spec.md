# Spec Delta

## Purpose

Defines what a person can do in the web interface: create an account and sign in, submit a log, follow its analysis, read findings next to the log lines they cite, and browse their past runs.

## ADDED Requirements

### Requirement: Create an account
The interface SHALL let a person create an account with an email address and a password, then sign them in and open the submit page. A rejected email address or password MUST be explained next to the field it concerns, keeping the email address the person entered.

#### Scenario: New account is signed in
- **GIVEN** a person enters a new email address and a password of at least 12 characters
- **WHEN** they create the account
- **THEN** they are signed in and the submit page opens

#### Scenario: Short password is explained at its field
- **GIVEN** a person enters a new email address and an 11-character password
- **WHEN** they create the account
- **THEN** a message at the password field says that a password needs at least 12 characters
- **AND** the email address is still filled in

#### Scenario: Taken email address is explained at its field
- **GIVEN** an account exists for "ana@example.com"
- **WHEN** a person tries to create an account for "ana@example.com"
- **THEN** a message at the email field says that an account with this email address already exists

### Requirement: Sign in and out
The interface SHALL let a person sign in with their email address and password, and sign out from any page. Every page except sign-in and account creation SHALL need a session: a person without one who opens such a page is sent to sign-in and, once signed in, to the page they opened.

#### Scenario: Signed-out person is sent to sign-in and back
- **GIVEN** a person without a session
- **WHEN** they open the page of one of their runs
- **THEN** the sign-in page is shown
- **AND** after they sign in, the run's page opens

#### Scenario: Failed sign-in keeps the email address
- **GIVEN** a person enters their email address and a wrong password
- **WHEN** they sign in
- **THEN** a message says that the email address or password is incorrect
- **AND** the email address is still filled in

#### Scenario: Locked account is explained
- **GIVEN** an account that is locked after repeated failed sign-ins
- **WHEN** a person signs in to it
- **THEN** a message says that sign-in to this account is blocked for up to 15 minutes after too many failed attempts

#### Scenario: Sign-out returns to sign-in
- **GIVEN** a signed-in person
- **WHEN** they sign out
- **THEN** the sign-in page is shown
- **AND** opening the run list afterwards shows the sign-in page

### Requirement: Keep input when a session ends
When the API reports that the person's session has ended, the interface SHALL ask them to sign in again and MUST keep the input they have not sent yet.

#### Scenario: Pasted log survives an ended session
- **GIVEN** a person has pasted a log and their session has ended
- **WHEN** they submit it
- **THEN** they are asked to sign in
- **AND** after they sign in, the pasted log is still in the paste area

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
The interface SHALL list the person's own past runs newest first with their file name, submission time, status and highest severity, and selecting one SHALL open its run page.

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
