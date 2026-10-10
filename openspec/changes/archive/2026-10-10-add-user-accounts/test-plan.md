# Test Plan

Every scenario in this change is deterministic. All tests are integration tests: the API is hosted in memory against a PostgreSQL container from Testcontainers, so they need Docker but no network, model or API key. Test classes live in `tests/LogAnalyzer.IntegrationTests/Accounts/`, except the health check test, which already exists and only gains its scenario tag.

| Scenario id | Test | Kind | Project |
|---|---|---|---|
| `user-accounts/new-account-is-created` | `RegistrationTests.Register_CreatesAccountWithoutSession` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/invalid-email-address-is-rejected` | `RegistrationTests.Register_RejectsInvalidEmail` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/email-address-in-different-case-is-taken` | `RegistrationTests.Register_RejectsEmailTakenInDifferentCase` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/short-password-is-rejected` | `RegistrationTests.Register_RejectsShortPassword` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/overlong-password-is-rejected` | `RegistrationTests.Register_RejectsOverlongPassword` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/twelve-lowercase-letters-are-accepted` | `RegistrationTests.Register_AcceptsTwelveLowercaseLetters` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/stored-account-does-not-contain-the-password` | `PasswordStorageTests.StoredAccount_DoesNotContainPassword` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/same-password-gives-different-stored-hashes` | `PasswordStorageTests.SamePassword_GivesDifferentHashes` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/correct-credentials-start-a-session` | `SignInTests.SignIn_StartsSessionWithSecureCookie` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/wrong-password-is-rejected` | `SignInTests.SignIn_RejectsWrongPassword` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/unknown-email-gets-the-same-answer-as-a-wrong-password` | `SignInTests.SignIn_AnswersUnknownEmailLikeWrongPassword` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/form-encoded-sign-in-is-rejected` | `SignInTests.SignIn_RejectsFormEncodedBody` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/malformed-registration-body-is-rejected` | `RegistrationTests.Register_RejectsMalformedBody` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/empty-sign-in-body-is-rejected` | `SignInTests.SignIn_RejectsEmptyBody` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/fifth-failed-sign-in-locks-the-account` | `LockoutTests.FifthFailedSignIn_LocksAccount` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/lock-ends-after-15-minutes` | `LockoutTests.Lock_EndsAfterFifteenMinutes` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/successful-sign-in-resets-the-failure-count` | `LockoutTests.SuccessfulSignIn_ResetsFailureCount` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/signed-in-user-is-returned` | `CurrentUserTests.Me_ReturnsSignedInUser` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/request-without-a-session-is-refused` | `CurrentUserTests.Me_RefusesRequestWithoutSession` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/tampered-session-cookie-is-refused` | `CurrentUserTests.Me_RefusesTamperedCookie` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/sign-out-clears-the-session-cookie` | `SignOutTests.SignOut_ExpiresSessionCookie` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/copied-cookie-stops-working-after-sign-out` | `SignOutTests.SignOut_InvalidatesCopiedCookie` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/idle-session-ends-after-8-hours` | `IdleSessionTests.Session_EndsAfterEightIdleHours` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/using-a-session-keeps-it-alive` | `IdleSessionTests.Session_StaysAliveWhileUsed` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/a-minute-of-use-renews-the-session` | `IdleSessionTests.Session_RenewsWhenUsedAfterOneMinute` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/health-check-answers-without-a-session` | `HealthEndpointTests.Health_ReturnsHealthy_WhenDatabaseAnswers` (existing, gains the tag) | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/non-public-endpoints-refuse-anonymous-requests` | `AccessPolicyTests.NonPublicEndpoints_RefuseAnonymousRequests` | integration | LogAnalyzer.IntegrationTests |

## Fixtures

- `PostgresApiFactory` (existing): creates clients with `https://localhost` as the base address, so `Secure` cookies are kept and sent back.
- `TimedPostgresApiFactory`: the same host with a `FakeTimeProvider` registered as the `TimeProvider`, for the idle-session tests. Needs the `Microsoft.Extensions.TimeProvider.Testing` package.
- `AccountsClient`: a test helper that registers, signs in, signs out and reads the current user, and creates a unique email address per test (`user-<guid>@example.com`), so tests sharing one database never collide.
- `ProblemAssert`: asserts a response's status code and its problem-details `code` member, and for the unknown-email scenario compares `title` and `detail` with a wrong-password response.
- `AccountsDatabase`: direct SQL access to the test database, to read every stored column of an account (password scenarios), its id and its password hash, to count its stored sessions, and to set its lockout end to 1 second ago (lockout-expiry scenario).
- Cookie helpers: read the session cookie and its attributes from the `Set-Cookie` header, replay a copied cookie value on a fresh client, and change one character of a cookie value.
- Public endpoint list for `AccessPolicyTests`, by HTTP method and route pattern: `GET /health`, `POST /auth/register`, `POST /auth/sign-in` and `GET /openapi/{documentName}.json`. Every other method of every endpoint from `EndpointDataSource` is called with placeholder route values. An endpoint without method metadata counts as `GET` only; the health check is one, and it answers every method. The test fails if `POST /auth/sign-out` is not among the calls, because only the default rule refuses it.

## Tests without a scenario

These pin limits and fixes that no scenario names. They carry no `Scenario` trait.

- `RegistrationTests`: email addresses of 254 and 255 characters, an address with a NUL escaped in a quoted local part, a 128-character password, passwords of 11 and 128 emoji (each one character), and a password of twelve spaces.
- `SignInTests.SignIn_AnswersEmailWithControlCharacterLikeUnknownEmail`: an address with a NUL gets the unknown-address answer.
- `PasswordStorageTests.StoredHash_UsesPbkdf2WithHmacSha512AndAtLeast100000Iterations`: the hash format and work factor from design.md.
- `OpenApiDocumentTests`: the OpenAPI document answers without a session, and lists every answer of each account endpoint.
- `DatabaseMigrationTests` and `DataProtectionKeyTests`: the first migration and the key storage.
- `DatabaseMigratorTests.Stop_AfterDispose_DoesNotThrow`: the migration service can be stopped after it was disposed, which test hosts do when they shut down.
