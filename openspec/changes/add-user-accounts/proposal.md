# Proposal: Add user accounts

## Why

Every analysis will hold a submitted log, which can contain internal details, and every analysis costs money. The API needs to know who is calling before it stores its first run, so that only people with an account can use it and, from `add-api-skeleton` on, each person sees only their own analyses. Adding accounts before runs exist means runs belong to a user from the first table instead of being retrofitted.

## What Changes

- Register an account with an email address and a password. The email address is the sign-in name and is unique regardless of letter case. Passwords must be 12 to 128 characters and are stored only as salted hashes.
- Sign in with email and password. A successful sign-in starts a server-side session carried in a cookie that page scripts cannot read and that the browser sends only to this site.
- Sign out, which ends the session on the server, so a copied cookie stops working.
- Read the signed-in user.
- Lock an account for 15 minutes after 5 failed sign-ins in a row.
- End a session after 8 hours without activity.
- Require a signed-in user for every API endpoint by default. Only the health check, registration and sign-in stay public, plus the OpenAPI document in Development.
- Store accounts and sessions in PostgreSQL through the project's first schema migrations.

## Capabilities

### New Capabilities

- `user-accounts`: creating an account, signing in and out, sessions and their expiry, lockout after failed sign-ins, and which endpoints need a signed-in user. None of the existing capabilities covers who the caller is.

### Modified Capabilities

- None. No capability has an archived main spec yet, and this change adds no requirements to the capabilities planned by other changes.

## Non-Goals

- Email confirmation, password reset and password change, which need an email sender.
- Sign-in with external providers (Google, GitHub, Microsoft) and two-factor authentication.
- Roles and administration.
- Sign-in and registration screens in the Angular app.
- Tying runs to users: `add-api-skeleton` does that on top of this change.
- Rate limiting sign-in attempts per client. Lockout covers repeated guesses against one account.
- Deleting an account or changing its email address.
- Production hardening, such as encrypting the cookie protection keys at rest.

## Impact

- **Order:** implement before `add-api-skeleton`. Independent of `add-analysis-contract`.
- **Database:** the first schema migrations, for accounts, sessions and the keys that protect session cookies. The API applies pending migrations at startup.
- **API:** new account endpoints. Every endpoint now requires a session unless it is marked public. `/health` stays public, so the Docker Compose and CI health checks are unchanged.
- **Dependencies:** EF Core, already named in the project context, and the ASP.NET Core account packages enter `LogAnalyzer.Infrastructure`. `LogAnalyzer.Core` stays free of them.
- **Follow-ups in other active changes** (not edited by this change):
  - `add-api-skeleton`: runs belong to the signed-in user and the run list shows only theirs. Its non-goal "authentication (`add-input-safety`)" is out of date.
  - `add-web-shell`: its non-goal "Sign-in and per-user history" conflicts with this change. The UI needs sign-in and registration screens, there or in a separate change.
  - `add-input-safety`: the submission limit "per client" can become per user, and its non-goal "Sign-in and per-user quotas" needs revisiting.
  - README: add this change to the planned list.
- **Cost:** no model calls, so no token cost.
