# Tasks

## 1. Ingestion and statistics rules in Core

- [x] 1.1 On a branch from a main that includes `add-analysis-contract` (PR #4), write the failing unit tests below; verify `ResponseValidator` builds and each new test fails because the Core types do not exist
  - Decoding and splitting: CRLF, CR, LF, a final line break, BOM, NUL, invalid UTF-8, encoded surrogates and white space only.
  - File-name cleaning.
  - The level-format and timestamp-format tables, and the time-range rules from design.md.
- [x] 1.2 Implement `LogText` (the byte checks in design.md's order, and line splitting) and file-name cleaning in `LogAnalyzer.Core/Ingestion`; verify their unit tests pass
- [x] 1.3 Implement `LogStatistics` with the level and timestamp detectors and the time-range rules; verify the format tables and time-range tests pass and `CoreDependencyTests` still passes

## 2. Submission and storage

- [x] 2.1 Write the failing tests below, with `RunsApiFactory` (FakeTimeProvider, a 1 MiB limit, the worker removed), `RunsClient` and `RunsDatabase`; verify each fails because `POST /runs` and the charset check do not exist
  - The nine submission scenarios in `SubmissionTests`, and `SignInTests.SignIn_RejectsUnsupportedCharset`, from test-plan.md.
  - The registration 415 test, and the oversized-body test without Content-Length.
- [x] 2.2 Add the `Run`, `RunLog`, `RunFinding` and `RunAttempt` entities, with the CHECK constraints and indexes from design.md, and generate the `AddRuns` migration; verify `DatabaseMigrationTests` finds the four new tables
- [x] 2.3 Add `POST /runs` as listed below; verify the submission tests pass
  - The size check, from Content-Length and while reading.
  - The content checks.
  - Statistics computed at submission.
  - The run and its lines stored in one transaction, answering 202 `{ id, status }`.
  - `IngestionOptions` validated at startup.
- [x] 2.4 Add the UTF-8 charset middleware after authorization, for every endpoint with `IAcceptsMetadata`, and change `AccessPolicyTests` to send each endpoint its declared content type; verify the charset tests for `POST /runs`, registration and sign-in pass, and `AccessPolicyTests` passes
- [x] 2.5 Document and describe the submission endpoint as listed below; verify every code the group's tests assert appears in the docs
  - Declare `POST /runs`' answers for OpenAPI, with its `OpenApiDocumentTests` row.
  - Create docs/runs-api.md, covering submission, the size limit, line splitting and the level and timestamp rules.
  - Add UNSUPPORTED_CHARSET to docs/accounts-api.md, and a run-endpoints row to the README.

## 3. Reading and listing

- [x] 3.1 Write the failing tests below, arranging completed runs with findings through `RunsDatabase`; verify each fails because the read routes do not exist
  - `ReadRunTests`, `ListRunsTests` and `StatisticsTests.Statistics_SpanOutOfOrderTimestamps`, for their 8 scenarios.
  - The malformed-id test and the INVALID_LIMIT test.
- [x] 3.2 Add `RunStore`'s owned queries and `GET /runs/{id}`, returning the run resource from design.md and RUN_NOT_FOUND for malformed, unknown and other people's ids; verify `ReadRunTests` and the time-range test pass
- [x] 3.3 Add `GET /runs`, with `limit`, the opaque keyset cursor, INVALID_LIMIT and INVALID_CURSOR; verify `ListRunsTests` pass
- [x] 3.4 Declare the read routes' answers for OpenAPI, with their `OpenApiDocumentTests` rows, and document reading and listing in docs/runs-api.md; verify every code the group's tests assert appears in it

## 4. Processing and the model seam

- [x] 4.1 Write the failing tests below, using `ScriptedAnalysisModel` and `RunsApiFactory.ProcessQueuedRunsAsync`; verify each fails because processing does not exist
  - `RunLifecycleTests`, `AttemptTests`, `CorrectiveRetryTests`, and the two "when the run completes" scenarios in `StatisticsTests`: 8 scenarios.
  - Unit tests for `AnalysisPipeline`, the stored rejection paths and the stand-in.
- [x] 4.2 Add the model seam (`IAnalysisModel`, `AnalysisRequest`, `ModelReply`, `ModelCallException`), `FailureReasons` and `StandInAnalysisModel` in Core; verify the stand-in's unit tests pass, with its replies passing the validator for every fixture log
- [x] 4.3 Add `AnalysisPipeline` and `IRunRecorder` in Core as listed below; verify the pipeline's unit tests pass
  - At most two model calls, including the correction.
  - Attempts recorded as they happen.
  - Findings stored only on acceptance.
  - Exceptions mapped to failure reasons.
- [x] 4.4 Add `RunProcessor` and the `IRunRecorder` implementation (attempts with masked paths, and the completing transaction) in Infrastructure; verify `RunLifecycleTests`, `AttemptTests`, `CorrectiveRetryTests` and the two statistics scenarios pass
- [x] 4.5 Add `RunWorker` as listed below, and register the stand-in; verify `RunWorkerTests` completes a run with the real worker and `HealthEndpointWithoutDatabaseTests` still passes
  - It waits for migrations.
  - It claims runs with SKIP LOCKED.
  - It wakes on the post-commit signal or a poll.
  - It runs `Analysis:MaxConcurrentRuns` loops.
- [x] 4.6 Document processing, the stand-in and the failure reasons in docs/runs-api.md; verify every reason in `FailureReasons` appears in it

## 5. Recovery after a restart

- [x] 5.1 Write the failing `RunRecoveryTests` for their 2 scenarios, starting a second API host with the real worker against the same database; verify the interrupted-run test fails because nothing sweeps runs left running
- [x] 5.2 When the worker starts, fail runs left running with INTERRUPTED, and at a graceful shutdown mark the in-flight run INTERRUPTED; verify `RunRecoveryTests` pass
- [x] 5.3 Document the restart behavior in docs/runs-api.md; verify it matches the tests

## 6. Integration checks

- [ ] 6.1 Start the stack on a fresh database, submit `orders-api-40.log` through nginx and read the run until it completes; verify the stand-in's findings cite real lines and `/api/health` stays Healthy
  - Reset with `docker compose down --volumes`, then `docker compose up --build --wait`.
  - Sign in before submitting.
- [ ] 6.2 Run the full test suite, `node scripts/check-scenario-coverage.mjs` and `openspec validate add-api-skeleton --strict`; verify all pass and the coverage report shows the 30 scenarios of this change as already having tests

## Workflow follow-up

- Run `/opsx:verify add-api-skeleton`, fix what it finds, and run `/opsx:archive add-api-skeleton` in the same pull request, after `add-analysis-contract` is archived.
- Merge the pull request once all four CI jobs pass.
- Revise the follow-ups listed in proposal.md with `/opsx:update`:
  - `add-claude-integration`: reuse of the submitter's own runs only, the model-provider setting and the stop-reason mapping.
  - `add-web-shell`: Blob uploads and the configured size limit.
  - `add-attempt-timeline-and-insights`: owner-only attempts and the scope of the insights.
