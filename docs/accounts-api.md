# Accounts API

People create an account with an email address and a password, then sign in to get a session. The rules come from the `user-accounts` spec (`openspec/changes/add-user-accounts/specs/user-accounts/spec.md` until the change is archived, then `openspec/specs/user-accounts/spec.md`).

Paths are as the API sees them. Through nginx (http://localhost:8080) and the Angular dev server, prefix them with `/api`, for example `/api/auth/register`.

## Errors

Every error is [problem details](https://www.rfc-editor.org/rfc/rfc9457) with a `code` member. Match on `code`, not on `title` or `detail`.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Email address already registered",
  "status": 409,
  "detail": "An account with this email address already exists.",
  "code": "EMAIL_TAKEN"
}
```

| Code | Status | Meaning |
|---|---|---|
| `INVALID_EMAIL` | 400 | The email address is not a valid address, or is longer than 254 characters. |
| `PASSWORD_TOO_SHORT` | 400 | The password has fewer than 12 characters. |
| `PASSWORD_TOO_LONG` | 400 | The password has more than 128 characters. |
| `EMAIL_TAKEN` | 409 | An account with this email address already exists. Letter case is ignored. |
| `INVALID_CREDENTIALS` | 401 | Sign-in failed: the email address has no account, or the password is wrong. Both get the same answer. |
| `ACCOUNT_LOCKED` | 403 | Too many failed sign-ins in a row. Sign-in to this account is refused for 15 minutes, even with the correct password. |
| `SIGN_IN_REQUIRED` | 401 | The endpoint needs a session, and the request has none, or its session has ended or was tampered with. |

## Sessions

Every endpoint needs a session except the health check, registration, sign-in and, in Development, the OpenAPI document. A new endpoint needs one too unless it is explicitly made public.

Signing in sets the session cookie `la_session`:

- `HttpOnly`, so page scripts cannot read it.
- `Secure`, so browsers send it only over HTTPS. They treat `http://localhost` as secure, except Safari, which drops the cookie there. Use Chrome, Edge or Firefox for the local stack.
- `SameSite=Strict`, so browsers never send it with requests from other sites.
- No `Expires`: the browser forgets it when it closes.

The cookie holds only a protected session id. The session itself is stored on the server, so signing out ends it even if someone copied the cookie.

A session ends after 8 hours without a request that uses it. Each request that uses it restarts the 8 hours; to save a database write per request, the server renews it at most once a minute. There is no maximum lifetime for a session that keeps being used.

## Register

`POST /auth/register` (public)

```json
{ "email": "ana@example.com", "password": "correct horse battery" }
```

- `201 Created` with `{ "id": "<uuid>", "email": "ana@example.com" }`. Registration does not sign you in.
- The email address is the sign-in name. It is unique regardless of letter case.
- The password must be 12 to 128 characters. There are no rules about digits, letter case or symbols. It is stored only as a salted, slow hash.
- Errors are checked in this order and only the first is reported: `INVALID_EMAIL`, `PASSWORD_TOO_SHORT`, `PASSWORD_TOO_LONG`, `EMAIL_TAKEN`.

## Sign in

`POST /auth/sign-in` (public)

```json
{ "email": "ana@example.com", "password": "correct horse battery" }
```

- `200 OK` with `{ "id": "<uuid>", "email": "ana@example.com" }` and the session cookie.
- The email address is matched regardless of letter case.
- The body must be JSON. Any other content type, such as an HTML form, gets `415` and no session.
- A wrong password and an unknown email address both get `401` with `INVALID_CREDENTIALS`.
- The fifth failed sign-in in a row locks the account for 15 minutes. That attempt, and every sign-in to the account until the lock ends, gets `403` with `ACCOUNT_LOCKED`, even with the correct password. A successful sign-in resets the count. Because only real accounts can be locked, `ACCOUNT_LOCKED` reveals that an account exists; this is an accepted trade-off.

## Current user

`GET /auth/me` (needs a session)

- `200 OK` with `{ "id": "<uuid>", "email": "ana@example.com" }`.
- Without a valid session: `401` with `SIGN_IN_REQUIRED`.

## Sign out

`POST /auth/sign-out` (needs a session)

- `204 No Content`. The server deletes the session and the response expires the cookie.
- A copy of the cookie taken before signing out gets `401` with `SIGN_IN_REQUIRED` afterwards.
