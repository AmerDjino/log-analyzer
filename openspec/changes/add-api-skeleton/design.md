# Design: Add the API skeleton

## Context

See proposal.md for motivation and the delta specs for the rules.

The API has the health check and the account endpoints from `add-user-accounts`:
- Every endpoint needs a session unless it is marked public.
- Errors are problem details with a `code` from an `ApiError` table.
- EF Core with the snake_case convention holds the account tables, and `DatabaseMigrator` applies migrations at startup.

`add-analysis-contract` (PR #4) adds the output contract and `ResponseValidator` to `LogAnalyzer.Core`. `Validate` takes the response text, the stop reason and the numbered log lines, and returns an accepted `Analysis` or every violation. This change builds on PR #4: it is implemented after PR #4 merges, and its response-validation delta adds to the main spec that PR #4 creates.

Two of the project's non-negotiables shape the design:
- Statistics are computed by code, never by the model.
- No model output is stored or shown before it passes validation.

## Goals / Non-Goals

**Goals:**
- The whole pipeline, from submission to a stored analysis, runs and is testable against a stand-in model.
- After an API restart, every run is in a defined state. Tests drive processing step by step, with no sleeps.
- Later changes (web shell, Claude, large logs, input safety, timeline) extend the tables, the run resource and the model seam without reworking them.

**Non-Goals:**
- More than one API instance per database (see Risks).
- Resuming an interrupted run, and a run-level deadline. `add-claude-integration` bounds every model call.
- An endpoint for attempts (`add-attempt-timeline-and-insights`).

## Decisions

### One submission endpoint that takes the log as the request body

`POST /runs` takes the log's bytes as the body:
- Content-Type is `text/plain`, with no charset or `charset=utf-8`.
- An optional `fileName` query parameter names an upload. A paste sends no file name and is stored as null.
- The web shell sends a paste as text and an upload as the chosen file's bytes, as a Blob, never `file.text()`.
- The file name keeps only its last path segment, without control characters, trimmed and cut to 255 characters. An empty result is null.

The handler reads the body itself. The route declares `Accepts` text/plain, so routing answers 415 for any other media type, and the OpenAPI document describes the body as binary. Success is 202 `{ id, status: "queued" }` without a Location header.

- Alternative: a JSON paste endpoint plus a multipart upload endpoint. Rejected: two code paths with different size rules, because JSON escaping grows the body. Invalid UTF-8 inside JSON also fails in the binder with a 400 that has no code, and multipart brings form limits, temporary files and antiforgery.
- Alternative: multipart for both. Rejected: text fields are decoded leniently, so invalid UTF-8 becomes U+FFFD and UNSUPPORTED_CONTENT cannot be detected.
- Alternative: a Location header. Rejected: nginx strips `/api`, so `/runs/{id}` would point at the web app. The body already carries the id.

### The API checks the size limit on the submitted bytes

`Ingestion:MaxSubmissionBytes` defaults to 10,485,760. A MB is 1,048,576 bytes everywhere: in the spec, the docs and the web shell's check before upload.

The handler answers 413 INPUT_TOO_LARGE in two ways:
- When Content-Length is above the limit, before reading anything.
- Otherwise it reads at most limit + 1 bytes, and answers 413 if the extra byte arrives. This covers chunked bodies.

Before reading, it raises the request's `IHttpMaxRequestBodySizeFeature` to limit + 1, so Kestrel's own limit can never answer first. nginx keeps `client_max_body_size 12m` above the default, and its comment already says to raise both limits together.

- Alternative: Kestrel's `MaxRequestBodySize` or `RequestSizeLimit`. Rejected: their 413 has no code, and the in-memory test server does not enforce them.
- Alternative: nginx alone. Rejected: it answers with an HTML page, and direct calls to the API would have no limit.
- Alternative: decimal megabytes. Rejected: nginx's `12m` and file managers use binary units, and either unit works only if every place uses the same one.

### Content checks run in a fixed order, then lines are split

Each submission gets exactly one answer, checked in this order:
1. 401 without a session.
2. 415 for the media type (routing), then for the charset.
3. 413 INPUT_TOO_LARGE.
4. 400 UNSUPPORTED_CONTENT, for bytes that fail `Utf8.IsValid` (which also refuses encoded surrogates) or contain 0x00.
5. One leading UTF-8 BOM is removed, then 400 EMPTY_INPUT if nothing but white space (`char.IsWhiteSpace`) remains.

No rejected submission creates a run or reaches the model.

Lines are split at CRLF, LF and a lone CR. A line break at the very end does not start another line, and empty lines in between count. Lines are numbered from 1 and stored without their line breaks, otherwise unchanged. The same lines feed the statistics, the model, the validator and the later lines endpoint, so line N means the same everywhere. The rules match `File.ReadAllLines`, which the validator's fixtures use.

- Alternative: lenient decoding with U+FFFD. Rejected: it silently changes the text that findings cite.
- Alternative: split on LF only. Rejected: logs with CR-only line breaks become one line, and CRLF lines keep a trailing CR.
- Alternative: also split at NEL, U+2028 and U+2029. Rejected: numbering would disagree with editors.
- Alternative: reject every control character. Rejected: logs contain tabs and ANSI colour codes. Only NUL breaks PostgreSQL text.

### UTF-8 only: 415 UNSUPPORTED_CHARSET before the body is read

A small middleware runs after `UseAuthorization`, so an anonymous call to a protected endpoint still gets 401 first.
- It checks every endpoint that reads a body, meaning every endpoint with `IAcceptsMetadata`: registration, sign-in, `POST /runs` and any later one.
- If the Content-Type names a charset other than `utf-8` or `utf8` (in any case, quoted or not), it answers 415 problem details with code UNSUPPORTED_CHARSET before model binding runs.
- A missing charset means UTF-8.

Today registration and sign-in answer such a request with 500, because `ReadFromJsonAsync` throws for an unknown charset. The rule also ends the accidental transcoding of UTF-16 JSON that those endpoints do today.

- Alternative: an endpoint filter. Rejected: filters run after binding, which has already thrown.
- Alternative: map the exception in the exception handler. Rejected: every such request would be logged as an unhandled error, and the exception type is too broad to map.
- Alternative: accept every charset .NET can decode. Rejected: logs must be UTF-8 anyway, and RFC 8259 requires UTF-8 for JSON, so one rule covers every body.
- Alternative: 415 without a code, like routing's. Rejected: errors the API reports itself carry a code, and the web shell needs one to choose a message.

### Statistics are computed at submission, in Core

`LogStatistics.Compute(lines)` runs during `POST /runs`. The line count, the six level counts and the time range are stored as columns of the run in the same transaction, and never updated. Every status shows them, queued and failed included.

**Levels.** A line has at most one level: the leftmost marker that starts within its first 100 characters. The markers are:
1. A .NET console prefix in lowercase, followed by a colon: `trce:`, `dbug:`, `info:`, `warn:`, `fail:` or `crit:`.
2. A level name as the last word inside square brackets, in any case: `[ERR]`, `[09:00:01 WRN]`, `[Error]`.
3. A level name in capitals, as a whole word: `ERROR`, `WARN`, `INFO` (log4j, logback, NLog, Python logging, Spring Boot).
4. A key followed by a level name in any case: `level=`, `lvl=` and `severity=`, and the JSON keys `"level"`, `"@l"`, `"LogLevel"` and `"severity"`.

Level names:

| Level | Names |
|---|---|
| critical | crit, critical, fatal, ftl |
| error | fail, error, err, severe |
| warning | warn, warning, wrn |
| info | info, information, inf |
| debug | dbug, debug, dbg |
| trace | trce, trace, trc, verbose, vrb |

Lines without a marker, such as stack frames and continuation lines, count toward no level.

**Timestamps.** A line's timestamp is the first ISO 8601 date and time that starts within its first 40 characters. It reads:
- `yyyy-MM-dd`;
- then `T` or a space, then `HH:mm:ss`;
- then an optional fraction after `.` or `,`, kept to 100 ns;
- then an optional offset `Z`, `±HH:mm` or `±HHmm`, which may follow one space.

Impossible dates and times are not timestamps.

The time range runs from the earliest to the latest timestamp. The first timestamp found decides which kind counts:
- If it has an offset, timestamps with an offset are compared as instants, and the range is returned in UTC.
- If it has none, timestamps without an offset are compared as written, and the range is returned without an offset.

Timestamps of the other kind are ignored. The range carries `utc: true` or `utc: false`, and is null when no line has a timestamp.

- Alternative: compute statistics in the worker. Rejected: queued runs, and runs that fail before processing, would have none.
- Alternative: compute them on every read. Rejected: the run page polls.
- Alternative: search whole lines for level words, in any case. Rejected: it counts message text such as "0 errors".
- Alternative: give continuation lines the level of the line above. Rejected: the spec counts lines at a level, and stack frames would inflate the error count.
- Alternative: read times without an offset as UTC. Rejected: it shifts local-time logs by the server's offset.
- Alternative: the first and last timestamps in line order. Rejected (the spec was reworded): logs merged from several threads or services are not in time order.

### Storage: four tables, with each run's lines in one array

One migration, `AddRuns`, creates four tables:
- **runs**:
  - id: UUIDv7, built from the submission time.
  - user_id: a foreign key to users, deleted with the user.
  - file_name; status (queued, running, completed or failed); failure_reason.
  - submitted_at, started_at and finished_at.
  - byte_count and the statistics columns.
  - summary and highest_severity.
  - CHECK constraints: failure_reason is present exactly when the run failed, and summary exactly when it completed.
- **run_logs**: run_id as the primary key, and lines as `text[]`, where line N is `lines[N]`. It has one row per run and is inserted in the submission transaction.
- **run_findings**: run_id, position (in model order), the seven contract fields in contract spelling, and line_refs as `int[]`. It is written only in the transaction that completes the run.
- **run_attempts**: run_id and sequence; outcome (accepted or rejected); rejection_reasons as jsonb `[{code, path}]`; model, prompt_version, started_at and finished_at.

Indexes:
- `(user_id, submitted_at DESC, id DESC)` serves listing.
- `(submitted_at, id) WHERE status = 'queued'` serves the claim.

Status, severity and category are text columns with CHECK constraints. failure_reason has no CHECK on its value, so later changes can add reasons without a migration.

A range of lines is a slice, `lines[a:b]`. PostgreSQL clamps it to the lines that exist, which is the web shell's clamping rule.

- Alternative: one row per line. Rejected: a 10 MB log of short lines becomes millions of rows, and inserting them in time needs COPY.
- Alternative: blocks of 1,000 lines. Rejected: slicing code for a gain not needed at 10 MB. This is the upgrade path if range reads show up in profiles.
- Alternative: the whole log as one text value. Rejected: every range read would split it again, and line numbers would depend on the splitter never changing.
- Alternative: statistics in their own table, or findings as jsonb on the run. Rejected: a one-to-one join for nothing. Findings get columns that later changes extend, such as occurrence counts.

### An attempt stores reason codes and contract paths, never model text

Each attempt stores:
- its sequence and outcome;
- the model and the prompt version;
- its start and end times;
- for a rejection, every violation's code and path.

A path keeps the contract's field names and array indexes. Any other segment, such as an unknown field the model made up, is stored as `*`: `findings[0]["max tokens"]` becomes `findings[0].*`. Messages are never stored, because they quote the response (`found "fatal"`). The response text exists only in memory, for the corrective request, and is never logged.

A model call that yields no response (MODEL_UNAVAILABLE or MODEL_CONFIGURATION_ERROR) writes no attempt, because an attempt is a response the validator evaluated. `add-claude-integration` decides whether failed calls get records of their own.

- Alternative: store the violations verbatim. Rejected: it would store unvalidated model output, against the non-negotiables.
- Alternative: codes only. Rejected: it loses which field failed, which the timeline and prompt work need.
- Alternative: store the raw response for debugging. Rejected: the non-negotiables forbid it.

### The runs table is the queue, claimed with SKIP LOCKED

`RunWorker` is a BackgroundService in Infrastructure. It starts in three steps:
1. It waits until migrations are applied (`DatabaseMigrationState` gains `WhenApplied`).
2. It fails interrupted runs (see the next decision).
3. It runs `Analysis:MaxConcurrentRuns` loops, 2 by default.

Each loop claims the oldest queued run with one statement, then processes it in its own scope and repeats:

```sql
UPDATE runs SET status = 'running', started_at = @now
WHERE id = (SELECT id FROM runs WHERE status = 'queued'
            ORDER BY submitted_at, id LIMIT 1 FOR UPDATE SKIP LOCKED)
RETURNING id
```

When nothing is queued, a loop waits for whichever comes first: an in-process signal that `POST /runs` raises after its commit, or 5 seconds.

Every exception inside a loop is caught and logged, and the loop waits the same way before trying again. An exception that escapes a BackgroundService stops the host, and the API must keep answering `/health` while PostgreSQL is down. No transaction or lock is held during a model call.

- Alternative: an in-process Channel. Rejected: it is lost on restart, so the database would be scanned anyway.
- Alternative: Hangfire, Quartz.NET or Wolverine. Rejected: each brings its own schema and job retries, which would repeat model calls and break the rule of one correction per run.
- Alternative: LISTEN/NOTIFY. Rejected: it is needed only to wake other instances.

### A restart fails interrupted runs; nothing resumes

When the worker starts, before it claims anything, it fails every run that is still running with reason INTERRUPTED, in one UPDATE. Queued runs are processed as usual.

At a graceful shutdown, the stopping token cancels the model call, and the processor marks its run INTERRUPTED, using a short timeout of its own. This is best effort; after a kill, the sweep at the next start does it. Attempts already recorded are kept. All of this assumes one API instance per database, which is what Docker Compose runs.

- Alternative: requeue runs the model had not answered. Rejected: it can repeat a model call that was already paid for, needs a claim limit against crash loops, and lets a run move from running back to queued.
- Alternative: leases with heartbeats. Rejected: only needed for several instances. This is the upgrade path.
- Alternative: let the run finish during shutdown. Rejected: Docker stops a container 10 seconds after asking it to stop, and a model call can take longer.

### The model seam lives in Core

The seam has four parts:
- `IAnalysisModel.AnalyzeAsync(AnalysisRequest, CancellationToken)` returns a `ModelReply`.
- `AnalysisRequest` holds `RunId`, `Lines` (each a `NumberedLine` with `Number` and `Text`) and an optional `Correction` (`RejectedResponseText` and `Violations`).
- `ModelReply` holds `Text`, `StopReason`, `Model` and `PromptVersion`.
- A call that yields no response throws `ModelCallException`, with failure reason MODEL_UNAVAILABLE or MODEL_CONFIGURATION_ERROR:
  - MODEL_UNAVAILABLE: a transient failure, including a timeout, after the adapter's retries.
  - MODEL_CONFIGURATION_ERROR: retrying cannot help.

The reply is text plus a stop reason, so every adapter's output goes through the validator. Lines carry their original numbers, so chunked or collapsed input from `add-large-log-support` fits the same request.

`add-claude-integration` adds `ClaudeAnalysisModel` behind the same interface. It:
- renders its versioned prompt and the correction;
- sends `AnalysisContract.SchemaJson` as the output format;
- maps stop reasons as the `add-analysis-contract` design records;
- adds usage to `ModelReply`, with matching columns on run_attempts;
- adds a `Model:Provider` setting, so the stand-in stays available without an API key.

- Alternative: return a parsed `Analysis`. Rejected: an adapter could bypass validation.
- Alternative: a prompt string in and text out. Rejected: prompts and their versions belong to `add-claude-integration`'s templates, and the stand-in and the tests would have to parse prose.
- Alternative: report failures as reply values. Rejected: a reply would no longer always mean text to validate, and the SDK throws anyway.

### The stand-in model cites real lines

`StandInAnalysisModel` lives in Core. It is deterministic and does no I/O. It takes up to three lines with distinct text: critical first, then error, then warning, judged by the statistics' level rule. It writes one finding per line:
- title: `<Level> logged on line N`;
- severity: the line's level;
- category: exception or timeout by keyword, otherwise other;
- line_refs: `[N]`;
- evidence: the trimmed line, which is always a substring of it;
- explanation and suggested action: fixed texts saying that no AI model analyzed the log.

A log without such lines gets no findings and a summary that says so. The stand-in reports model and prompt version `stand-in` and `StopReason.Completed`. Its JSON goes through the validator like any reply.

Integration tests replace it with `ScriptedAnalysisModel`, which lives in the test project. Each test gives it a queue of replies (text and stop reason) or exceptions, and it records every request it receives.

- Alternative: fixed canned JSON. Rejected: its lines and evidence would not match most logs, so every local run would fail validation.
- Alternative: script the production stand-in through configuration. Rejected: test controls would live in production settings.
- Alternative: a WireMock fake of the Anthropic API now. Rejected: that is `add-claude-integration`'s transport seam.

### The run pipeline lives in Core and records each attempt as it happens

`AnalysisPipeline` makes at most two model calls:
1. Attempt 1 sends every line.
2. If the validator rejects it, for any reason including TRUNCATED and REFUSED, attempt 1 is recorded at once. Attempt 2 then sends the same lines with the correction: the rejected text, and every violation with its code, path and message.

The final attempt is written in the same transaction as the run's outcome:
- Accepted: the findings (in model order and contract spelling), the summary, highest_severity, status completed and finished_at.
- Rejected twice: status failed with VALIDATION_FAILED, and nothing from either response.

Other endings:
- `ModelCallException` fails the run with its reason, keeping any attempt already recorded.
- Cancellation at shutdown fails it with INTERRUPTED.
- Any other exception fails it with INTERNAL_ERROR, logged with the run id.

The pipeline writes through a port, `IRunRecorder`, which Infrastructure implements, so it is unit-tested without a database.

- Alternative: the pipeline in Infrastructure, using EF Core directly. Rejected: its rules could be tested only against PostgreSQL.
- Alternative: write every attempt at the end. Rejected: a crash would lose the record of a paid call.
- Alternative: write the accepted attempt before the findings. Rejected: a crash between the two writes would leave an accepted attempt without findings.

### Failure reasons are a closed set

The five reasons are VALIDATION_FAILED, MODEL_UNAVAILABLE, MODEL_CONFIGURATION_ERROR, INTERRUPTED and INTERNAL_ERROR.
- They are defined in Core (`FailureReasons`), named in the spec, and explained in docs/runs-api.md, so the web shell can describe each one.
- Validator codes stay on attempts and are never run failure reasons.
- A timed-out call ends as MODEL_UNAVAILABLE, because model-gateway treats timeouts as transient.
- `add-large-log-support` passes a failed chunk's reason through.

- Alternative: a separate MODEL_TIMEOUT. Rejected: once the retries are exhausted it is indistinguishable from other unavailability.
- Alternative: a free-text error detail. Rejected: it may leak internals. Details go to the server log.

### Reading and listing go through one ownership filter

`GET /runs/{id}` takes the id as a string. A malformed id, an unknown id and another person's run all get the same 404 RUN_NOT_FOUND.

Every run query filters user_id by the session's NameIdentifier claim in SQL, through RunStore's owned queries. The later lines and attempts endpoints reuse them. The worker uses separate methods that no endpoint reaches.

The run resource is camelCase:
- id, status, fileName, submittedAt, startedAt, finishedAt and failureReason;
- statistics:
  - lineCount and byteCount;
  - levels, with critical, error, warning, info, debug and trace;
  - timeRange, which is `{ start, end, utc }` or null;
- summary and findings, both null until the run completes. Findings come in model order; the web shell sorts and filters them.

`GET /runs?limit=&cursor=` lists the person's runs, newest first by (submitted_at, id):
- limit is 1 to 100, default 20; anything else is 400 INVALID_LIMIT.
- The cursor is opaque: base64url of the last item's submission time and id. One the API cannot read is 400 INVALID_CURSOR. A readable cursor someone made up only moves the page within the caller's own runs, because the ownership filter still applies, so it is not signed.
- nextCursor is null on the last page.
- Each item has id, fileName, submittedAt, status and highestSeverity. highestSeverity is written when the run completes, and is null without findings.

- Alternative: an `{id:guid}` route constraint. Rejected: `7f3c` would get routing's 404 without a code, which differs from the answer for another person's run.
- Alternative: load the run, then compare its owner. Rejected: easy to forget in a later endpoint. The query filter fails closed.
- Alternative: an EF Core global query filter. Rejected: the worker reads every run, and the context would need the HTTP user.
- Alternative: offset paging. Rejected: new submissions shift the pages, repeating or skipping runs.

### Code placement

- **Core:**
  - Ingestion: byte checks, splitting, file names, statistics with the level and timestamp rules, and `IngestionOptions`.
  - Processing: the model seam, `AnalysisPipeline`, `IRunRecorder`, `FailureReasons`, the stored rejection paths and `StandInAnalysisModel`. (A namespace named Analysis would hide the contract's `Analysis` record.)
- **Infrastructure:**
  - The run entities and their mapping, and the `AddRuns` migration.
  - `RunStore`, the `IRunRecorder` implementation, `RunProcessor`, `RunWorker` and the queue signal.
- **Api:** `RunEndpoints`, `RunErrors`, the response records and the charset middleware.
- **Docs:**
  - docs/runs-api.md documents the endpoints, codes, failure reasons, and the level and timestamp rules.
  - docs/accounts-api.md gains UNSUPPORTED_CHARSET.

Core keeps the rules and the orchestration without infrastructure, so `CoreDependencyTests` stays green.

### Tests drive processing step by step

Integration tests prove every scenario through HTTP against PostgreSQL.
- `RunsApiFactory` derives from `PostgresApiFactory`. It adds a `FakeTimeProvider`, sets `MaxSubmissionBytes` to 1,048,576, replaces the model with `ScriptedAnalysisModel`, and removes `RunWorker`.
- A test submits, calls `ProcessQueuedRunsAsync` (which drains the queue through `RunProcessor`) and reads.
- Recovery tests set up running rows and run the startup sweep.
- One test keeps the real worker, with the stand-in, and polls until the run completes.
- Attempts are checked with SQL, because no endpoint exposes them yet.
- `AccessPolicyTests` sends each endpoint the content type it declares.

Unit tests cover:
- decoding and splitting;
- a table of level and timestamp formats, and the time-range rules;
- file-name cleaning;
- the pipeline, with a fake model and a fake recorder;
- the stand-in's replies passing the validator for every fixture log.

- Alternative: run the real worker in every test and poll. Rejected: slow and timing-dependent.
- Alternative: a production setting that turns the worker off. Rejected: a switch that exists only for tests. The test factory removes the hosted service instead.

## Risks / Trade-offs

- [The startup sweep assumes one API instance per database. A second instance would fail the first one's running runs] → Documented; Docker Compose runs one. Leases with heartbeats are the upgrade path.
- [An API restart during an analysis fails that run with INTERRUPTED, and the person resubmits] → The web shell explains the reason in plain language. With the stand-in, runs finish in milliseconds. Revisit with real model calls.
- [A range read decompresses the run's whole array of lines] → Fine under 10 MB. Switch to blocks of lines if range reads show up in profiles; the API stays the same.
- [A 10 MB submission holds the bytes, the decoded text and the lines in memory at once, roughly 50 MB] → Bounded by the size limit. `add-input-safety` adds per-person limits.
- [Level and timestamp detection are heuristics] → docs/runs-api.md lists the rules as a table, with a unit test per format. An unknown format counts toward no level, and adding one is a Core change with its own test.
- [JSON bodies in UTF-16 or Latin-1, which registration and sign-in transcode today, get 415] → Browsers and the generated client always send UTF-8. Noted in docs/accounts-api.md.
- [An anonymous `POST /runs` with the wrong media type gets routing's 415 before the session check, so `AccessPolicyTests`' JSON probe fails] → The test sends each endpoint its declared content type. The accounts design already accepted that 405 and 415 come before 401.
- [A generated client may type the text/plain body as a string, and `file.text()` replaces invalid UTF-8] → The body is described as binary in OpenAPI, and `add-web-shell` sends the File as a Blob.
- [Stand-in findings could be mistaken for a real analysis] → Its summary and explanations say no AI model was used, and its attempts record model `stand-in`.
- [This change needs `add-analysis-contract` (PR #4): the validator, the contract types, the embedded schema and the response-validation main spec] → Implement it after PR #4 merges, and archive it after PR #4.

## Migration Plan

`AddRuns` creates the four tables, and the API applies it at startup, like `InitialAccounts`. To roll back on a development machine, run `docker compose down --volumes`, or the `dotnet ef database update InitialAccounts` command with the options in the README's "Database migrations" section.
