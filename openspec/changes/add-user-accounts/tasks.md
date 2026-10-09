# Tasks

## 1. Persistence and migrations

- [x] 1.1 Write the failing integration tests `DatabaseMigrationTests.Startup_AppliesInitialMigration` (on a fresh PostgreSQL container, plain SQL finds the initial migration in `__EFMigrationsHistory` and the `users`, `user_sessions` and `data_protection_keys` tables) and `DataProtectionKeyTests.Protect_StoresKeyInDatabase`; verify both fail because no migration exists
- [x] 1.2 Add the EF Core, Npgsql EF Core, Identity EF Core stores, data-protection EF Core, EFCore.NamingConventions and EF Core design packages to `Directory.Packages.props`, and pin `dotnet-ef` in a local tool manifest (`.config/dotnet-tools.json`); verify `dotnet tool restore` and `dotnet build` succeed
- [x] 1.3 Add `AppUser` (`IdentityUser<Guid>`), `UserSession` and `LogAnalyzerDbContext` (user-only Identity context, data-protection keys, snake_case names, tables `users`, `user_claims`, `user_logins`, `user_tokens`, `user_sessions` and `data_protection_keys`) on the existing `NpgsqlDataSource`, plus a design-time factory, and generate the `InitialAccounts` migration with `dotnet ef migrations add`; verify the migration creates exactly those tables and `CoreDependencyTests` still passes
- [x] 1.4 Add the hosted service that applies pending migrations at startup (first attempt during startup, then every 5 seconds until it succeeds), and make `/health` healthy only when PostgreSQL answers and migrations are applied; verify `DatabaseMigrationTests`, `HealthEndpointTests` and `HealthEndpointWithoutDatabaseTests` pass
- [x] 1.5 Persist data-protection keys in `data_protection_keys` under the application name `LogAnalyzer`; verify `DataProtectionKeyTests` passes
- [x] 1.6 Document in the README how migrations are applied, added (`dotnet ef migrations add <Name> --project src/LogAnalyzer.Infrastructure`) and rolled back, and add `add-user-accounts` to the planned changes before `add-api-skeleton`; verify the documented commands run as written

## 2. Registration and password storage

- [x] 2.1 Write the failing `RegistrationTests` and `PasswordStorageTests` for their 8 scenarios in test-plan.md, with the `AccountsClient`, `ProblemAssert` and `AccountsDatabase` helpers and an `https://localhost` base address in `PostgresApiFactory`; verify each fails because the registration endpoint does not exist
- [x] 2.2 Register Identity core with the EF Core stores and the options from design.md (12-character minimum, no composition rules, unique email, any user-name characters); verify the solution builds and the health tests still pass
- [x] 2.3 Add the error-code table with problem-details responses that carry a `code` member, and `POST /auth/register` with the email format and 254-character check, the 12 to 128 character password check, and EMAIL_TAKEN for duplicates, including a unique-index violation from concurrent registrations; verify all `RegistrationTests` and `PasswordStorageTests` pass
- [x] 2.4 Create `docs/accounts-api.md` with the registration request, response and error codes, and link it from the README; verify every code used in `RegistrationTests` appears in it

## 3. Sign-in, the current user and the default access rule

- [x] 3.1 Write the failing `SignInTests`, `CurrentUserTests` and `AccessPolicyTests` for their 8 scenarios in test-plan.md, with the cookie helpers, and add the `user-accounts/health-check-answers-without-a-session` trait to the existing `HealthEndpointTests` as a guard; verify the new tests fail because sign-in and `/auth/me` do not exist
- [x] 3.2 Add the session repository in Infrastructure and the ticket-store adapter in the API, and configure the `la_session` cookie (HttpOnly, always Secure, SameSite=Strict, no Expires, 8-hour lifetime, `TimeProvider` from DI) with events that answer 401 and 403 as problem details instead of redirecting; verify the solution builds and the health and registration tests still pass
- [x] 3.3 Add the authorization fallback policy that requires a signed-in user, with `AllowAnonymous()` on the health check, registration and the Development OpenAPI document; verify `HealthEndpointTests` and `RegistrationTests` still pass
- [x] 3.4 Add `POST /auth/sign-in` (JSON body only, lookup by email address, a dummy-hash check for unknown addresses, a password check that counts failures, INVALID_CREDENTIALS, and `AllowAnonymous()`) and `GET /auth/me`; verify all `SignInTests`, `CurrentUserTests` and `AccessPolicyTests` pass
- [x] 3.5 Document sign-in, the session cookie, `GET /auth/me` and SIGN_IN_REQUIRED in `docs/accounts-api.md`; verify every code used in this group's tests appears in it

## 4. Lockout

- [x] 4.1 Write the failing `LockoutTests` for their 3 scenarios in test-plan.md, including a check that the stored lockout end is 15 minutes after the fifth failure; verify `FifthFailedSignIn_LocksAccount` fails because a locked account still answers INVALID_CREDENTIALS
- [x] 4.2 Set the lockout options (5 attempts, 15 minutes, on for new accounts) and answer locked sign-ins with 403 and ACCOUNT_LOCKED; verify all `LockoutTests` pass
- [x] 4.3 Document the lockout rule and ACCOUNT_LOCKED in `docs/accounts-api.md`; verify the documented numbers match the configured options

## 5. Sign-out and idle sessions

- [x] 5.1 Write the failing `SignOutTests` and `IdleSessionTests` for their 4 scenarios in test-plan.md, with `TimedPostgresApiFactory` and `FakeTimeProvider`; verify the sign-out tests fail because the endpoint does not exist and `Session_StaysAliveWhileUsed` fails because renewal waits for half the lifetime
- [x] 5.2 Add `POST /auth/sign-out`, which deletes the stored session and expires the cookie; verify all `SignOutTests` pass
- [x] 5.3 Renew a session on use at most once a minute, and delete an expired session's row when it is presented; verify all `IdleSessionTests` pass
- [x] 5.4 Document sign-out and the 8-hour idle rule in `docs/accounts-api.md`; verify the documented times match the cookie options

## 6. Integration checks

- [x] 6.1 Start the stack on a fresh database (`docker compose down --volumes`, then `docker compose up --build --wait`) and, in Chrome, Edge or Firefox at http://localhost:8080, register, sign in, read `/api/auth/me` and sign out through nginx; verify each call answers as specified and `/api/health` stays Healthy
- [x] 6.2 Run the full test suite, `node scripts/check-scenario-coverage.mjs` and `openspec validate add-user-accounts --strict`; verify all pass and the coverage report shows the 24 scenarios of this change as already having tests

## Workflow follow-up

- Open a pull request and merge it once all four CI jobs pass.
- Run `/opsx:verify add-user-accounts`, then `/opsx:archive add-user-accounts`.
- Revise the plans listed under follow-ups in proposal.md with `/opsx:update`: runs per user in `add-api-skeleton`, sign-in screens in `add-web-shell`, and the per-user limit in `add-input-safety`.
