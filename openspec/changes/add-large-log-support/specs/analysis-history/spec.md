# Spec Delta

## ADDED Requirements

### Requirement: Report run progress
While a run is running, reading it SHALL return how many of its chunks are complete out of the total.

#### Scenario: Progress is reported
- **GIVEN** a running run with 4 chunks, 2 of them complete
- **WHEN** a client reads the run
- **THEN** the response reports 2 of 4 chunks complete
