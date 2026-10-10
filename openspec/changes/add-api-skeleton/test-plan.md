# Test Plan

Every scenario in this change is deterministic, and none depends on model judgment. The scenario tests are integration tests: the API runs in memory against a PostgreSQL container from Testcontainers, with a scripted model in place of the stand-in. They need Docker, but no network, model or API key. Tests for runs live in `tests/LogAnalyzer.IntegrationTests/Runs/`.

| Scenario id | Test | Kind | Project |
|---|---|---|---|
| `log-ingestion/pasted-log-is-accepted` | `SubmissionTests.Submit_AcceptsPastedLog` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/uploaded-log-file-is-accepted` | `SubmissionTests.Submit_AcceptsUploadedFile` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/binary-file-is-rejected` | `SubmissionTests.Submit_RejectsBinaryFile` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/unsupported-character-set-is-rejected` | `SubmissionTests.Submit_RejectsUnsupportedCharset` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/line-breaks-do-not-add-lines` | `SubmissionTests.Submit_CountsCrlfLinesWithoutAddingOne` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/whitespace-only-paste-is-rejected` | `SubmissionTests.Submit_RejectsWhitespaceOnlyPaste` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/empty-file-is-rejected` | `SubmissionTests.Submit_RejectsEmptyFile` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/oversized-upload-is-rejected` | `SubmissionTests.Submit_RejectsOversizedUpload` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/upload-at-the-limit-is-accepted` | `SubmissionTests.Submit_AcceptsUploadAtTheLimit` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/level-counts-come-from-the-log` | `StatisticsTests.Statistics_CountLevelsFromTheLog` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/log-without-recognizable-levels-or-timestamps` | `StatisticsTests.Statistics_ReportNoLevelsAndNoTimeRange` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/time-range-spans-out-of-order-lines` | `StatisticsTests.Statistics_SpanOutOfOrderTimestamps` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/run-completes` | `RunLifecycleTests.Process_CompletesRun` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/failed-run-carries-its-reason` | `RunLifecycleTests.Process_FailedRunCarriesItsReason` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/model-failure-fails-the-run-with-its-reason` | `RunLifecycleTests.Process_FailsRunWhenModelIsUnavailable` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/interrupted-run-fails-after-a-restart` | `RunRecoveryTests.Restart_FailsRunLeftRunning` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/queued-run-is-processed-after-a-restart` | `RunRecoveryTests.Restart_ProcessesQueuedRun` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/completed-run-is-returned-with-its-findings` | `ReadRunTests.Read_ReturnsCompletedRunWithFindings` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/unknown-run-id` | `ReadRunTests.Read_AnswersUnknownIdWithNotFound` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/another-person-s-run-is-not-found` | `ReadRunTests.Read_AnswersAnotherPersonsRunWithNotFound` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/runs-are-listed-newest-first` | `ListRunsTests.List_ReturnsNewestFirst` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/runs-are-paged` | `ListRunsTests.List_ReturnsTwentyPerPageWithNextCursor` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/other-people-s-runs-are-not-listed` | `ListRunsTests.List_OmitsOtherPeoplesRuns` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/invalid-page-cursor-is-rejected` | `ListRunsTests.List_RejectsUnreadableCursor` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/rejected-then-accepted-attempts-are-both-recorded` | `AttemptTests.Process_RecordsRejectedThenAcceptedAttempts` | integration | LogAnalyzer.IntegrationTests |
| `response-validation/corrected-response-is-accepted` | `CorrectiveRetryTests.Process_AcceptsCorrectedResponse` | integration | LogAnalyzer.IntegrationTests |
| `response-validation/second-rejection-fails-the-run` | `CorrectiveRetryTests.Process_FailsRunAfterSecondRejection` | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/malformed-registration-body-is-rejected` | `RegistrationTests.Register_RejectsMalformedBody` (existing, unchanged) | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/empty-sign-in-body-is-rejected` | `SignInTests.SignIn_RejectsEmptyBody` (existing, unchanged) | integration | LogAnalyzer.IntegrationTests |
| `user-accounts/sign-in-body-in-an-unsupported-character-set-is-rejected` | `SignInTests.SignIn_RejectsUnsupportedCharset` | integration | LogAnalyzer.IntegrationTests |

## Fixtures

- `RunsApiFactory`, derived from `PostgresApiFactory`. It adds a `FakeTimeProvider` starting at 09:00 UTC, sets `Ingestion:MaxSubmissionBytes` to 1,048,576, registers `ScriptedAnalysisModel` in place of the stand-in, and removes the `RunWorker` hosted service. Its `ProcessQueuedRunsAsync` drains the queue through `RunProcessor`, so a test submits, processes and reads, with no sleeps.
- `ScriptedAnalysisModel`, in the test project. Each test gives it a queue of replies (response text and stop reason) or a `ModelCallException`, and it records every `AnalysisRequest` it receives. The corrective-retry tests read the second request's correction from it.
- `RunsClient`. It submits a log as raw bytes with a chosen Content-Type and optional `fileName`, and reads and lists runs. Two signed-in clients (`AccountsClient.RegisterAndSignInAsync`) cover the ownership scenarios.
- `RunsDatabase`, which uses plain SQL. It counts runs, reads a run's attempts in order, and sets a run to `running` for the recovery scenarios.
- Restarts: the recovery tests start a second API host with the real worker against the same PostgreSQL container, after the run rows are in place. They poll `GET /runs/{id}` until the run reaches a final state.
- Logs:
  - `levels-10.log`: 10 lines, 3 `fail:` lines and 2 `warn:` lines.
  - `no-levels.log`: no level markers and no timestamps.
  - A two-line log stamped 09:00:05Z, then 09:00:01Z.
  - A paste of three CRLF-terminated lines.
  - A small file containing a NUL byte.
  - Generated bodies of exactly 1,048,576 and 1,572,864 bytes.
  - `orders-api-40.log` from the contract tests, for findings that cite real lines.
- Model replies, as contract JSON built per test: a valid analysis with two findings, a reply citing a line past the end (LINE_OUT_OF_RANGE), and a reply whose evidence is not on its cited line (EVIDENCE_NOT_FOUND).
- Paging: 25 runs submitted while the clock advances one minute per run.

## Tests without a scenario

These pin rules from design.md that no scenario names. They carry no `Scenario` trait.

- Integration:
  - Registration with charset "bogus" answers 415 UNSUPPORTED_CHARSET and creates no account.
  - An oversized body sent without Content-Length gets 413.
  - `limit` outside 1–100 gets INVALID_LIMIT.
  - A malformed id gets the same 404 body as another person's run.
  - The `OpenApiDocumentTests` rows for the run routes.
  - `AccessPolicyTests` sends each endpoint the content type it declares.
  - `RunWorkerTests` keeps the real worker with the stand-in and polls until a run completes.
- Unit (LogAnalyzer.UnitTests, Core only):
  - Decoding and splitting: CRLF, CR, LF, a final line break, BOM, NUL, invalid UTF-8 and encoded surrogates.
  - A table of level formats and of timestamp formats, and the time-range rules (earliest and latest, the offset kind, `utc`).
  - File-name cleaning.
  - `AnalysisPipeline` with a fake model and recorder: attempt order and times, the correction's content, at most two calls, and the mapping of model exceptions and cancellation.
  - The stored rejection paths, with unknown fields masked as `*`.
  - `StandInAnalysisModel`'s replies passing the validator for every fixture log.
