# Spec Delta

## ADDED Requirements

### Requirement: Show progress while a run is in flight
While a run is running, the run page SHALL show how many parts are complete out of the total and update it without a page reload.

#### Scenario: Progress updates live
- **GIVEN** a person is on the page of a running run with 4 chunks
- **WHEN** the second chunk completes
- **THEN** the page shows "2 of 4" without reloading

### Requirement: Show occurrence counts
A finding that cites a collapsed line SHALL show how many times that line occurred in the log.

#### Scenario: Occurrence count is shown
- **GIVEN** a finding that cites a line collapsed from 1,214 occurrences
- **WHEN** a person views the run
- **THEN** the finding shows 1,214 occurrences
