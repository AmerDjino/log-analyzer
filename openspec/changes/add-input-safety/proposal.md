# Proposal: Add input safety

## Why

Logs routinely contain connection strings, tokens and personal data, and anything in a log is text an attacker may control. Before real logs go through the analyzer, secrets must be removed before they are stored or sent anywhere, log content must never steer the model, and submissions must be rate-limited because every analysis costs money. Sign-in and registration need a limit too: lockout stops repeated guesses against one account, but nothing stops one client from trying many accounts, or from locking them.

## What Changes

- Redact connection-string passwords, bearer tokens, API keys and email addresses before a log is stored or sent to the model, keeping line numbering intact.
- Treat log content as data, so instructions written inside a log cannot change the analysis.
- Limit the number of submissions per signed-in person per minute.
- Limit sign-in and registration attempts per client address per minute. A refused attempt does not count toward an account's lockout.
- Show redaction markers distinctly in the evidence view.
- Tell a rate-limited person how long to wait, keeping their input.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `log-ingestion`: adds redaction and the per-person submission limit.
- `log-analysis`: adds resistance to instructions embedded in logs, checked by an `[eval]` scenario.
- `analysis-ui`: adds labeled redaction markers and the rate-limit message.
- `user-accounts`: adds the per-client limit on sign-in and registration attempts.

## Non-Goals

- Quotas over longer periods, such as a number of analyses per person per day; only the per-minute limits apply.
- Detecting every possible kind of secret; only the kinds listed above are redacted.

## Impact

- New redaction step at ingestion, and rate limiting on the submission endpoint (per person) and on sign-in and registration (per client address).
- Requests reach the API through nginx, so the API must take the client address from the `X-Forwarded-For` header that nginx sets, and trust that header only from nginx.
- Evidence matching now runs against redacted lines, which is what the model sees.
- Token and cost impact: negligible, since redaction markers are about as long as the values they replace.
