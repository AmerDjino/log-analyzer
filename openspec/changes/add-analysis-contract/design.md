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

`schemas/analysis-result.schema.json` is loaded by the validator and, from `add-claude-integration` on, sent to Claude as the structured-output format. `LogAnalyzer.Core` embeds the file as a resource, so both uses read exactly the same bytes; the API image therefore copies `schemas/` into its build stage. A unit test keeps the C# contract types and the schema's fields and closed sets in step.

The validator checks the schema with a small interpreter for exactly the keywords the contract uses: `type` (object, array, string, integer), `properties`, `required`, `additionalProperties: false`, `items`, `enum` and `minItems`. A schema with any other keyword or type fails to load, so the file can never promise a rule that the validator does not enforce.

- Alternative: derive the schema from C# records, for example with the SDK's typed `Create<T>()`. Rejected: spec-first means the contract exists before the code, and a schema file can be reviewed on its own.
- Alternative: hand-written C# checks with no schema. Rejected: it duplicates the contract and drifts.
- Alternative: a general JSON Schema library. Rejected: it adds a dependency to `LogAnalyzer.Core` for a handful of keywords, and its error locations (`/findings/0` plus a message naming the missing property) would have to be translated into this contract's paths (`findings[0].suggested_action`).

### Only structured-output-compatible keywords in the schema

Every property is required, every object sets `additionalProperties: false`, the finding schema is inlined rather than referenced, and there are no numeric or length constraints (only `minItems: 1`, which the API supports). The file can then be sent to the API unchanged. Limits the schema cannot express (non-blank text, title and summary length, line range, verbatim evidence) live in the validator; the requirement "Enforce limits the schema cannot express" covers them.

### Strict parsing: reject fenced or wrapped JSON

A response must be exactly one JSON object, otherwise `NOT_JSON`. The parser also refuses comments, trailing commas and a field that appears twice, because a repeated field leaves it unclear which value counts.

- Alternative: extract JSON from code fences or surrounding prose. Rejected: it hides prompt problems from the rejection metrics. With structured outputs the case should not occur, and the corrective retry in `add-api-skeleton` recovers when it does.

### Closed-set values are matched regardless of letter case

The schema check compares severity and category with their closed sets regardless of letter case, because structured outputs do not guarantee the letter case of enum values. An accepted analysis holds them as the `Severity` and `FindingCategory` enums, whose contract spelling is lowercase, so a stored value is always lowercase.

### Exact evidence matching

Evidence must be an exact substring of a cited line after trimming its surrounding whitespace. The validator receives the log as lines without line breaks, so evidence that spans two lines never matches. Evidence is checked only against cited lines that exist: when every cited line is outside the log, the finding gets `LINE_OUT_OF_RANGE` alone. From `add-input-safety` on, it is matched against the redacted line, which is what the model saw.

- Alternative: fuzzy matching. Rejected: it weakens the guarantee against invented evidence. Revisit only if evals show false rejections.

### No confidence score in findings

- Alternative: a model-reported confidence between 0 and 1. Rejected: self-reported confidence is poorly calibrated and invites false precision; severity plus cited evidence carry the signal.

### The validator returns a result instead of throwing

`Validate(input)` returns either an accepted analysis or a list of violations, each with a reason code, a JSON path and a message. The input holds the response text, the stop reason reported by the model service and the numbered log lines. A result object feeds the corrective retry and the rejection metrics directly.

- Reason codes are strings (`ReasonCodes.LineOutOfRange` is `"LINE_OUT_OF_RANGE"`), the form the specs, the documentation and stored attempts use.
- Violations come in a fixed order: contract violations, then citations, then text limits, each in document order. Values with the wrong type are reported once, by the schema check, and skipped by the later checks.
- Lengths count Unicode code points, so an emoji counts as one character, as in the account rules.

### Statistics stay out of the model's output

Line counts, level counts and time ranges are computed by code in `add-api-skeleton`; the model is asked only for judgment.

## Risks / Trade-offs

- [A strict contract may reject many early responses] → Track the rejection rate per reason and fix the prompt before loosening the contract.
- [Evidence matching can fail on Unicode or whitespace differences] → Normalize line endings and trim; add a scenario for each new case found.
- [The 120- and 1,000-character limits are first guesses] → Adjust them through a later change if evals show they cut useful content.
