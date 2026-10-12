# Proposal: Add the web shell

## Why

The trust story is easiest to judge when you can see it: findings next to the exact log lines they cite. A first UI built against the stand-in model lets the API contract and the user flow be tested end to end before real model calls add cost and latency. Since `add-user-accounts`, every API endpoint except registration, sign-in and the health check needs a session, so the shell starts with creating an account and signing in.

## What Changes

- Create-account, sign-in and sign-out screens. A page that needs a session sends a signed-out person to sign-in, and back to that page afterwards.
- A submit page for pasting or uploading a log. Submitting is disabled while the input is empty, and a file larger than the API's configured size limit is refused before it is uploaded. A chosen file is sent as its bytes, unchanged, so the API can still refuse one that is not UTF-8 text.
- A run page that follows the run until it finishes, then shows the summary and findings ordered by severity, with a severity filter.
- An evidence view that shows a finding's cited lines in context with the evidence highlighted.
- A list of the person's own past runs.
- Plain-language messages for API errors that keep the person's input, including when a session ends.
- An API endpoint that serves a range of a run's log lines for the evidence view, only to the person who submitted the run.
- An API endpoint that reports the configured submission size limit, so the submit page checks the limit the API enforces instead of assuming 10 MB.

## Capabilities

### New Capabilities

- `analysis-ui`: what a person can do in the web interface, from creating an account and signing in to reading findings.

### Modified Capabilities

- `analysis-history`: adds serving a range of a run's log lines.
- `log-ingestion`: adds reporting the configured submission size limit.

## Non-Goals

- Progress per chunk for large logs (`add-large-log-support`).
- The attempt timeline and insights (`add-attempt-timeline-and-insights`).
- Password reset, password change and account settings, which the API does not offer.
- Checking the size of pasted text before it is sent. The API refuses it with INPUT_TOO_LARGE, and the interface explains that answer.

## Impact

- New Angular pages and a TypeScript API client generated from the API's OpenAPI document.
- The OpenAPI document gains a description of the `code` member of error answers, which the generated client needs to pick a message. Today it describes errors as plain problem details.
- Two new read-only API endpoints: a run's log lines, and the submission size limit.
- Carries out the follow-ups that `add-api-skeleton` left for this change: uploads are sent as raw bytes, and the size limit comes from the API.
- nginx still refuses bodies over 12 MB with its own 413 page. A size limit configured above that needs nginx raised too, as `docs/runs-api.md` says, or a file between the two limits passes the interface's check and then fails.
- Signing in on the local stack needs Chrome, Edge or Firefox: Safari drops the `Secure` session cookie on `http://localhost`.
- No model calls, so no token cost.
