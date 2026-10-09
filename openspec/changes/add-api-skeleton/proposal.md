# Proposal: Add the API skeleton

## Why

To exercise the whole pipeline before paying for model calls, the system needs a way to accept logs, track each analysis as a run and keep every model attempt. Building this against a stand-in model makes the API, persistence and recovery path testable and demoable before Claude is wired in.

## What Changes

- Accept logs as pasted text or uploaded files, rejecting empty, oversized and non-text submissions.
- Compute log statistics (line count, level counts, time range) in code.
- Track each submission as a run that moves from queued to completed or failed; read a single run and list recent runs.
- Store every model attempt with its validation outcome, and retry once with corrective feedback when a response is rejected.
- Use a stand-in model that returns scripted responses; Claude replaces it in `add-claude-integration`.

## Capabilities

### New Capabilities

- `log-ingestion`: which submissions are accepted, how lines are numbered and which facts about a log are computed by code.
- `analysis-history`: how runs and their model attempts are recorded and read back.

### Modified Capabilities

- `response-validation`: adds the corrective retry after a rejected response.

## Non-Goals

- Real model calls (`add-claude-integration`).
- Logs too large for a single model request (`add-large-log-support`).
- Redaction, rate limiting and authentication (`add-input-safety`).

## Impact

- New API endpoints for submitting, reading and listing runs, plus a background worker that processes queued runs.
- PostgreSQL tables and migrations for runs, statistics, findings and attempts.
- No real model calls, so no token cost.
