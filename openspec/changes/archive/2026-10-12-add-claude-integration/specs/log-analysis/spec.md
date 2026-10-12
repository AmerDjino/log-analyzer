# Spec Delta

## ADDED Requirements

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
