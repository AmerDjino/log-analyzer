# Test Plan

The six `log-analysis` scenarios depend on model judgment, so they are golden-set eval cases scored by pass rate. Every other scenario is deterministic and is proven by an integration test:

- **Integration** (xUnit, `tests/LogAnalyzer.IntegrationTests`): the API runs against a PostgreSQL container from Testcontainers.
  - Tests of the Claude provider point the SDK's base URL at a WireMock.Net server that plays the Messages API, so they need Docker but no network, model or API key.
  - Startup scenarios start the API as a process, as `SettingsValidationTests` does.
- **Eval** (`tests/LogAnalyzer.Evals`): each case runs 3 times against the configured Claude model, and passes at 2 of 3. Evals need an API key and run on demand, never in pull-request CI.

An xUnit test carries `[Trait("Scenario", "<id>")]`. An eval case's file path below `Cases/`, without its extension, is its scenario id.

| Scenario id | Test | Kind | Project |
|---|---|---|---|
| `analysis-history/identical-resubmission-reuses-the-run` | `ReuseTests.Submit_ReusesOwnCompletedRunForIdenticalContent` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/new-prompt-version-analyzes-again` | `ReuseTests.Submit_AnalyzesAgainWithNewPromptVersion` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/another-person-s-identical-log-is-analyzed-again` | `ReuseTests.Submit_AnalyzesAnotherPersonsIdenticalLogAgain` | integration | LogAnalyzer.IntegrationTests |
| `log-analysis/planted-exception-burst-is-reported` | `Cases/log-analysis/planted-exception-burst-is-reported.json` | eval | LogAnalyzer.Evals |
| `log-analysis/connection-pool-exhaustion-is-reported` | `Cases/log-analysis/connection-pool-exhaustion-is-reported.json` | eval | LogAnalyzer.Evals |
| `log-analysis/process-crash-is-critical` | `Cases/log-analysis/process-crash-is-critical.json` | eval | LogAnalyzer.Evals |
| `log-analysis/slow-queries-without-failures-are-a-warning` | `Cases/log-analysis/slow-queries-without-failures-are-a-warning.json` | eval | LogAnalyzer.Evals |
| `log-analysis/repeated-timeout-becomes-one-finding` | `Cases/log-analysis/repeated-timeout-becomes-one-finding.json` | eval | LogAnalyzer.Evals |
| `log-analysis/clean-log-has-no-findings` | `Cases/log-analysis/clean-log-has-no-findings.json` | eval | LogAnalyzer.Evals |
| `model-gateway/missing-key-stops-startup` | `ModelSettingsTests.Startup_FailsWithoutKeyWhenClaudeIsSelected` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/key-never-leaks` | `ClaudeGatewayTests.Run_NeverExposesApiKey` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/stand-in-runs-without-a-key` | `ModelProviderTests.StandIn_CompletesRunWithoutKey` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/claude-is-used-when-selected` | `ClaudeGatewayTests.Run_CallsModelServiceWhenClaudeIsSelected` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/unknown-provider-stops-startup` | `ModelSettingsTests.Startup_FailsForUnknownProvider` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/output-limit-is-rejected-as-truncated` | `ClaudeGatewayTests.Run_RejectsMaxTokensAsTruncated` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/refusal-is-rejected-as-refused` | `ClaudeGatewayTests.Run_RejectsRefusalAsRefused` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/unexpected-stop-reason-is-never-accepted` | `ClaudeGatewayTests.Run_RejectsUnknownStopReasonAsTruncated` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/slow-call-is-abandoned` | `ClaudeTransportTests.Call_IsAbandonedAfterTimeout` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/rate-limit-then-success` | `ClaudeTransportTests.Call_RetriesRateLimitThenSucceeds` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/retries-exhausted` | `ClaudeTransportTests.Call_FailsWithModelUnavailableAfterRetries` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/authentication-error-is-not-retried` | `ClaudeTransportTests.Call_DoesNotRetryAuthenticationError` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/configured-version-is-used-and-recorded` | `ClaudeGatewayTests.Run_RecordsConfiguredPromptVersion` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/past-runs-keep-their-version` | `ClaudeGatewayTests.Runs_KeepTheirPromptVersionAfterConfigurationChange` | integration | LogAnalyzer.IntegrationTests |
| `model-gateway/usage-is-recorded` | `ClaudeGatewayTests.Attempt_RecordsUsage` | integration | LogAnalyzer.IntegrationTests |

## Fixtures

- **Integration:**
  - `WireMock.Net` is added to `Directory.Packages.props` and the integration test project.
  - `FakeMessagesApi` wraps a WireMock server and offers canned Messages API answers:
    - a valid analysis of the log the test submitted, as structured-output JSON;
    - answers with the stop reasons `end_turn`, `max_tokens`, `refusal` (with `stop_details`) and `pause_turn`;
    - usage of 4,210 input and 610 output tokens, and a `request-id` header;
    - 429 with `retry-after: 1`, 529 overloaded, 401, and a 6-second delay.

    It also records each request it receives: headers, model, `output_config` and the message text.
  - `ClaudeApiFactory` derives from `RunsApiFactory`:
    - It sets `Model:Provider=Claude`, a fixed fake API key (`fake-key-for-tests`, never shaped like a real key) and `Model:Claude:BaseUrl` to the fake's address.
    - It sets `Model:Claude:Timeout` to 5 s and `Model:Claude:MaxRetries` to 2.
    - It replaces `ScriptedAnalysisModel` with the real `ClaudeAnalysisModel`.
    - It adds a log sink that captures every message the API writes, for the key search.
    - It registers a test template source with `log-analysis/v1` and `log-analysis/v2`, so tests can switch prompt versions without shipping a v2 prompt.
  - `ReuseTests` use `RunsApiFactory` with `ScriptedAnalysisModel`. The model gains a configurable prompt version and model (`log-analysis/v1`, `claude-test`), which reuse compares, and a count of the calls it received.
  - `ModelSettingsTests` reuse the process-start helper from `SettingsValidationTests`. It is moved into a shared helper. The tests set `Model__Provider` and `Model__Claude__ApiKey` as environment variables.
  - `ModelProviderTests` use `WorkerApiFactory`, which keeps the stand-in and the real worker, with no key configured.
  - `Runs_KeepTheirPromptVersionAfterConfigurationChange` starts a second host, configured with `log-analysis/v2`, against the same database, as the restart tests do.
  - `RunsDatabase` gains reads of the new attempt columns and of a run's `content_sha256`, `prompt_version` and `model`, plus a search of every text column for a value.
- **Eval:**
  - The project gains references to Infrastructure and the Anthropic SDK, and a harness that:
    - analyzes each case's log through `ClaudeAnalysisModel`, then `AnalysisPipeline` with the validator;
    - runs each case 3 times;
    - writes `eval-report.json` with the pass rate, tokens and latency per case.
  - Logs come from `GoldenLogGenerator`, which is deterministic and seeded. Its output is committed under `GoldenLogs/log-analysis/`, outside `Cases/`, because the coverage check reads every file below `Cases/` as a scenario id. The logs are:
    - 40 NullReferenceException entries between lines 200 and 260 of a 600-line log;
    - pool timeouts from line 4,120, then 503 responses, in a 5,000-line log;
    - an unhandled exception followed by a shutdown at the end;
    - repeated slow-query warnings and no failures;
    - 200 identical timeouts calling `inventory-service`;
    - startup messages, health checks and successful requests only.
  - Case files name their log, their graders (category, severity, cited line range, finding count, empty findings) and, for the clean log, a summary judge: `claude-haiku-5-5` answering yes or no through structured output.

## Tests without a scenario

These pin rules from design.md that no scenario names. None carries a scenario id of this change.

- **Integration (fast, no container; in `tests/LogAnalyzer.IntegrationTests/Models/`):**
  - The stop-reason table: `end_turn`, `max_tokens`, `model_context_window_exceeded`, `refusal`, and unknown values such as `stop_sequence`.
  - The retry classifier:
    - 429, 529, 500, 503, a timeout and a connection failure are transient;
    - 400, 401, 403, 404 and 422 are not;
    - backoff grows from 1 s to 2 s with jitter, is capped at 30 s, and honors `retry-after`.
  - Prompt rendering:
    - lines are numbered `N: text`;
    - the log block is identical in the first and the corrective request;
    - the corrective block quotes the rejected text and each violation's code, path and message.
  - Options validation: Claude needs a key; the stand-in does not; an unknown prompt version is refused.
- **Integration (with containers):**
  - `ClaudeGatewayTests`:
    - the request carries the structured output format built from the contract, effort from settings, the configured model and `max_tokens`;
    - the corrective request is a single user turn with no assistant turn;
    - the request asks for no fallback model, and effort is `medium` when the setting is not given;
    - shutdown during a call fails the run with INTERRUPTED.
  - `ClaudeTransportTests`:
    - a 400 fails at once with MODEL_CONFIGURATION_ERROR;
    - a connection failure is retried;
    - `retry-after` is honored;
    - an attempt records its transport retries.
  - `ModelSettingsTests`: an unknown prompt version stops startup with a message naming `Model:Claude:PromptVersion`.
  - `ReuseTests`:
    - a queued or failed run is never reused;
    - a reused answer is `200 { id, status: "completed" }`;
    - a different model is analyzed again;
    - the file name is ignored.
  - `OpenApiDocumentTests`: `POST /runs` lists 200 as well as 202. The committed copy `src/web/openapi.json` and the generated types are updated.
  - `DatabaseMigrationTests`: `AddAttemptUsage` adds the attempt columns, the run columns and the `(user_id, content_sha256)` index.
- **Unit (`LogAnalyzer.UnitTests`, Core only):**
  - The stand-in reports zero tokens, no retries and no request id.
  - `AnalysisPipeline` passes a reply's usage into the recorded attempt.

## Checks in CI

- **Backend job:** every integration test above runs, the WireMock ones included. No test reaches the real Anthropic API, and the workflow gets no Anthropic key or other secret.
- **Frontend job:** the generated types match the updated OpenAPI copy.
- **Specs job:** the coverage check reads the six eval case files and the 18 integration tags. After archiving, a missing case file fails the check as MISSING, even though evals do not run in CI.
- **Evals:** not in CI. They run locally with `dotnet test tests/LogAnalyzer.Evals`, reading the key from `dotnet user-secrets` or the `ANTHROPIC_API_KEY` environment variable, never from a committed file. Their report is kept with the change's verification.
