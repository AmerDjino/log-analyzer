# Spec Delta

## Purpose

Defines how the system calls the model: how the credentials are protected, how calls are bounded and retried, which prompt version is used, and what is recorded about every call.

## ADDED Requirements

### Requirement: Keep the API key on the server
The system SHALL read the model API key only from server-side configuration and MUST refuse to start when it is missing, naming the missing setting. The key value MUST NOT appear in any API response, log entry or stored record.

#### Scenario: Missing key stops startup
- **GIVEN** no API key is configured
- **WHEN** the API starts
- **THEN** startup fails with a message naming the missing setting

#### Scenario: Key never leaks
- **GIVEN** a configured API key and a completed run
- **WHEN** every API response, log entry and stored record from that run is searched for the key value
- **THEN** no match is found

### Requirement: Bound every model call
The system SHALL abandon a model call that has not finished within the configured timeout (60 seconds by default) and MUST treat it as a transient failure.

#### Scenario: Slow call is abandoned
- **GIVEN** a timeout of 5 seconds
- **AND** a model service that does not answer within 5 seconds
- **WHEN** a run calls the model
- **THEN** the call is abandoned after 5 seconds and handled as a transient failure

### Requirement: Retry transient failures only
The system SHALL retry rate limiting, overload, server errors, timeouts and connection failures up to the configured limit (2 retries by default) with increasing delays, honoring any retry-after hint. Other client errors MUST NOT be retried.

#### Scenario: Rate limit then success
- **GIVEN** the model service answers the first call with a rate-limit error and the second with a valid analysis
- **WHEN** the run is processed
- **THEN** the run completes
- **AND** its attempt records one transport retry

#### Scenario: Retries exhausted
- **GIVEN** the model service answers every call with an overload error
- **WHEN** the run is processed
- **THEN** the model is called 3 times
- **AND** the run fails with reason MODEL_UNAVAILABLE

#### Scenario: Authentication error is not retried
- **GIVEN** the model service rejects the API key
- **WHEN** the run is processed
- **THEN** the model is called exactly once
- **AND** the run fails with reason MODEL_CONFIGURATION_ERROR

### Requirement: Use versioned prompt templates
Every model request SHALL be built from a named, versioned prompt template selected by configuration, and every attempt MUST record the template version it used.

#### Scenario: Configured version is used and recorded
- **GIVEN** the prompt version is configured as "log-analysis/v2"
- **WHEN** a run is processed
- **THEN** its attempts record the prompt version "log-analysis/v2"

#### Scenario: Past runs keep their version
- **GIVEN** a run completed with "log-analysis/v1"
- **WHEN** the configuration changes to "log-analysis/v2"
- **THEN** the earlier run still reports "log-analysis/v1"

### Requirement: Record usage for every attempt
Every attempt SHALL record its input and output token counts, latency, model identifier, number of transport retries and the request id returned by the model service.

#### Scenario: Usage is recorded
- **GIVEN** a model response that reports 4,210 input tokens and 610 output tokens
- **WHEN** the attempt is stored
- **THEN** it records those token counts, its latency, the model identifier, its transport retries and the request id
