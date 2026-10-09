# Design: Add user accounts

## Context

See proposal.md for motivation and `specs/user-accounts/spec.md` for the rules.

Today the API has one endpoint, `/health`, and no authentication. `LogAnalyzer.Infrastructure` reaches PostgreSQL through a single `NpgsqlDataSource`, with no ORM and no schema. The browser reaches the API only through nginx, which strips `/api`, so the Angular app and the API share one origin; in development the Angular CLI proxy does the same.

Two existing tests constrain the approach:
- `CoreDependencyTests` forbids EF Core and ASP.NET Core references in `LogAnalyzer.Core`.
- `HealthEndpointWithoutDatabaseTests` expects the API to start and answer `/health` with 503 `Unhealthy` when PostgreSQL is unreachable.

## Goals / Non-Goals

**Goals:**
- Use ASP.NET Core's account building blocks for password hashing, lockout and the user store instead of writing security code.
- Sessions that the server can end, and that tests can expire by moving a clock.
- A schema-migration path that later changes extend, starting with the runs table in `add-api-skeleton`.

**Non-Goals:**
- Running migrations as a separate deployment step. Startup migration is enough for the local stack.
- A scheduled cleanup of expired sessions (see Risks).

## Decisions

### ASP.NET Core Identity for users, passwords and lockout

`AddIdentityCore<AppUser>()` with the EF Core stores on an `IdentityUserContext<AppUser, Guid>` (user tables only, no role tables). Passwords are checked with `SignInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)`, which also counts failures and applies the lock.

Options:
- `Password.RequiredLength = 12`, every composition rule (digit, upper, lower, non-alphanumeric, unique characters) off.
- `Lockout.MaxFailedAccessAttempts = 5`, `Lockout.DefaultLockoutTimeSpan = 15 minutes`, `Lockout.AllowedForNewUsers = true`.
- `User.RequireUniqueEmail = true`. The user name is the email address, and `User.AllowedUserNameCharacters` is empty, so every valid address is accepted.

Passwords are stored by Identity's default hasher: PBKDF2 with HMAC-SHA512, a random salt per password and 100,000 iterations.

- Alternative: `MapIdentityApi<AppUser>()`. Rejected: its endpoints and response shapes are fixed, include refresh tokens, two-factor and email flows that are non-goals here, and do not produce this API's error codes.
- Alternative: hand-written hashing and lockout. Rejected: security-sensitive code that Identity already gets right.
- Alternative: `IdentityDbContext` with roles. Rejected: roles are a non-goal, and the user-only context creates fewer tables.

### Own endpoints under `/auth`

| Method and path | Public | On success |
|---|---|---|
| `POST /auth/register` | yes | 201 with `{ id, email }` |
| `POST /auth/sign-in` | yes | 200 with `{ id, email }` and the session cookie |
| `POST /auth/sign-out` | no | 204, session cookie expired |
| `GET /auth/me` | no | 200 with `{ id, email }` |

Through nginx these are `/api/auth/...`.

- Registration checks the email address (format, at most 254 characters) and the password length itself before calling `UserManager.CreateAsync`, so each request gets exactly one code, the email address first. A duplicate is recognized from Identity's `DuplicateUserName` and `DuplicateEmail` errors, and from a unique-index violation when two registrations race.
- Sign-in looks the account up by email address. When there is none, it still verifies the password against a fixed dummy hash, so the response time does not reveal whether the account exists.

### Errors are problem details with a `code`

Every error response is RFC 9457 problem details with a `code` extension member (`EMAIL_TAKEN`, `INVALID_CREDENTIALS`, ...). One table maps each code to its status, title and detail. The planned specs already say "responds 400 with code X"; this change is the first to implement that convention. A wrong password and an unknown email address use the same table entry, which is what makes their answers identical.

- Alternative: return Identity's error list. Rejected: it exposes framework error names and differs from the planned API's codes.

### A server-side session behind a cookie

A cookie authentication scheme whose `SessionStore` is a ticket store backed by a `user_sessions` table. The cookie carries only the protected session key; the authentication ticket lives in the table. Sign-out deletes the row, so a copied cookie stops working.

Cookie: name `la_session`, `HttpOnly`, always `Secure`, `SameSite=Strict`, path `/`, and no `Expires`, so the browser drops it when it closes. The cookie events answer 401 and 403 with problem details instead of redirecting to a login page.

- Alternative: the default self-contained cookie. Rejected: sign-out cannot invalidate a copied cookie before it expires.
- Alternative: JWT bearer tokens. Rejected when the change was scoped: page scripts would have to hold the token, and refresh and revocation need their own design. The browser is the only client.
- Alternative: Identity's security-stamp check on every request. Rejected: it signs a user out on every device at once and still costs a lookup per request.

### Idle timeout: renew on use, at most once a minute

`ExpireTimeSpan` is 8 hours. In `OnValidatePrincipal`, a request sets `ShouldRenew` once at least a minute has passed since the ticket was issued, which moves the expiry to now plus 8 hours and updates the stored row. The 8-hour period therefore restarts on every use, to the minute.

- Alternative: the built-in sliding expiration alone. Rejected: it renews only after half the lifetime has passed, so a session used 3 hours after sign-in would still end 8 hours after sign-in, which breaks "Using a session keeps it alive".
- Alternative: renew on every request. Rejected: a database write and a `Set-Cookie` header on every request for no practical gain.

### The clock comes from `TimeProvider`

The cookie options and the session store take the `TimeProvider` registered in DI, wired explicitly rather than relying on defaults. Integration tests replace it with `FakeTimeProvider` and advance it to test idle expiry. Lockout end times are compared by Identity, so the lockout-expiry test arranges "locked 15 minutes and 1 second ago" by writing the stored lockout end instead of moving the clock.

### Sign-in required by default

An authorization fallback policy that requires an authenticated user applies to every endpoint without its own policy. Public endpoints opt out with `AllowAnonymous()`: the health check, register, sign-in and, in Development, the OpenAPI document. A test reads every endpoint from `EndpointDataSource`, skips the public routes, and calls the rest without a session.

- Alternative: `RequireAuthorization()` on each route group. Rejected: one forgotten call leaves an endpoint open. The fallback policy fails closed.

Routing answers a request that matches a path but not its HTTP method or content type with a built-in rejection endpoint (405 or 415). That endpoint has no metadata, so the fallback policy would turn it into a 401 for anonymous callers, for example a form-encoded sign-in. A small middleware before authorization runs these rejection endpoints directly, so callers get the real status.

- Alternative: read the sign-in body by hand and return 415 from the endpoint. Rejected: the endpoint would lose its request schema in the OpenAPI document that the Angular client is generated from.

### Cross-site request protection: SameSite=Strict and JSON-only bodies

Browsers do not send a `SameSite=Strict` cookie on cross-site requests. The account endpoints accept only `application/json`, so a cross-site HTML form gets 415. The API sends no CORS headers, so other origins cannot make a credentialed JSON request.

- Alternative: antiforgery tokens. Rejected: an extra round trip and client code for protection that SameSite already provides in every current browser.

### EF Core arrives with the first migration

`LogAnalyzerDbContext` in `LogAnalyzer.Infrastructure` holds the Identity user tables, `user_sessions` and the data-protection keys. Names are mapped to snake_case with `UseSnakeCaseNamingConvention()`. The context reuses the existing `NpgsqlDataSource`.

Migrations live in `src/LogAnalyzer.Infrastructure/Persistence/Migrations`. `dotnet-ef` is pinned in a local tool manifest, and a design-time factory lets the tool create the context without the API's configuration.

- Alternative: Dapper with SQL scripts. Rejected: the project context names EF Core, and Identity's stores are built on it.

### Migrations run at startup without blocking it

A hosted service applies pending migrations when the API starts. Its first attempt runs during startup, so with a reachable database the schema is current before the first request. If that attempt fails, it retries every 5 seconds in the background. `/health` reports `Healthy` only when PostgreSQL answers and migrations are applied. Docker Compose and CI therefore still wait for a usable API, and the API still starts and reports `Unhealthy` when the database is unreachable.

- Alternative: migrate synchronously before `app.Run()`. Rejected: the API would crash instead of reporting `Unhealthy`, which breaks `HealthEndpointWithoutDatabaseTests`.
- Alternative: a separate migration container in Docker Compose. Rejected for now: one more service and image for a local stack. Revisit together with deployment.

### Data-protection keys in the database

The keys that protect the session cookie are stored in PostgreSQL with `PersistKeysToDbContext`, under the application name `LogAnalyzer`. By default they would live in the container's file system, and every `docker compose up --build` would sign everyone out.

- Alternative: a Docker volume for the key ring. Rejected: one more volume to manage, and the integration tests would need their own.

### Code placement

- `LogAnalyzer.Infrastructure`: `AppUser`, `UserSession`, the DbContext, migrations, the migration hosted service and the session repository.
- `LogAnalyzer.Api`: the `/auth` endpoints, cookie and authorization setup, the ticket-store adapter (it uses ASP.NET Core's ticket types), and the error-code table.
- `LogAnalyzer.Core`: unchanged, so `CoreDependencyTests` stays green.

`AppUser` derives from `IdentityUser<Guid>`. Ids are UUIDs, so they reveal nothing about how many accounts exist.

### Test setup

Integration tests use the existing `PostgresApiFactory` (one PostgreSQL container per test class) with `https://localhost` as the client base address, so `Secure` cookies are sent back. Each test uses its own email addresses, and tests that depend on time use a `FakeTimeProvider`. Every test carries `[Trait("Scenario", "user-accounts/<scenario-id>")]`.

## Risks / Trade-offs

- [Lockout reveals that an account exists: only real accounts answer ACCOUNT_LOCKED] → Accepted. Faking lockout state for unknown addresses costs more than it protects, and registration's EMAIL_TAKEN reveals the same by design.
- [Anyone who knows an email address can lock that account for 15 minutes] → Accepted for now. Per-client rate limiting in `add-input-safety` reduces it.
- [No absolute session lifetime: a session used at least every 8 hours never ends] → Revisit in a later change if needed. Sign-out still ends it at any time.
- [Expired session rows accumulate] → A row is deleted when its expired session is presented. A scheduled cleanup can come later; the table stays small on a local stack.
- [Some browsers, notably Safari, reject `Secure` cookies on `http://localhost`] → Use Chrome, Edge or Firefox for the local stack, or serve it over HTTPS in a later change.
- [EFCore.NamingConventions may not have a release for EF Core 10] → Map table and column names explicitly in the model configuration instead.
- [Data-protection keys are stored unencrypted] → Acceptable for the local stack. Encrypting them is production hardening, a non-goal.

## Migration Plan

The first migration creates the account, session and data-protection key tables in the existing `loganalyzer` database. The API applies it on its next start; nothing else changes in Docker Compose or CI. To roll back on a development machine, run `dotnet ef database update 0` or `docker compose down --volumes`.
