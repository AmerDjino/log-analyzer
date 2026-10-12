# Spec Delta

## ADDED Requirements

### Requirement: Report the submission size limit
The system SHALL tell a signed-in person the size limit it applies to submissions, in bytes, so that a client can refuse a larger file before sending it. The reported limit MUST be the one the API enforces.

#### Scenario: Configured size limit is reported
- **GIVEN** the size limit is configured as 1 MB
- **WHEN** a signed-in person asks for the submission size limit
- **THEN** the response gives a size limit of 1,048,576 bytes
