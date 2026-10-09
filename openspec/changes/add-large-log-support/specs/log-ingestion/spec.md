# Spec Delta

## ADDED Requirements

### Requirement: Collapse repeated lines
Lines that differ only in their timestamp SHALL be sent to the model once, together with the number of occurrences and the original line numbers of every occurrence.

#### Scenario: Repeated timeout is sent once
- **GIVEN** a log in which the same pool timeout appears on 1,214 lines with different timestamps
- **WHEN** the log is prepared for the model
- **THEN** the model input contains that line once, with an occurrence count of 1,214

#### Scenario: Lines with different messages are kept apart
- **GIVEN** two lines with the same timestamp but different messages
- **WHEN** the log is prepared for the model
- **THEN** both lines are sent

### Requirement: Keep original line numbers
Every line reference the system stores or returns for a run SHALL use the 1-based line numbers of the log as submitted, regardless of collapsing or splitting.

#### Scenario: Finding from a later chunk cites original lines
- **GIVEN** an 80,000-line log analyzed in 4 chunks
- **WHEN** a finding comes from the third chunk
- **THEN** its cited lines are original line numbers such as 41,200, not positions within the chunk

#### Scenario: Collapsed line maps back to every occurrence
- **GIVEN** a finding that cites a line collapsed from 1,214 occurrences
- **WHEN** a client reads the run
- **THEN** the finding reports 1,214 occurrences with the first and last original line numbers
