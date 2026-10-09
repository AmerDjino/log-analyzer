# Spec Delta

## ADDED Requirements

### Requirement: Stay within the request token budget
When a prepared log exceeds the configured per-request token budget, the system SHALL split it on line boundaries into chunks that each fit within the budget together with the prompt, and MUST NOT send a request that exceeds the budget.

#### Scenario: Large log is split
- **GIVEN** a token budget of 50,000 tokens
- **AND** a prepared log of about 180,000 tokens
- **WHEN** the run is processed
- **THEN** the log is analyzed in at least 4 requests
- **AND** no request exceeds 50,000 tokens

#### Scenario: Small log is sent whole
- **GIVEN** a prepared log well within the budget
- **WHEN** the run is processed
- **THEN** it is analyzed in a single request

### Requirement: Merge findings across chunks
Findings from different chunks that share a category and identical evidence SHALL be merged into one finding with combined line references, and every run MUST end with one summary covering the whole log.

#### Scenario: Same problem in two chunks becomes one finding
- **GIVEN** chunk 1 and chunk 3 each return a resource finding with evidence "connection pool exhausted"
- **WHEN** the chunk results are merged
- **THEN** the run has one such finding citing lines from both chunks

#### Scenario: One summary per run
- **GIVEN** a run analyzed in 4 chunks
- **WHEN** it completes
- **THEN** it has exactly one summary

### Requirement: Fail the run when a chunk fails
If any chunk still fails after its retries and correction, the run SHALL fail with that chunk's failure reason and MUST NOT report partial findings.

#### Scenario: One failed chunk fails the run
- **GIVEN** a run analyzed in 3 chunks whose second chunk fails with MODEL_UNAVAILABLE
- **WHEN** processing finishes
- **THEN** the run fails with reason MODEL_UNAVAILABLE
- **AND** it returns no findings
