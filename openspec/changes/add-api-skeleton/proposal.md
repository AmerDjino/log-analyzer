# Proposal: Add the API skeleton

## Why

To exercise the whole pipeline before paying for model calls, the system needs a way to accept logs, track each analysis as a run and keep every model attempt. Building this against a stand-in model makes the API, persistence and recovery path testable and demoable before Claude is wired in.

## What Changes

- Accept logs as pasted text or uploaded files, rejecting empty, oversized and non-text submissions.
- Compute log statistics (line count, level counts, time range) in code.
- Track each submission as a run that moves from queued to completed or failed. A run belongs to the signed-in person who submitted it, and only that person can read it or see it listed.
- Store every model attempt with its validation outcome, and retry once with corrective feedback when a response is rejected.
- Use a stand-in model that returns scripted responses; Claude replaces it in `add-claude-integration`.
- Answer 415 instead of 500 when a request body declares a character set the API does not support, on these endpoints and on registration and sign-in.

## Capabilities

### New Capabilities

- `log-ingestion`: which submissions are accepted, how lines are numbered and which facts about a log are computed by code.
- `analysis-history`: how runs and their model attempts are recorded, and how the person who submitted a run reads it back.

### Modified Capabilities

- `response-validation`: adds the corrective retry after a rejected response.
- `user-accounts`: registration and sign-in answer 415 for a body in a character set the API does not support.

## Non-Goals

- Real model calls (`add-claude-integration`).
- Logs too large for a single model request (`add-large-log-support`).
- Redaction and rate limiting (`add-input-safety`).
- Sharing a run with other people.

## Impact

- New API endpoints for submitting, reading and listing runs, plus a background worker that processes queued runs. Like every endpoint since `add-user-accounts`, they need a session.
- PostgreSQL tables and migrations for runs, statistics, findings and attempts. Each run references the account that submitted it.
- The 415 answer applies to every endpoint that reads a body, including registration and sign-in, which answer 500 today.
- No real model calls, so no token cost.
