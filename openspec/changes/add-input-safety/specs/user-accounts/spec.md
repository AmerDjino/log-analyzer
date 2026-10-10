# Spec Delta

## ADDED Requirements

### Requirement: Limit sign-in and registration attempts per client
The system SHALL accept at most the configured number of sign-in and registration requests per client address per minute (20 by default). It MUST answer further requests from that address in that minute with status 429, code RATE_LIMITED and a Retry-After header, without checking the password, counting toward an account's lockout or creating an account.

#### Scenario: Sign-in over the limit is refused
- **GIVEN** the limit is 20 attempts per minute
- **WHEN** one client sends its 21st sign-in request within one minute, with the correct password
- **THEN** the API responds 429 with code RATE_LIMITED and a Retry-After header
- **AND** sets no session cookie

#### Scenario: Registration over the limit creates no account
- **GIVEN** one client has used all its attempts this minute
- **WHEN** it registers a new email address
- **THEN** the API responds 429 with code RATE_LIMITED
- **AND** no account is created

#### Scenario: Refused sign-in does not count toward lockout
- **GIVEN** "ana@example.com" has 4 failed sign-ins in a row, and one client has used all its attempts this minute
- **WHEN** that client signs in to Ana's account with a wrong password
- **THEN** the API responds 429 with code RATE_LIMITED
- **AND** a sign-in with the correct password from another client right after it responds 200

#### Scenario: Attempt limit is counted per client
- **GIVEN** one client has used all its attempts this minute
- **WHEN** another client signs in with correct credentials
- **THEN** the API responds 200
