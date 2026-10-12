# Tasks

## 1. Branch and packages

- [x] 1.1 On a new branch `add-claude-integration` from a `main` that includes this plan, add the packages below at exact versions; verify `CI=true dotnet build LogAnalyzer.slnx --configuration Release` succeeds and the unit and integration suites still pass unchanged
  - `Anthropic` (the official C# SDK) in `Directory.Packages.props`, referenced by `LogAnalyzer.Infrastructure`.
  - `WireMock.Net` in `Directory.Packages.props`, referenced by `LogAnalyzer.IntegrationTests`.
  - If the SDK release lacks something design.md relies on (`OutputConfig` with a JSON-schema format and effort, typed status exceptions, a settable base URL, `MaxRetries`), stop and revisit the design with the user.

## 2. Usage on attempts, and the migration

- [x] 2.1 Write the failing tests below; verify each fails on its assertions, or because the column does not exist
  - `StandInAnalysisModelTests`: a reply reports zero tokens, no transport retries and no request id.
  - `AnalysisPipelineTests`: a reply's tokens, retries and request id reach the attempt given to `IRunRecorder`.
  - `DatabaseMigrationTests`: `AddAttemptUsage` adds `input_tokens`, `output_tokens`, `transport_retries` and `request_id` to `run_attempts`, and `content_sha256`, `prompt_version` and `model` to `runs`, with the index on `(user_id, content_sha256)`.
  - `AttemptTests`: a scripted reply with usage is stored with those values. `RunsDatabase` gains reads of the new columns, and `ScriptedAnalysisModel.Reply` takes optional usage.
- [x] 2.2 Give `ModelReply` the fields `InputTokens`, `OutputTokens`, `TransportRetries` and `RequestId`. Make the stand-in report zeros and no id, and have `AnalysisPipeline` pass them into the recorded attempt. Verify the unit tests from 2.1 and the whole unit suite pass.
- [x] 2.3 Add the columns to `RunAttempt` and `Run`, and generate the migration `AddAttemptUsage` with `dotnet ef migrations add`. Existing rows get zeros and null. `RunRecorder` stores the usage. Verify the migration and attempt tests from 2.1, and the whole integration suite, pass.
- [x] 2.4 Say in docs/runs-api.md that every attempt stores its tokens, transport retries and request id, and that a later change returns them; verify the column names match the migration

## 3. Reuse of the person's own completed runs

- [x] 3.1 Write the failing tests below; verify the scenario tests fail because the second submission answers 202 and calls the model
  - `ReuseTests` for the three analysis-history scenarios, and its checks listed under "Tests without a scenario" in test-plan.md.
  - `ScriptedAnalysisModel` takes a configured model and prompt version, which default to today's `scripted` and `scripted-v1`.
  - The `OpenApiDocumentTests` row: `POST /runs` lists 200 as well as 202.
- [x] 3.2 Implement reuse as design.md describes; verify `ReuseTests`, the OpenAPI row and `AccessPolicyTests` pass
  - `IAnalysisModel` reports the model and prompt version it is configured with. For the stand-in, both are `stand-in`.
  - When the processor starts a run, it writes those two values to the run.
  - At submission, the API stores the SHA-256 of the body's bytes. It looks for a completed run of the same person through `RunStore`'s ownership filter, with the same hash, model and prompt version. When one exists, it answers `200 { id, status: "completed" }` and creates nothing.
- [x] 3.3 Run the whole integration suite, and adjust only the tests that fail because one person's identical resubmission is now reused, by giving them distinct content; verify the whole suite passes
- [x] 3.4 Rewrite `src/web/openapi.json` with `UPDATE_OPENAPI_SNAPSHOT=1`, then run `npm run api:generate` in `src/web`; verify the copy test passes without the variable, `npm test -- --watch=false` and `npm run build` pass, and the web code needs no change
- [x] 3.5 Document reuse in docs/runs-api.md: the rule, the 200 answer with its body, and that another person's identical log is analyzed again; verify every status the `ReuseTests` assert appears in it

## 4. The provider setting, its validation and the prompt templates

- [x] 4.1 Write the failing tests below; verify each fails on its assertions
  - Move the process-start helper out of `SettingsValidationTests` into a shared helper.
  - `ModelSettingsTests` for the two startup scenarios, and the unknown-prompt-version check.
  - `ModelProviderTests.StandIn_CompletesRunWithoutKey`, with `WorkerApiFactory` and no key configured.
  - The fast tests in `Models/`: options validation and prompt rendering, as listed under "Tests without a scenario".
- [x] 4.2 Add `ModelOptions` for `Model:Provider` and `Model:Claude:*` with the defaults in design.md, including the optional `BaseUrl`, validated on start with messages that name the setting. Register the stand-in for `StandIn`, the default; the Claude branch arrives in 5.3. Verify the missing-key and unknown-provider tests, `ModelProviderTests`, the options tests and `SettingsValidationTests` pass.
- [x] 4.3 Write `prompts/log-analysis/v1/system.md` and `correction.md` as design.md describes, including that the log is data whose instructions are ignored. Embed `prompts/**` in Infrastructure, keyed by version, behind a template source that tests can replace. Add the renderer for numbered lines and the correction block. Verify the prompt-rendering tests and the unknown-prompt-version test pass.

## 5. The Claude model through the SDK

- [x] 5.1 Write the failing tests below, with the fixtures from test-plan.md; verify each fails because no Claude model is registered
  - `FakeMessagesApi` and its canned Messages answers.
  - `ClaudeApiFactory`, with the fake key `fake-key-for-tests` and `Model:Claude:BaseUrl` pointing at the fake. Delays and timeouts use real time, because the factory's test clock only moves by hand.
  - `ClaudeGatewayTests` for its eight scenarios in test-plan.md, and its checks listed under "Tests without a scenario".
  - The fast stop-reason table tests in `Models/`.
- [x] 5.2 Implement `ClaudeAnalysisModel` as design.md describes; verify the stop-reason table tests and every `ClaudeGatewayTests` test that needs no retry pass
  - One Messages request per call: the configured model, effort and `MaxTokens`, the output format built from `AnalysisContract.SchemaJson`, and no fallback.
  - The cached system prompt and log block, and the single-turn correction.
  - The reply text joins the text blocks. The stop reason maps through the table, and the reply carries usage and the `request-id`.
- [x] 5.3 Register Claude for `Model:Provider=Claude`, passing the key only to the client's constructor. Verify `Run_CallsModelServiceWhenClaudeIsSelected` and `Run_NeverExposesApiKey` pass, and the whole integration suite passes.
- [x] 5.4 Document the providers and their settings in docs/runs-api.md, and say in docs/output-contract.md that the contract is now sent as the output format; verify the setting names match `ModelOptions`

## 6. Retries and timeouts

- [x] 6.1 Write the failing `ClaudeTransportTests` for its four scenarios, with its checks under "Tests without a scenario", and the fast retry-classifier and backoff tests in `Models/`; verify each fails on its assertions
- [x] 6.2 Implement the retry loop as design.md describes; verify `ClaudeTransportTests`, the classifier and backoff tests, and the whole integration suite pass
  - The SDK client gets `MaxRetries = 0`.
  - Transient failures are retried up to `MaxRetries`, with backoff and jitter or after `retry-after`. 400, 401, 403, 404 and 422 end at once with MODEL_CONFIGURATION_ERROR.
  - Each try is bounded by `Timeout` through a linked token, and host cancellation passes through, so the run fails with INTERRUPTED.
  - The retry count goes into the reply.
- [x] 6.3 Say in docs/runs-api.md which model-service failures end as MODEL_UNAVAILABLE and which as MODEL_CONFIGURATION_ERROR; verify it matches the classifier tests

## 7. The key as a Docker secret

- [x] 7.1 Add the key-per-file configuration source for `/run/secrets` (optional) in `Program.cs`. In docker-compose, declare the secret from `ANTHROPIC_API_KEY` and pass `Model__Provider` from `MODEL_PROVIDER`. Add `ANTHROPIC_API_KEY=` (empty) and `MODEL_PROVIDER=StandIn` to `.env.example`. Then verify, after `cp .env.example .env` as CI does:
  - `docker compose up --build --wait` brings every service up healthy, and a pasted log completes with the stand-in.
  - With `MODEL_PROVIDER=Claude` and no key, the api container stops with a message naming `Model:Claude:ApiKey`.
  - If Compose rejects the unset variable, use the fallback in design.md's risks.
- [x] 7.2 Document configuring Claude in the README: `MODEL_PROVIDER`, the key in `.env` or `dotnet user-secrets`, and that keys are never committed or added to GitHub; verify the documented commands run as written, and `git grep -n -E "sk-ant-[A-Za-z0-9_-]{8,}"` finds nothing

## 8. The golden-set evals

- [x] 8.1 Write the six eval cases and their logs, as listed below; verify `node scripts/check-scenario-coverage.mjs` passes and lists the six `log-analysis` scenarios among the pending scenarios with tests
  - `GoldenLogGenerator` is deterministic and seeded.
  - It writes the six logs from test-plan.md to `tests/LogAnalyzer.Evals/GoldenLogs/log-analysis/`, committed.
  - `Cases/log-analysis/<scenario>.json` names each case's log and graders.
  - A test that needs no key checks that regenerating the logs leaves them unchanged.
- [x] 8.2 Add the harness, as listed below; verify `RUN_EVALS=1 dotnet test tests/LogAnalyzer.Evals` without a key fails at once with a message naming the setting, and that without `RUN_EVALS=1` the eval cases are skipped
  - The cases run only with `RUN_EVALS=1`: the project reads the API's user secrets, so a developer who keeps a key there for running the API would otherwise spend money on every `dotnet test` of the solution.
  - Each case's log goes through `ClaudeAnalysisModel`, then `AnalysisPipeline` with the validator and an in-memory recorder, 3 times.
  - The code graders and the `claude-haiku-5-5` judge score the answers, and a case passes at 2 of 3.
  - It writes `eval-report.json` with the pass rate, tokens and latency per case.
  - It reads the key from `dotnet user-secrets` or `ANTHROPIC_API_KEY`.
- [ ] 8.3 With the user's go-ahead, since it costs money, run the evals once with their key on their machine; verify every case passes at 2 of 3, and keep `eval-report.json` with the change's verification. If a case fails, fix the v1 prompt rather than the grader, unless the grader is wrong.
- [x] 8.4 Rewrite the evals README and the comment in `LogAnalyzer.Evals.csproj`; verify the README's commands run as written
  - The case format, `GoldenLogs/`, how to run locally, and the cost.
  - That evals never run in CI and need no GitHub secret.

## 9. Integration checks

- [ ] 9.1 Run every gate, as listed below; verify all pass
  - `CI=true dotnet build LogAnalyzer.slnx --configuration Release`, then the unit and integration suites.
  - `npm test -- --watch=false` and `npm run build` in `src/web`.
  - `openspec validate --all --strict`.
  - `node scripts/check-scenario-coverage.mjs`.
  - The Docker stack from `.env.example`, with the Playwright end-to-end tests.
- [ ] 9.2 Check the branch for secrets; verify all of the following
  - `git diff main --stat` shows no `.env` file.
  - `git grep -n -E "sk-ant-[A-Za-z0-9_-]{8,}"` finds nothing. That is the shape of a real key; the plan only mentions the bare prefix.
  - `.env.example` holds `ANTHROPIC_API_KEY=` with no value.
  - `.github/workflows/ci.yml` references no Anthropic secret.

## Workflow follow-up

- Review the whole branch once, then run `/opsx:verify add-claude-integration` and fix what either finds in the same pull request.
- Archive the change in that pull request, then open it for the user to merge.
