# Test Plan

Every scenario in this change is deterministic. All tests run against canned model responses and need no network, database or API key.

| Scenario id | Test | Kind | Project |
|---|---|---|---|
| `log-analysis/analysis-with-findings-is-accepted` | `Validate_AcceptsAnalysisWithFindings` | unit | LogAnalyzer.UnitTests |
| `log-analysis/analysis-without-findings-is-accepted` | `Validate_AcceptsAnalysisWithoutFindings` | unit | LogAnalyzer.UnitTests |
| `log-analysis/analysis-without-a-summary-is-rejected` | `Validate_RejectsMissingSummary` | unit | LogAnalyzer.UnitTests |
| `log-analysis/complete-finding-is-accepted` | `Validate_AcceptsCompleteFinding` | unit | LogAnalyzer.UnitTests |
| `log-analysis/line-numbers-start-at-1` | `Validate_TreatsLineNumbersAsOneBased` | unit | LogAnalyzer.UnitTests |
| `log-analysis/finding-with-a-missing-field-is-rejected` | `Validate_RejectsFindingWithMissingField` | unit | LogAnalyzer.UnitTests |
| `log-analysis/each-defined-severity-is-accepted` | `Validate_AcceptsEachDefinedSeverity` | unit | LogAnalyzer.UnitTests |
| `log-analysis/unknown-severity-is-rejected` | `Validate_RejectsUnknownSeverity` | unit | LogAnalyzer.UnitTests |
| `log-analysis/each-defined-category-is-accepted` | `Validate_AcceptsEachDefinedCategory` | unit | LogAnalyzer.UnitTests |
| `log-analysis/unknown-category-is-rejected` | `Validate_RejectsUnknownCategory` | unit | LogAnalyzer.UnitTests |
| `response-validation/truncated-response-is-rejected` | `Validate_RejectsTruncatedResponse` | unit | LogAnalyzer.UnitTests |
| `response-validation/refusal-is-rejected` | `Validate_RejectsRefusal` | unit | LogAnalyzer.UnitTests |
| `response-validation/fenced-json-is-rejected` | `Validate_RejectsFencedJson` | unit | LogAnalyzer.UnitTests |
| `response-validation/prose-around-json-is-rejected` | `Validate_RejectsProseAroundJson` | unit | LogAnalyzer.UnitTests |
| `response-validation/malformed-json-is-rejected` | `Validate_RejectsMalformedJson` | unit | LogAnalyzer.UnitTests |
| `response-validation/wrong-type-is-rejected` | `Validate_RejectsWrongType` | unit | LogAnalyzer.UnitTests |
| `response-validation/unknown-field-is-rejected` | `Validate_RejectsUnknownField` | unit | LogAnalyzer.UnitTests |
| `response-validation/closed-set-values-are-matched-case-insensitively` | `Validate_NormalizesClosedSetCasing` | unit | LogAnalyzer.UnitTests |
| `response-validation/citation-outside-the-log-is-rejected` | `Validate_RejectsCitationOutsideLog` | unit | LogAnalyzer.UnitTests |
| `response-validation/line-zero-is-rejected` | `Validate_RejectsLineZero` | unit | LogAnalyzer.UnitTests |
| `response-validation/paraphrased-evidence-is-rejected` | `Validate_RejectsParaphrasedEvidence` | unit | LogAnalyzer.UnitTests |
| `response-validation/evidence-from-an-uncited-line-is-rejected` | `Validate_RejectsEvidenceFromUncitedLine` | unit | LogAnalyzer.UnitTests |
| `response-validation/blank-suggested-action-is-rejected` | `Validate_RejectsBlankSuggestedAction` | unit | LogAnalyzer.UnitTests |
| `response-validation/overlong-title-is-rejected` | `Validate_RejectsOverlongTitle` | unit | LogAnalyzer.UnitTests |
| `response-validation/two-violations-are-both-reported` | `Validate_ReportsEveryViolation` | unit | LogAnalyzer.UnitTests |

## Fixtures

- `tests/LogAnalyzer.UnitTests/Fixtures/logs/orders-api-40.log`: 40 lines; line 1 reads "Unhandled exception. System.NullReferenceException", line 12 reads "fail: Orders.Api[0] Database connection pool exhausted".
- `tests/LogAnalyzer.UnitTests/Fixtures/logs/orders-api-120.log`: 120 lines, for the out-of-range citation.
- `tests/LogAnalyzer.UnitTests/Fixtures/responses/`: one JSON file per scenario, named after the scenario id.
- A test helper that builds a validation input from response text, stop reason and a fixture log.
