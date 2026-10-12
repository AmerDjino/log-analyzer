# Output contract

What the model must return for one log, and how each response is checked before the system uses any part of it. The rules come from the `log-analysis` and `response-validation` specs in `openspec/specs/`. The decisions behind them are in `openspec/changes/archive/2026-10-10-add-analysis-contract/design.md`.

The machine-readable contract is [`schemas/analysis-result.schema.json`](../schemas/analysis-result.schema.json). The validator in `LogAnalyzer.Core` checks every response against it, and Claude receives it as the response format of every request (structured outputs), so its answer has the contract's shape. The schema uses only what Claude's structured outputs accept, so a few limits (non-blank text, title and summary length, line range, evidence on the cited line) are checked by the validator alone.

## The response

One JSON object with exactly these fields:

| Field | Type | Rules |
|---|---|---|
| `summary` | string | What the log shows, in one to three sentences. Not blank, at most 1,000 characters. Says plainly when no problems were found. |
| `findings` | array of findings | The distinct problems the log shows. Empty when there are none. |

Each finding has exactly these seven fields:

| Field | Type | Rules |
|---|---|---|
| `title` | string | A short headline. Not blank, at most 120 characters. |
| `severity` | string | One of the [severities](#severities). |
| `category` | string | One of the [categories](#categories). |
| `line_refs` | array of whole numbers | The cited lines, at least one. Line numbers start at 1 and count the lines of the log as submitted. A whole number may be written as 12, 12.0 or 1.2e1. |
| `evidence` | string | Not blank. An excerpt copied character for character from one of the cited lines; whitespace at its start and end is ignored. |
| `explanation` | string | Not blank. Why the problem matters and its most likely cause, based only on what the log shows. |
| `suggested_action` | string | Not blank. The next thing an engineer should check or do. |

No other field is allowed, in the response or in a finding. Lengths count Unicode characters (code points), so an emoji such as 😀 counts as one.

```json
{
  "summary": "The database connection pool ran out and orders failed to save.",
  "findings": [
    {
      "title": "Database connection pool exhausted",
      "severity": "critical",
      "category": "resource",
      "line_refs": [12],
      "evidence": "Database connection pool exhausted",
      "explanation": "Every pooled database connection is in use, so new requests cannot reach the database and orders fail to save.",
      "suggested_action": "Look for connections that are not disposed, then check the Max Pool Size in the connection string."
    }
  ]
}
```

## Severities

| Value | Meaning |
|---|---|
| `critical` | An outage, crash or data loss. |
| `error` | Failed operations without a full outage. |
| `warning` | Degradation or a risk of failure. |
| `info` | Noteworthy but harmless. |

## Categories

| Value | Meaning |
|---|---|
| `exception` | Unhandled or logged exceptions. |
| `timeout` | Operations exceeding a time limit. |
| `dependency` | Failing calls to databases, HTTP services or queues. |
| `resource` | Exhausted memory, connections, threads or disk. |
| `security` | Authentication, authorization or suspicious access. |
| `performance` | Slow operations that still succeed. |
| `configuration` | Missing or invalid settings. |
| `other` | Anything else. |

Severities and categories are matched regardless of letter case and kept in lowercase: `"Critical"` is accepted as `critical`.

## How a response is checked

The validator checks every response before the system stores or shows any part of it. A response with even one violation is rejected as a whole.

1. A response that the model service reports as cut off (at its output limit or its context window), or as a refusal, is rejected with that one reason, without reading its text.
2. The text must be exactly one JSON object: no markdown code fence, no text before or after it, no comments, no trailing commas, no field named twice and no unpaired surrogate, such as the escape `\uD800`. Anything else is rejected with `NOT_JSON` alone, and its message includes the parser's reason.
3. Otherwise the validator reports every violation it finds, each with its reason code, the path of the field and a message: contract violations first, then citations, then text limits. A path looks like `findings[0].line_refs[0]`; the reasons in steps 1 and 2 concern the whole response and have an empty path.

| Code | Meaning |
|---|---|
| `TRUNCATED` | The model reached its output limit or its context window, so the response is cut off. |
| `REFUSED` | The model declined to answer. |
| `NOT_JSON` | The response is not exactly one JSON object. |
| `SCHEMA_VIOLATION` | A field is missing, unknown or of the wrong type, `line_refs` is empty, or a severity or category is not one of its values. |
| `LINE_OUT_OF_RANGE` | A cited line number is below 1 or past the last line of the log, however large. |
| `EVIDENCE_NOT_FOUND` | The evidence, without the whitespace at its start and end, does not appear on any of the finding's cited lines. It is only checked when at least one cited line exists. |
| `BLANK_FIELD` | A text field is empty or contains only whitespace. A NUL character (U+0000) counts as whitespace, because stored text cannot hold it. |
| `FIELD_TOO_LONG` | The summary is longer than 1,000 characters, or a title longer than 120. |
