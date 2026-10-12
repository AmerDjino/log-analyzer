# log-analysis Specification

## Purpose
Defines what an analysis of a submitted log contains and the rules every finding follows, independent of how the analysis is produced.

## Requirements

### Requirement: Analysis structure
Every analysis SHALL consist of a summary of what the log shows and a list of zero or more findings.

#### Scenario: Analysis with findings is accepted
- **GIVEN** a model response with a summary and two well-formed findings
- **WHEN** the response is validated
- **THEN** it is accepted with both findings

#### Scenario: Analysis without findings is accepted
- **GIVEN** a model response with a summary and an empty findings list
- **WHEN** the response is validated
- **THEN** it is accepted as an analysis with zero findings

#### Scenario: Analysis without a summary is rejected
- **GIVEN** a model response with findings but no summary
- **WHEN** the response is validated
- **THEN** it is rejected with reason SCHEMA_VIOLATION at path `summary`

### Requirement: Finding content
Each finding SHALL contain a title, a severity, a category, one or more cited line numbers, an evidence excerpt, an explanation of why it matters and a suggested next action. Cited line numbers MUST refer to 1-based positions in the log as submitted.

#### Scenario: Complete finding is accepted
- **GIVEN** a 40-line log whose line 12 reads "fail: Orders.Api[0] Database connection pool exhausted"
- **AND** a finding with all seven fields that cites line 12 with evidence "Database connection pool exhausted"
- **WHEN** the response is validated
- **THEN** the finding is accepted unchanged

#### Scenario: Line numbers start at 1
- **GIVEN** a log whose first line reads "Unhandled exception. System.NullReferenceException"
- **AND** a finding that cites line 1 with evidence "System.NullReferenceException"
- **WHEN** the response is validated
- **THEN** the citation is accepted

#### Scenario: Finding with a missing field is rejected
- **GIVEN** a finding without a suggested action
- **WHEN** the response is validated
- **THEN** it is rejected with reason SCHEMA_VIOLATION at path `findings[0].suggested_action`

#### Scenario: Finding without cited lines is rejected
- **GIVEN** a finding whose list of cited line numbers is empty
- **WHEN** the response is validated
- **THEN** it is rejected with reason SCHEMA_VIOLATION at path `findings[0].line_refs`

### Requirement: Severity levels
Each finding SHALL have exactly one severity from this closed set: critical (an outage, crash or data loss), error (failed operations without a full outage), warning (degradation or a risk of failure) and info (noteworthy but harmless).

#### Scenario: Each defined severity is accepted
- **GIVEN** four findings with the severities critical, error, warning and info
- **WHEN** the response is validated
- **THEN** all four findings are accepted

#### Scenario: Unknown severity is rejected
- **GIVEN** a finding with severity "fatal"
- **WHEN** the response is validated
- **THEN** it is rejected with reason SCHEMA_VIOLATION at path `findings[0].severity`

### Requirement: Finding categories
Each finding SHALL have exactly one category from this closed set: exception (unhandled or logged exceptions), timeout (operations exceeding a time limit), dependency (failing calls to databases, HTTP services or queues), resource (exhausted memory, connections, threads or disk), security (authentication, authorization or suspicious access), performance (slow operations that still succeed), configuration (missing or invalid settings) and other.

#### Scenario: Each defined category is accepted
- **GIVEN** eight findings, one for each defined category
- **WHEN** the response is validated
- **THEN** all eight findings are accepted

#### Scenario: Unknown category is rejected
- **GIVEN** a finding with category "network"
- **WHEN** the response is validated
- **THEN** it is rejected with reason SCHEMA_VIOLATION at path `findings[0].category`

### Requirement: Report the incidents a log contains
An analysis SHALL report every distinct problem the log shows, citing lines where it appears.

#### Scenario: [eval] Planted exception burst is reported
- **GIVEN** a log with 40 NullReferenceException entries between lines 200 and 260
- **WHEN** the log is analyzed
- **THEN** at least one finding with category exception and severity error or critical cites lines in that range

#### Scenario: [eval] Connection pool exhaustion is reported
- **GIVEN** a log in which database connection pool timeouts begin at line 4,120 and requests then fail with status 503
- **WHEN** the log is analyzed
- **THEN** a finding with category resource or dependency and severity critical cites lines from 4,120 onward

### Requirement: Classify severity by impact
An analysis SHALL give each finding the severity that matches its impact as defined in the requirement Severity levels, judged only from what the log shows.

#### Scenario: [eval] Process crash is critical
- **GIVEN** a log ending with an unhandled exception followed by an application shutdown message
- **WHEN** the log is analyzed
- **THEN** the finding for the crash has severity critical

#### Scenario: [eval] Slow queries without failures are a warning
- **GIVEN** a log with repeated slow-query warnings and no failed requests
- **WHEN** the log is analyzed
- **THEN** the finding for the slow queries has severity warning

### Requirement: Report repeated symptoms once
An analysis SHALL report a problem that repeats many times as one finding citing representative lines, not as one finding per occurrence.

#### Scenario: [eval] Repeated timeout becomes one finding
- **GIVEN** a log with 200 identical timeout errors for calls to the same service
- **WHEN** the log is analyzed
- **THEN** exactly one finding describes that timeout

### Requirement: Do not report routine activity
An analysis SHALL NOT report findings for routine activity such as startup messages, health checks and successful requests.

#### Scenario: [eval] Clean log has no findings
- **GIVEN** a log containing only startup messages, health checks and successful requests
- **WHEN** the log is analyzed
- **THEN** the findings list is empty
- **AND** the summary says that no problems were found
