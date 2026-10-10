# Tasks

## 1. Contract types and schema check

- [x] 1.1 Write failing tests for every `log-analysis` scenario in test-plan.md, using the canned responses under `Fixtures/responses`; verify each one fails because the validator does not exist yet
- [x] 1.2 Add the analysis, finding, validation input and validation result types in `LogAnalyzer.Core` and embed `schemas/analysis-result.schema.json`; verify the solution builds and a test confirms the schema's required fields match the types
- [x] 1.3 Implement JSON parsing, closed-set normalization and the schema check; verify every `log-analysis` test passes
- [x] 1.4 Describe the contract fields and closed sets in `docs/output-contract.md`; verify every field in the schema appears in the document

## 2. Rejection rules

- [x] 2.1 Write failing tests for every `response-validation` scenario in test-plan.md; verify each one fails for the expected reason (the wrong-type, unknown-field and letter-case tests already pass, because the schema check and case-insensitive matching from 1.3 cover them)
- [x] 2.2 Implement the incomplete-response and single-document checks (`TRUNCATED`, `REFUSED`, `NOT_JSON`); verify their tests pass
- [x] 2.3 Implement the citation checks (`LINE_OUT_OF_RANGE`, `EVIDENCE_NOT_FOUND`) against the numbered log lines; verify their tests pass
- [x] 2.4 Implement the field limits (`BLANK_FIELD`, `FIELD_TOO_LONG`) and collect every violation with its code, path and message; verify all remaining tests pass
- [x] 2.5 List every reason code with its meaning in `docs/output-contract.md`; verify each code used in the tests appears in the document

## 3. Review fixes

- [x] 3.1 Add the scenario "Finding without cited lines is rejected", and write failing tests for what the review found: an unpaired surrogate (escaped, raw and in a field name), numbers whose fraction or size a decimal cannot hold, an undefined stop reason, the parser's reason in the NOT_JSON message, and the lowercase contract spelling; verify each fails for the expected reason (an exception, a wrong result, or the unimplemented `ToContractValue`)
- [x] 3.2 Decode every string while parsing and turn any parser failure into NOT_JSON with its reason, read whole numbers exactly from their text, make the stop-reason check exhaustive, count the context window as TRUNCATED, and add `ToContractValue()`; verify all unit tests pass
- [x] 3.3 Pin behavior the tests left loose: a valid analysis that is cut off or refused, the last line and one past it, blank evidence, explanation and summary, evidence in a different letter case, one violation of each kind in order, and a 1,000-character summary; verify that dropping `minItems`, an off-by-one line bound, case-insensitive evidence matching and a skipped evidence check each make a test fail
- [x] 3.4 Correct design.md, docs/output-contract.md, test-plan.md and the paraphrased-evidence scenario where the review found them imprecise, and record the stop-reason mapping for `add-claude-integration`; verify each statement against the code or a test

## Workflow follow-up

- Run `/opsx:verify add-analysis-contract`, then `/opsx:archive add-analysis-contract`.
