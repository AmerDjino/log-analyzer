# Accounts API

People create an account with an email address and a password, then sign in to get a session. The rules come from the `user-accounts` spec, `openspec/specs/user-accounts/spec.md`. The decisions behind them are in `openspec/changes/archive/2026-10-10-add-user-accounts/design.md`.

Paths are as the API sees them. Through nginx (http://localhost:8080) and the Angular dev server, prefix them with `/api`, for example `/api/auth/register`.

## Errors

Every error is [problem details](https://www.rfc-editor.org/rfc/rfc9457). Errors the API reports itself carry a `code` member: match on `code`, not on `title` or `detail`. Answers that come from the framework carry no `code`:

- `400` for a JSON body that is empty or malformed, or for no body at all.
- `404` for a path that does not exist. Without a session, any unknown path gets `401` with `SIGN_IN_REQUIRED` instead.
- `405` for a wrong HTTP method.
- `415` when an endpoint that takes a JSON body gets a body with another `Content-Type`, or with none.
- `500` for an unexpected error.

Through nginx, a request body larger than 12 MB gets nginx's own `413` HTML page, not problem details.

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
| `UNSUPPORTED_CHARSET` | 415 | The body's `Content-Type` names a character set other than UTF-8, such as `application/json; charset=utf-16`. Bodies must be UTF-8, as RFC 8259 requires for JSON; `utf-8` and `utf8` are accepted in any letter case, quoted or not, and no charset means UTF-8. |

The OpenAPI document lists every code the API reports, from this table and the one in [runs-api.md](runs-api.md#errors), as the values of `code` in its `ProblemDetails` schema.

## Sessions

Every endpoint needs a session except the health check, registration, sign-in and, in Development, the OpenAPI document. A new endpoint needs one too unless it is explicitly made public.

Signing in sets the session cookie `la_session`:

- `HttpOnly`, so page scripts cannot read it.
- `Secure`, so browsers send it only over HTTPS. They treat `http://localhost` as secure, except Safari, which drops the cookie there. Use Chrome, Edge or Firefox for the local stack.
- `SameSite=Strict`, so browsers never send it with requests from other sites.
- No `Expires`: it is a session cookie, which browsers normally forget when they close. Browsers that restore the previous session can keep it, so sign out on a shared computer.

The cookie holds only a protected session id. The session itself is stored on the server, so signing out ends it even if someone copied the cookie.

A session ends 8 hours after it was last renewed. A request that uses it renews it when at least a minute has passed since the last renewal, which saves a database write per request. So a session can end up to a minute before 8 hours without use. There is no maximum lifetime for a session that keeps being used.

## Register

`POST /auth/register` (public)

```json
{ "email": "ana@example.com", "password": "correct horse battery" }
```

- `201 Created` with `{ "id": "<uuid>", "email": "ana@example.com" }`. Registration does not sign you in.
- The email address is the sign-in name. It is unique regardless of letter case.
- The password must be 12 to 128 characters. Every Unicode character counts as one, including spaces and emoji such as 😀. Some emoji, such as a thumbs-up with a skin tone, are built from two or more characters. There are no rules about digits, letter case or symbols. The password is stored only as a salted, slow hash.
- Errors are checked in this order and only the first is reported: `INVALID_EMAIL`, `PASSWORD_TOO_SHORT`, `PASSWORD_TOO_LONG`, `EMAIL_TAKEN`.
- The body must be JSON in UTF-8. Any other content type gets `415`, and a body that declares another character set gets `415` with `UNSUPPORTED_CHARSET`. An empty or malformed JSON body gets `400` without a `code`. None of these creates an account.

## Sign in

`POST /auth/sign-in` (public)

```json
{ "email": "ana@example.com", "password": "correct horse battery" }
```

- `200 OK` with `{ "id": "<uuid>", "email": "ana@example.com" }` and the session cookie.
- The email address is matched regardless of letter case.
- The body must be JSON in UTF-8. Any other content type, such as an HTML form, gets `415` and no session, and so does a body that declares another character set, with `UNSUPPORTED_CHARSET`. An empty or malformed JSON body gets `400` and no session.
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
