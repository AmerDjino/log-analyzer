# Proposal: Add the Claude integration

## Why

With the contract, pipeline and UI proven against a stand-in, the real model can be added where it is easy to measure: every call is bounded, retried only when that helps, recorded with its cost, and judged against a golden set of logs whose right answers are known.

## What Changes

- Add Claude through the official C# SDK behind the model seam, requesting output in the format of `schemas/analysis-result.schema.json`. A `Model:Provider` setting selects it. The stand-in stays the default, so the local stack, CI and the tests run without an API key.
- Build every request from a versioned prompt template and record the version, model, tokens and latency of every attempt.
- Keep the API key on the server, required only when Claude is the selected provider, bound every call with a timeout and retry only transient failures.
- Map the Messages API's stop reasons as the `add-analysis-contract` design records: `end_turn` is a complete answer, `max_tokens` and `model_context_window_exceeded` a response cut off, `refusal` a refusal, and any other stop reason is never accepted as complete.
- Add the golden-set eval suite for the `[eval]` scenarios, with logs whose planted incidents are known.
- Reuse the person's own earlier run for an identical submission instead of calling the model again. Another person's runs are never reused.

## Capabilities

### New Capabilities

- `model-gateway`: how the system calls the model and what it records about each call.

### Modified Capabilities

- `log-analysis`: adds requirements on the quality of findings, checked by `[eval]` scenarios.
- `analysis-history`: adds reuse of results for identical submissions.

## Non-Goals

- Logs that do not fit in one request (`add-large-log-support`).
- Redaction of secrets in logs (`add-input-safety`).
- Rejecting NUL characters in model text: they are removed before storing, as `add-api-skeleton` does, and the output contract does not change.

## Impact

- New: the Claude client in `LogAnalyzer.Infrastructure`, prompt templates under `prompts/`, the eval project's golden set, and the API key as a Docker secret.
- Token and cost impact: each run makes one model call, or two when a correction is needed. Input is the numbered log plus roughly 1,000–2,000 tokens of instructions; output is capped by a configured limit. The eval suite runs on demand or nightly, not in pull-request CI.
