# Proposal: Add the web shell

## Why

The trust story is easiest to judge when you can see it: findings next to the exact log lines they cite. A first UI built against the stand-in model lets the API contract and the user flow be tested end to end before real model calls add cost and latency.

## What Changes

- A submit page for pasting or uploading a log, with size and empty-input checks before anything is sent.
- A run page that follows the run until it finishes, then shows the summary and findings ordered by severity, with a severity filter.
- An evidence view that shows a finding's cited lines in context with the evidence highlighted.
- A list of past runs.
- Plain-language messages for API errors that keep the person's input.
- An API endpoint that serves a range of a run's log lines for the evidence view.

## Capabilities

### New Capabilities

- `analysis-ui`: what a person can do in the web interface.

### Modified Capabilities

- `analysis-history`: adds serving a range of a run's log lines.

## Non-Goals

- Progress per chunk for large logs (`add-large-log-support`).
- The attempt timeline and insights (`add-attempt-timeline-and-insights`).
- Sign-in and per-user history.

## Impact

- New Angular pages and a TypeScript API client generated from the API's OpenAPI document.
- One new read-only API endpoint for log lines.
- No model calls, so no token cost.
