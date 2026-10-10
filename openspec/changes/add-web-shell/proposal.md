# Proposal: Add the web shell

## Why

The trust story is easiest to judge when you can see it: findings next to the exact log lines they cite. A first UI built against the stand-in model lets the API contract and the user flow be tested end to end before real model calls add cost and latency. Since `add-user-accounts`, every API endpoint except registration, sign-in and the health check needs a session, so the shell starts with creating an account and signing in.

## What Changes

- Create-account, sign-in and sign-out screens. A page that needs a session sends a signed-out person to sign-in, and back to that page afterwards.
- A submit page for pasting or uploading a log, with size and empty-input checks before anything is sent.
- A run page that follows the run until it finishes, then shows the summary and findings ordered by severity, with a severity filter.
- An evidence view that shows a finding's cited lines in context with the evidence highlighted.
- A list of the person's own past runs.
- Plain-language messages for API errors that keep the person's input, including when a session ends.
- An API endpoint that serves a range of a run's log lines for the evidence view, only to the person who submitted the run.

## Capabilities

### New Capabilities

- `analysis-ui`: what a person can do in the web interface, from creating an account and signing in to reading findings.

### Modified Capabilities

- `analysis-history`: adds serving a range of a run's log lines.

## Non-Goals

- Progress per chunk for large logs (`add-large-log-support`).
- The attempt timeline and insights (`add-attempt-timeline-and-insights`).
- Password reset, password change and account settings, which the API does not offer.

## Impact

- New Angular pages and a TypeScript API client generated from the API's OpenAPI document.
- The OpenAPI document gains a description of the `code` member of error answers, which the generated client needs to pick a message. Today it describes errors as plain problem details.
- One new read-only API endpoint for log lines.
- Signing in on the local stack needs Chrome, Edge or Firefox: Safari drops the `Secure` session cookie on `http://localhost`.
- No model calls, so no token cost.
