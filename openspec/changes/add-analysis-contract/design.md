# Design: Add the analysis contract

## Context

This is the first behavior change, and nothing calls a model yet. See proposal.md for motivation and the delta specs for the rules. Later changes build on this contract: the prompt's output format (`add-claude-integration`), the run resource (`add-api-skeleton`) and the findings view (`add-web-shell`).

## Goals / Non-Goals

**Goals:**
- One machine-readable contract that is both sent to the model and enforced by the validator.
- A validator that is a pure function: no network, no database, no clock.

**Non-Goals:**
- Corrective retries and persistence (`add-api-skeleton`).
- Evaluating the quality of findings (`add-claude-integration`).

## Decisions

### The JSON Schema file is the single source of truth

`schemas/analysis-result.schema.json` is loaded by the validator and, from `add-claude-integration` on, sent to Claude as the structured-output format.

- Alternative: derive the schema from C# records, for example with the SDK's typed `Create<T>()`. Rejected: spec-first means the contract exists before the code, and a schema file can be reviewed on its own.
- Alternative: hand-written C# checks with no schema. Rejected: it duplicates the contract and drifts.

### Only structured-output-compatible keywords in the schema

Every property is required, every object sets `additionalProperties: false`, the finding schema is inlined rather than referenced, and there are no numeric or length constraints (only `minItems: 1`, which the API supports). The file can then be sent to the API unchanged. Limits the schema cannot express (non-blank text, title and summary length, line range, verbatim evidence) live in the validator; the requirement "Enforce limits the schema cannot express" covers them.

### Strict parsing: reject fenced or wrapped JSON

A response must be exactly one JSON object, otherwise `NOT_JSON`.

- Alternative: extract JSON from code fences or surrounding prose. Rejected: it hides prompt problems from the rejection metrics. With structured outputs the case should not occur, and the corrective retry in `add-api-skeleton` recovers when it does.

### Closed-set values are normalized before the schema check

Severity and category are lowercased before schema validation, because structured outputs do not guarantee the letter case of enum values.

### Exact evidence matching

Evidence must be an exact substring of a cited line after trimming surrounding whitespace and normalizing line endings. From `add-input-safety` on, it is matched against the redacted line, which is what the model saw.

- Alternative: fuzzy matching. Rejected: it weakens the guarantee against invented evidence. Revisit only if evals show false rejections.

### No confidence score in findings

- Alternative: a model-reported confidence between 0 and 1. Rejected: self-reported confidence is poorly calibrated and invites false precision; severity plus cited evidence carry the signal.

### The validator returns a result instead of throwing

`Validate(input)` returns either an accepted analysis or a list of violations, each with a reason code, a JSON path and a message. The input holds the response text, the stop reason reported by the model service and the numbered log lines. A result object feeds the corrective retry and the rejection metrics directly.

### Statistics stay out of the model's output

Line counts, level counts and time ranges are computed by code in `add-api-skeleton`; the model is asked only for judgment.

## Risks / Trade-offs

- [A strict contract may reject many early responses] → Track the rejection rate per reason and fix the prompt before loosening the contract.
- [Evidence matching can fail on Unicode or whitespace differences] → Normalize line endings and trim; add a scenario for each new case found.
- [The 120- and 1,000-character limits are first guesses] → Adjust them through a later change if evals show they cut useful content.
