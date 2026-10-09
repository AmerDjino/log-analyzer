# Proposal: Add the analysis contract

## Why

The assistant is only as trustworthy as the check on its output. Defining what an analysis contains, and when a model response must be rejected, before any model is called gives every later change (prompt, API, UI) a fixed, testable definition of an acceptable answer, and guarantees that invalid output never reaches a user.

## What Changes

- Define what an analysis contains: a summary plus findings, each with a title, severity, category, cited line numbers, verbatim evidence, an explanation and a suggested action.
- Fix the closed sets of severities and categories, with a definition for each value.
- Publish the machine-readable contract as `schemas/analysis-result.schema.json`, limited to JSON Schema features that Claude's structured outputs accept.
- Add a validation layer that accepts or rejects a model response and reports every violation with a reason code: incomplete responses, non-JSON output, contract violations, citations that don't match the log, and limits the schema cannot express.
- No model calls yet: everything is proven with canned responses.

## Capabilities

### New Capabilities

- `log-analysis`: what an analysis of a log contains and the rules its findings follow.
- `response-validation`: how a model response is checked before the system uses any part of it.

### Modified Capabilities

- None.

## Non-Goals

- Calling the model, storing results or retrying rejected responses (`add-api-skeleton`, `add-claude-integration`).
- Judging the quality of findings; that needs the real model and is covered by `[eval]` scenarios in `add-claude-integration`.

## Impact

- New: `schemas/analysis-result.schema.json` and the validation component in `LogAnalyzer.Core`.
- The prompt's output format, the API's run resource and the UI's findings view are all built on this contract.
- No model calls, so no token cost.
