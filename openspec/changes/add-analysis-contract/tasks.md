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

## Workflow follow-up

- Run `/opsx:verify add-analysis-contract`, then `/opsx:archive add-analysis-contract`.
