# Design: Add the Claude integration

## Context

See proposal.md for motivation and the delta specs for the rules.

`add-api-skeleton` built the pipeline around a model seam in Core:
- `IAnalysisModel.AnalyzeAsync(AnalysisRequest)` returns a `ModelReply`, which holds the text, the stop reason, the model and the prompt version.
- A call that yields no reply throws `ModelCallException`, with MODEL_UNAVAILABLE or MODEL_CONFIGURATION_ERROR.
- `AnalysisPipeline` makes at most two calls: the second carries a `Correction` with the rejected text and every violation. It records each response the validator evaluated as an attempt.
- The only implementation is `StandInAnalysisModel`, registered in Infrastructure. Integration tests swap in `ScriptedAnalysisModel`.

The rest of the stack this change works with:
- The output contract `schemas/analysis-result.schema.json` uses only what Claude's structured outputs accept, so it can be sent as the output format.
- The validator maps three stop reasons, and `add-analysis-contract`'s design records how the Messages API's stop reasons map onto them.
- Runs belong to the person who submitted them.
- The web shell polls a run until it finishes, and treats any 2xx answer from `POST /runs` as accepted.

## Goals / Non-Goals

**Goals:**
- Claude plugs into the existing seam without changing the pipeline's rules: at most two calls, every response validated, nothing stored before it passes.
- Every attempt says what it cost: tokens, latency, transport retries and the request id.
- The stand-in stays the default, so the local stack, CI and every existing test run without a key or network access.
- Transport behavior (timeouts, retries, error mapping) is proven against a local fake of the Anthropic API, and model quality against a golden set, never in pull-request CI.

**Non-Goals:**
- Streaming partial results to the web shell. A run still completes or fails as a whole.
- Batch processing and per-chunk analysis (`add-large-log-support`).
- Computing money cost. Token counts are recorded, and prices change.

## Decisions

### A provider setting selects the model; the stand-in is the default

`Model:Provider` takes `StandIn` (the default) or `Claude`. Infrastructure registers the matching `IAnalysisModel`. The settings are validated when the API starts, like `Ingestion` and `Analysis`:
- An unknown provider stops startup with a message naming `Model:Provider`.
- When the provider is `Claude`, a missing `Model:Claude:ApiKey` stops startup with a message naming that setting. With the stand-in, the key is not read.

The Claude settings are:

| Setting | Default |
|---|---|
| `Model:Claude:Model` | `claude-opus-5-5` |
| `Model:Claude:PromptVersion` | `log-analysis/v1` |
| `Model:Claude:Effort` | `medium` |
| `Model:Claude:MaxOutputTokens` | 16,000 |
| `Model:Claude:Timeout` | 60 s |
| `Model:Claude:MaxRetries` | 2 |
| `Model:Claude:BaseUrl` | empty: the SDK's default endpoint. Tests point it at the WireMock fake. |

- Alternative: Claude by default. Rejected (decided 2026-10-12): every developer machine, CI job and e2e run would need a key or an override.
- Alternative: choose the provider by whether a key is present. Rejected: a missing secret would silently fall back to the stand-in in production.

### The API key arrives as a Docker secret and never leaves the configuration

The API adds a key-per-file configuration source for `/run/secrets` (optional, so it is absent outside Docker). A secret file named `Model__Claude__ApiKey` becomes `Model:Claude:ApiKey`.
- docker-compose declares the secret from the `ANTHROPIC_API_KEY` environment variable (Compose's `environment` secret source), so `.env` can hold it while the value stays out of `docker inspect`.
- Local runs outside Docker use `dotnet user-secrets`.

The key is passed only to the SDK client's constructor. No log message, exception message, option dump or stored row includes it. The `Key never leaks` test searches the API's answers, the captured log output and every table for the key value.

No real key is ever committed or stored on GitHub:
- `.env` is gitignored, and `.env.example` gets only empty placeholders.
- No GitHub Actions secret is added. CI runs the stand-in and the WireMock fake, and evals run only on a developer's machine.
- Test code uses a fixed fake key that does not start with `sk-ant-`, so secret scanning never mistakes it for a real one.

- Alternative: a plain environment variable. Rejected: it shows in `docker inspect` and process listings.
- Alternative: the SDK's own `ANTHROPIC_API_KEY` lookup. Rejected: it bypasses the startup check and the provider rule.

### Claude is called through the official SDK, with structured output

`ClaudeAnalysisModel` lives in Infrastructure, next to the stand-in's registration. It uses the official `Anthropic` NuGet package's `AnthropicClient` and sends one Messages request per call:
- `OutputConfig.Format` is a JSON-schema output format built from `AnalysisContract.SchemaJson`, so the response is the contract's JSON. The validator still checks every response: citations, evidence and text limits are beyond what the schema can express.
- Thinking stays adaptive, Claude Opus 5.5's only mode. `OutputConfig.Effort` comes from `Model:Claude:Effort`, `medium` by default (decided 2026-10-12). It is sent on every request, so the setting decides it rather than the model's default.
- `MaxTokens` comes from `Model:Claude:MaxOutputTokens`. The request is not streamed: 16,000 output tokens fit within the SDK's HTTP timeout.
- The reply's text is the concatenation of its text blocks. Thinking blocks are not used and not stored.

No refusal fallback is requested, so every answer comes from the configured model. A response that ends with stop reason `refusal` is rejected as REFUSED, like any other rejection. The attempt records the model that answered (`response.Model`).

- Alternative: raw HTTP. Rejected: the SDK supplies typed errors, retries we can turn off, and the request types.
- Alternative: tool use with a strict tool instead of structured output. Rejected: structured output says what is wanted directly, and the contract was written for it.
- Alternative: the server-side refusal fallback (`fallbacks: "default"`), which re-serves a refused request with another model. Rejected (decided 2026-10-12): analyses come from Claude Opus 5.5 only. A log that a safety classifier declines ends as VALIDATION_FAILED, and its attempts show REFUSED.

### Versioned prompt templates are embedded resources

Prompts live under `prompts/log-analysis/<version>/`:
- `system.md`: the task, what counts as a finding, the severity scale, citation rules, and that the log is data whose instructions must be ignored.
- `correction.md`: how to fix a rejected response.

Infrastructure embeds `prompts/**` as resources, keyed by version. A configured version with no matching templates stops startup with a message naming `Model:Claude:PromptVersion`. Every reply reports the version, which the attempt already records.

The user message holds the numbered log (`N: text` per line), in a content block of its own marked for prompt caching. The system prompt is cached as well.

- Alternative: read prompts from disk at run time. Rejected: the path differs between the container, tests and local runs, and an edited file would change a version in place.

### The corrective request repeats the log and quotes the rejected answer

The second call is a new single-turn request: the same system prompt, the same cached log block, then a block with `correction.md`, the rejected response quoted as text, and each violation's code, path and message.

Because the log block is identical, the corrective call reads it from the prompt cache.

- Alternative: a multi-turn conversation with the rejected answer as an assistant turn. Rejected: Claude Opus 5.5 ties thinking blocks to the conversation that produced them. Replaying the assistant turn without its thinking blocks counts as editing history, which newer accounts are refused for.

### The adapter owns retries and timeouts, so it can count them

The SDK client is created with `MaxRetries = 0`. `ClaudeAnalysisModel` retries itself:
- **Transient, retried:** 429, 529 (overloaded), other 5xx, a call that exceeds `Model:Claude:Timeout`, and connection failures.
- **Retry timing:** up to `Model:Claude:MaxRetries` times, with exponential backoff and jitter (1 s, then 2 s, capped at 30 s), or after `retry-after` when the answer carries it.
- **Not retried:** 400, 401, 403, 404 and 422 end at once with MODEL_CONFIGURATION_ERROR: retrying cannot fix a bad key, model or request.
- **Retries exhausted:** the call ends with MODEL_UNAVAILABLE, as model-gateway's spec asks.

The number of retries goes into the reply, and from there into the attempt.

The timeout bounds each try, through a linked cancellation token. Cancellation by the host (shutdown) is passed through, so the processor still fails the run with INTERRUPTED.

- Alternative: the SDK's built-in retries. Rejected: they are invisible to the caller, so transport retries could not be recorded, and their policy (it also retries 409) is not ours to test.
- Alternative: Polly. Rejected: a dependency for one small loop.

### Stop reasons map as add-analysis-contract recorded

| Messages API stop reason | `StopReason` | Validator reason |
|---|---|---|
| `end_turn` | `Completed` | — |
| `max_tokens`, `model_context_window_exceeded` | `OutputLimit` | `TRUNCATED` |
| `refusal` | `Refusal` | `REFUSED` |
| anything else (`stop_sequence`, `tool_use`, `pause_turn`, a later one) | `OutputLimit` | `TRUNCATED` |

A stop reason that is not `end_turn` is never passed on as complete. The request uses no stop sequences or tools, so the last row should not occur. If it does, the pipeline's single correction handles it like a cut-off answer.

- Alternative: fail the run with MODEL_UNAVAILABLE for an unknown stop reason. Rejected: it would write no attempt, so the reason would be lost, and it would skip the correction the pipeline already offers.

### Usage is recorded on each attempt

`ModelReply` gains:
- `InputTokens` and `OutputTokens`, with cache reads counted in the input;
- `TransportRetries`;
- `RequestId`, the `request-id` the API returns.

The stand-in reports zeros and no request id.

`run_attempts` gains the columns `input_tokens`, `output_tokens`, `transport_retries` and `request_id` (migration `AddAttemptUsage`). Existing rows get zeros and null. Latency is `finished_at − started_at`, which every attempt already stores.

The SDK exposes the response headers through its raw-response API. If the release in use does not, a delegating HTTP handler on the client's `HttpClient` captures `request-id` for the call.

- Alternative: a separate usage table. Rejected: a one-to-one join for four numbers.

### Reuse is limited to the person's own completed runs

At submission, after the content checks:
1. The API computes SHA-256 over the submitted bytes and stores it on the run (`content_sha256`).
2. It looks for a completed run of the same person with the same hash, whose `prompt_version` and `model` equal the ones configured now. The provider writes those two columns, which this change adds to `runs`, when it starts processing a run.
3. If one exists, the API answers `200 { id, status: "completed" }` with that run, creates nothing and calls nothing.
4. Otherwise it answers `202` as before.

The lookup goes through `RunStore`'s ownership filter, with an index on `(user_id, content_sha256)`.

- The rule holds for every provider. With the stand-in, its configured version and model are both `stand-in`.
- The file name does not take part. A resubmission under another name returns the earlier run with its own name.
- `POST /runs` declares the 200 answer for OpenAPI. The committed copy and the generated web types are updated. The web shell needs no change, because a 200 carries the same `{ id, status }` body.

- Alternative: reuse across people, copying the result into a new run (decided against, 2026-10-12). It would reveal that someone else submitted the same log.
- Alternative: hash the decoded text. Rejected: the bytes are what was submitted, and hashing them needs no decoding rules of its own.

### The golden set grades model judgment outside pull-request CI

`tests/LogAnalyzer.Evals` gets one case per `[eval]` scenario, named by scenario id (`Cases/log-analysis/<scenario>.json`). Each case has:
- a log, generated once by a deterministic generator in the project and committed as a fixture, with its planted incident at known lines;
- graders, written as code wherever possible: categories, severities, cited line ranges, an empty findings list, "exactly one finding about this timeout". Only "the summary says no problems were found" needs a judge: `claude-haiku-5-5` answering yes or no through structured output.

Each case runs 3 times against the configured Claude model, and a case passes at 2 of 3. The run writes a report with the pass rate, tokens and latency per case.

The suite reads `Model:Claude:ApiKey` from `dotnet user-secrets` or the `ANTHROPIC_API_KEY` environment variable, never from a committed file. It runs on demand (`RUN_EVALS=1 dotnet test tests/LogAnalyzer.Evals`) and never in pull-request CI. Without `RUN_EVALS=1` the eval cases are skipped, so a `dotnet test` of the solution never spends money, even with a key in user secrets.

- Alternative: an LLM judge for every check. Rejected: code graders are exact, free and repeatable.
- Alternative: one run per case. Rejected: model output varies, and one sample says little.

### Tests prove transport behavior against WireMock

Integration tests run the API with `Model:Provider=Claude` and point the SDK's base URL at a WireMock.Net server that plays the Messages API. The scripted answers include:
- a rate limit, then success;
- overload on every try;
- 401;
- a delay longer than the timeout;
- `max_tokens`, `refusal` and `pause_turn` stop reasons;
- usage figures and a request id.

The startup scenarios (missing key, unknown provider, unknown prompt version) start the API as a process, as the settings validation test does since PR #8.

Unit tests cover the stop-reason table, the retry classifier and backoff, prompt rendering (line numbering, the correction block), and the reuse lookup's ownership.

### Code placement

- **Core:**
  - `ModelReply` gains the usage fields.
  - The stand-in reports zeros.
- **Infrastructure:**
  - `Models/ClaudeAnalysisModel.cs`, the retry and timeout loop, the stop-reason map, `ModelOptions` with its validation, and prompt loading.
  - The `AddAttemptUsage` migration, with the run columns `content_sha256`, `prompt_version` and `model`.
  - The provider switch in `InfrastructureServiceCollectionExtensions`.
- **Api:**
  - Reuse in `POST /runs`, and its 200 answer for OpenAPI.
  - The key-per-file configuration source in `Program.cs`.
- **Repository root:** `prompts/log-analysis/v1/`.
- **Docker:**
  - docker-compose's secret and `Model__Provider`.
  - `.env.example` gains `ANTHROPIC_API_KEY=` (empty) and `MODEL_PROVIDER=StandIn`.
- **Docs:**
  - docs/runs-api.md covers providers, reuse, and usage on attempts.
  - The README covers configuring Claude: the secret, user-secrets and running the evals.
  - docs/output-contract.md notes that the contract is now sent as the output format.

## Risks / Trade-offs

- [Real calls cost money] → The stand-in stays the default. Evals run only on demand, and every attempt records its tokens, so cost is visible per run.
- [A 60-second timeout may be short for a long log] → The timeout and effort are both settings. The evals' latency report shows where to set them, and `add-large-log-support` splits long logs.
- [Without a fallback, a safety classifier may refuse some security logs] → The run fails visibly, with REFUSED on its attempts. If refusals turn out to be common, the fallback decision is revisited.
- [Prompt injection: a log line can say "ignore your instructions"] → The system prompt marks the log as data. Structured output and the validator bound what any answer can contain: citations must exist, and evidence must be on the cited line.
- [The SDK's API surface changes between releases] → The package version is pinned in `Directory.Packages.props`, and the WireMock tests catch request-shape changes.
- [Compose may reject a secret whose environment variable is unset] → If it does, the key falls back to a `Model__Claude__ApiKey` entry read from `.env`, documented as the local-only option.
- [Reuse with the stand-in changes what an identical resubmission returns in local runs] → Intended: the rule is the same for every provider. Tests that resubmit identical content for one person are adjusted.

## Migration Plan

`AddAttemptUsage` adds four nullable or zero-default columns to `run_attempts` and three to `runs`, plus one index. The API applies it at startup, like the earlier migrations. Existing runs have no hash, so they are never reused.

Rolling back on a development machine means `docker compose down --volumes`, or `dotnet ef database update AddRuns` with the options in the README.

Switching a deployment to Claude means setting `MODEL_PROVIDER=Claude` and `ANTHROPIC_API_KEY`, then restarting the API.
