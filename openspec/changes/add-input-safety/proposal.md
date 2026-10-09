# Proposal: Add input safety

## Why

Logs routinely contain connection strings, tokens and personal data, and anything in a log is text an attacker may control. Before real logs go through the analyzer, secrets must be removed before they are stored or sent anywhere, log content must never steer the model, and submissions must be rate-limited because every analysis costs money.

## What Changes

- Redact connection-string passwords, bearer tokens, API keys and email addresses before a log is stored or sent to the model, keeping line numbering intact.
- Treat log content as data, so instructions written inside a log cannot change the analysis.
- Limit the number of submissions per client per minute.
- Show redaction markers distinctly in the evidence view.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `log-ingestion`: adds redaction and the submission rate limit.
- `log-analysis`: adds resistance to instructions embedded in logs, checked by an `[eval]` scenario.
- `analysis-ui`: adds labeled redaction markers.

## Non-Goals

- Sign-in and per-user quotas.
- Detecting every possible kind of secret; only the kinds listed above are redacted.

## Impact

- New redaction step at ingestion and rate limiting on the submission endpoint.
- Evidence matching now runs against redacted lines, which is what the model sees.
- Token and cost impact: negligible, since redaction markers are about as long as the values they replace.
