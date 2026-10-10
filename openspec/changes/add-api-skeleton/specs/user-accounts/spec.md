# Spec Delta

## MODIFIED Requirements

### Requirement: Reject unreadable request bodies
Registration and sign-in SHALL answer a request whose JSON body is empty or malformed with status 400, and a request whose body declares a character set the API does not support with status 415, without creating an account or starting a session.

#### Scenario: Malformed registration body is rejected
- **WHEN** someone registers with a JSON body that ends before its closing brace
- **THEN** the API responds 400
- **AND** no account is created

#### Scenario: Empty sign-in body is rejected
- **WHEN** someone signs in with an empty JSON body
- **THEN** the API responds 400
- **AND** sets no session cookie

#### Scenario: Sign-in body in an unsupported character set is rejected
- **WHEN** someone signs in with a JSON body that declares the character set "bogus"
- **THEN** the API responds 415
- **AND** sets no session cookie
