# user-accounts Specification

## Purpose
Defines how people create an account, sign in and out, how long a session lasts, and which parts of the API need a signed-in user.

## Requirements

### Requirement: Register an account
The system SHALL create an account from an email address and a password, and respond 201 with the account's id and email address. The email address MUST be a valid address of at most 254 characters, otherwise the API responds 400 with code INVALID_EMAIL. The email address is the account's sign-in name. Registration MUST NOT start a session.

#### Scenario: New account is created
- **GIVEN** no account exists for "ana@example.com"
- **WHEN** someone registers "ana@example.com" with the password "correct horse battery"
- **THEN** the API responds 201 with the new account's id and the email address "ana@example.com"
- **AND** the response contains no password and sets no session cookie

#### Scenario: Invalid email address is rejected
- **WHEN** someone registers "ana-at-example.com" with a valid password
- **THEN** the API responds 400 with code INVALID_EMAIL
- **AND** no account is created

### Requirement: Keep email addresses unique regardless of case
The system SHALL reject a registration whose email address matches an existing account when letter case is ignored, responding 409 with code EMAIL_TAKEN, and MUST NOT create a second account.

#### Scenario: Email address in different case is taken
- **GIVEN** an account for "ana@example.com"
- **WHEN** someone registers "Ana@Example.COM"
- **THEN** the API responds 409 with code EMAIL_TAKEN
- **AND** only one account exists for that address

### Requirement: Enforce password length
A password SHALL be 12 to 128 characters long. A shorter password MUST be rejected with status 400 and code PASSWORD_TOO_SHORT, and a longer one with status 400 and code PASSWORD_TOO_LONG. No rules about digits, letter case or symbols apply.

#### Scenario: Short password is rejected
- **WHEN** someone registers with an 11-character password
- **THEN** the API responds 400 with code PASSWORD_TOO_SHORT
- **AND** no account is created

#### Scenario: Overlong password is rejected
- **WHEN** someone registers with a 129-character password
- **THEN** the API responds 400 with code PASSWORD_TOO_LONG
- **AND** no account is created

#### Scenario: Twelve lowercase letters are accepted
- **WHEN** someone registers with the password "abcdefghijkl"
- **THEN** the API responds 201

### Requirement: Store passwords only as salted hashes
The system SHALL store a password only as a salted, deliberately slow hash, and MUST NOT store or return it in readable form. Two accounts with the same password MUST have different stored hashes.

#### Scenario: Stored account does not contain the password
- **GIVEN** "ana@example.com" registered with the password "correct horse battery"
- **WHEN** every stored value of that account is read
- **THEN** none of them contains "correct horse battery"

#### Scenario: Same password gives different stored hashes
- **GIVEN** two accounts registered with the same password
- **WHEN** their stored password hashes are compared
- **THEN** the hashes differ

### Requirement: Sign in with email and password
The system SHALL start a session when the email address, compared without regard to letter case, and the password match an account that is not locked. It responds 200 with the account's id and email address and sets a session cookie marked HttpOnly, Secure and SameSite=Strict. A wrong password and an unknown email address MUST get the same answer. Sign-in MUST accept only a JSON body.

#### Scenario: Correct credentials start a session
- **GIVEN** an account "ana@example.com" with the password "correct horse battery"
- **WHEN** someone signs in with "ANA@example.com" and "correct horse battery"
- **THEN** the API responds 200 with the account's id and email address
- **AND** sets a session cookie marked HttpOnly, Secure and SameSite=Strict

#### Scenario: Wrong password is rejected
- **GIVEN** an account "ana@example.com"
- **WHEN** someone signs in with "ana@example.com" and a wrong password
- **THEN** the API responds 401 with code INVALID_CREDENTIALS
- **AND** sets no session cookie

#### Scenario: Unknown email gets the same answer as a wrong password
- **GIVEN** no account exists for "bob@example.com"
- **WHEN** someone signs in with "bob@example.com" and any password
- **THEN** the API responds 401 with code INVALID_CREDENTIALS and the same title and detail as for a wrong password

#### Scenario: Form-encoded sign-in is rejected
- **GIVEN** an account "ana@example.com" with the password "correct horse battery"
- **WHEN** someone sends those credentials as an HTML form instead of JSON
- **THEN** the API responds 415
- **AND** sets no session cookie

### Requirement: Reject unreadable request bodies
Registration and sign-in SHALL answer a request whose JSON body is empty or malformed with status 400, without creating an account or starting a session.

#### Scenario: Malformed registration body is rejected
- **WHEN** someone registers with a JSON body that ends before its closing brace
- **THEN** the API responds 400
- **AND** no account is created

#### Scenario: Empty sign-in body is rejected
- **WHEN** someone signs in with an empty JSON body
- **THEN** the API responds 400
- **AND** sets no session cookie

### Requirement: Lock an account after repeated failed sign-ins
The fifth failed sign-in in a row for one account, and every sign-in to that account in the 15 minutes after it, SHALL be refused with status 403 and code ACCOUNT_LOCKED, even when the password is correct. A successful sign-in MUST reset the count of failed attempts.

#### Scenario: Fifth failed sign-in locks the account
- **GIVEN** an account with 4 failed sign-ins in a row
- **WHEN** a fifth sign-in with a wrong password is attempted
- **THEN** the API responds 403 with code ACCOUNT_LOCKED
- **AND** a sign-in with the correct password right after it is refused with code ACCOUNT_LOCKED

#### Scenario: Lock ends after 15 minutes
- **GIVEN** an account that was locked 15 minutes and 1 second ago
- **WHEN** someone signs in with the correct password
- **THEN** the API responds 200 and sets a session cookie

#### Scenario: Successful sign-in resets the failure count
- **GIVEN** an account with 4 failed sign-ins in a row followed by a successful one
- **WHEN** a sign-in with a wrong password is attempted
- **THEN** the API responds 401 with code INVALID_CREDENTIALS

### Requirement: Read the signed-in user
The system SHALL return the signed-in user's id and email address for a request with a valid session, and MUST refuse a request without a valid session with status 401 and code SIGN_IN_REQUIRED.

#### Scenario: Signed-in user is returned
- **GIVEN** "ana@example.com" is signed in
- **WHEN** the current user is requested with that session cookie
- **THEN** the API responds 200 with the account's id and the email address "ana@example.com"

#### Scenario: Request without a session is refused
- **WHEN** the current user is requested without a session cookie
- **THEN** the API responds 401 with code SIGN_IN_REQUIRED

#### Scenario: Tampered session cookie is refused
- **GIVEN** a session cookie with one character changed
- **WHEN** the current user is requested with it
- **THEN** the API responds 401 with code SIGN_IN_REQUIRED

### Requirement: Sign out
Signing out SHALL end the session on the server and expire the session cookie, responding 204. A copy of the cookie taken before sign-out MUST then be refused with status 401 and code SIGN_IN_REQUIRED.

#### Scenario: Sign-out clears the session cookie
- **GIVEN** a signed-in session
- **WHEN** the user signs out
- **THEN** the API responds 204 and the response expires the session cookie

#### Scenario: Copied cookie stops working after sign-out
- **GIVEN** a copy of a session cookie taken while signed in
- **AND** the user then signs out
- **WHEN** the current user is requested with the copied cookie
- **THEN** the API responds 401 with code SIGN_IN_REQUIRED

### Requirement: End idle sessions
A session SHALL end once 8 hours pass without a request that renews it. A request that uses the session MUST renew it whenever at least a minute has passed since its last renewal, so a session ends at most a minute before 8 hours without use. A request with an ended session MUST be refused with status 401 and code SIGN_IN_REQUIRED.

#### Scenario: Idle session ends after 8 hours
- **GIVEN** a session last used 8 hours and 1 minute ago
- **WHEN** the current user is requested with it
- **THEN** the API responds 401 with code SIGN_IN_REQUIRED

#### Scenario: Using a session keeps it alive
- **GIVEN** a session started at 09:00 and used at 12:00
- **WHEN** the current user is requested with it at 19:30 the same day
- **THEN** the API responds 200

#### Scenario: A minute of use renews the session
- **GIVEN** a session started at 09:00 and used at 09:01
- **WHEN** the current user is requested with it at 17:00:30 the same day
- **THEN** the API responds 200

### Requirement: Require a signed-in user by default
Every API endpoint SHALL refuse a request without a valid session with status 401 and code SIGN_IN_REQUIRED, unless it is public. The only public endpoints are the health check, registration, sign-in and, in Development, the OpenAPI document. A new endpoint MUST be protected without any extra setting.

#### Scenario: Health check answers without a session
- **GIVEN** the database answers
- **WHEN** the health check is requested without a session cookie
- **THEN** the API responds 200 with "Healthy"

#### Scenario: Non-public endpoints refuse anonymous requests
- **GIVEN** the list of every endpoint the API exposes
- **WHEN** each endpoint that is not public is called without a session cookie
- **THEN** each one responds 401 with code SIGN_IN_REQUIRED
