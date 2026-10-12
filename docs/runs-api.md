# Runs API

A run is one submitted log and its analysis. Each run belongs to the signed-in person who submitted it. The rules come from the `log-ingestion` and `analysis-history` specs, `openspec/specs/log-ingestion/spec.md` and `openspec/specs/analysis-history/spec.md`. The decisions behind them are in `openspec/changes/archive/2026-10-10-add-api-skeleton/design.md`.

Paths are as the API sees them. Through nginx (http://localhost:8080) and the Angular dev server, prefix them with `/api`, for example `/api/runs`. Every run endpoint needs a session.

## Errors

Errors are problem details with a `code` member, as described in [accounts-api.md](accounts-api.md#errors). Match on `code`, not on `title` or `detail`.

| Code | Status | Meaning |
|---|---|---|
| `SIGN_IN_REQUIRED` | 401 | The request has no session, or its session has ended. |
| `UNSUPPORTED_CHARSET` | 415 | The `Content-Type` names a character set other than UTF-8. |
| `INPUT_TOO_LARGE` | 413 | The log is larger than the size limit. |
| `UNSUPPORTED_CONTENT` | 400 | The log is not valid UTF-8, or contains a NUL byte. |
| `EMPTY_INPUT` | 400 | The log contains nothing but white space. |
| `RUN_NOT_FOUND` | 404 | You have no run with this id: it does not exist, the id is malformed, or the run is someone else's. |
| `INVALID_LIMIT` | 400 | `limit` is not a whole number from 1 to 100. |
| `INVALID_CURSOR` | 400 | `cursor` is not a cursor the API can read. |

On `POST /runs`, a `Content-Type` other than `text/plain` gets `415` from the framework, without a `code`. On `GET /runs/{id}/lines`, a `from` or `to` that is missing or not a whole number from -2,147,483,648 to 2,147,483,647 gets `400` from the framework, without a `code`.

## Submit a log

`POST /runs` (needs a session)

```http
POST /runs?fileName=orders-api.log
Content-Type: text/plain

2026-10-10 09:00:01.120 +00:00 [INF] Application started
2026-10-10 09:00:05.734 +00:00 [ERR] Payment for order 42 failed
```

- The body is the log itself: `Content-Type: text/plain`, with no charset or `charset=utf-8`. A body sent without a `Content-Type` is read the same way.
- `fileName` names an uploaded file. A paste sends none, and its run has no file name.
- `202 Accepted` with `{ "id": "<uuid>", "status": "queued" }`. There is no `Location` header: behind nginx it would point at the web app. The run is analyzed in the background.
- `200 OK` with `{ "id": "<uuid>", "status": "completed" }` when the submission reuses one of your runs, as described in [Reuse](#reuse).

Send a paste as its text, and an upload as the file's bytes, unchanged. In a browser, send the `File` itself (a `Blob`), never `file.text()`: the browser would replace invalid UTF-8 with U+FFFD, and the API could no longer refuse it.

The file name keeps only its last path segment (after the last `/` or `\`), without control characters, trimmed and cut to 255 characters (Unicode code points). A name that ends up empty is stored as no name.

### Checks

A submission gets exactly one answer. The checks run in this order, and only the first failure is reported:

1. `415` without a `code` when the `Content-Type` is not `text/plain`. Routing answers this before the session is checked.
2. `401 SIGN_IN_REQUIRED` without a session.
3. `415 UNSUPPORTED_CHARSET` when the `Content-Type` names a character set other than UTF-8. `utf-8` and `utf8` are accepted in any letter case, quoted or not. No charset means UTF-8.
4. `413 INPUT_TOO_LARGE` when the body is larger than the size limit.
5. `400 UNSUPPORTED_CONTENT` when the bytes are not valid UTF-8, including encoded UTF-16 surrogates, or contain a NUL byte (0x00). Other control characters, such as tabs and ANSI color codes, are kept.
6. One leading UTF-8 byte order mark is removed. Then `400 EMPTY_INPUT` when nothing but white space is left, including for an empty file.

A rejected submission creates no run and never reaches the model.

### Reuse

A submission that passes the checks is compared with your completed runs. When one of them has exactly the same bytes and was analyzed with the model and prompt version configured now, the answer is `200 OK` with that run's id and status `completed`. No run is created and the model is not called.

- The bytes are compared through their SHA-256, as sent, before the byte order mark is removed.
- Only your own runs count. Another person's identical log gets a new run, and their run is never revealed.
- A queued, running or failed run is never reused, and neither is a run analyzed with another model or prompt version. Changing either setting makes the next identical submission analyze the log again.
- The file name plays no part. The reused run keeps the name it was submitted with.
- Runs submitted before reuse existed have no stored hash, so they are never reused.

### Size limit

The limit applies to the bytes as sent. It is set by `Ingestion:MaxSubmissionBytes` (environment variable `Ingestion__MaxSubmissionBytes`):

- The default is 10,485,760 bytes. A MB is 1,048,576 bytes everywhere, so the default is 10 MB.
- It can be set from 1 to 104,857,600 bytes (100 MB). Any other value stops the API at startup with a message naming the setting.
- A body whose `Content-Length` is above the limit is refused before any of it is read. A body sent without `Content-Length` is refused as soon as the byte past the limit arrives. So is a chunked body whose framing is out of proportion to its data, such as one sent a byte per chunk.
- nginx accepts bodies up to 12 MB (`client_max_body_size` in `src/web/nginx.conf`) and answers larger ones with its own `413` HTML page. Raise both limits together.
- The API holds a log in memory several times over while it accepts it: about 5 times its size for lines of a typical length, and up to about 40 times for a log of very short or empty lines, mostly while its lines are written to the database. Analyzing a run needs up to about 30 times. Each submission in progress and each run being analyzed needs its own. Choose the limit with that in mind.

### Lines

- CRLF, LF and a lone CR each end a line. A line break at the very end does not start another line, and empty lines in between count. A paste of three lines, each ending with CRLF, has 3 lines.
- Lines are numbered from 1 and stored without their line breaks, otherwise unchanged. Other characters, such as NEL (U+0085) and the line separator U+2028, stay inside their line, as editors show it.
- Line N is the same line everywhere: in the statistics, in what the model receives, and in a finding's cited lines and evidence.

## Read the size limit

`GET /runs/limits` (needs a session)

```json
{ "maxSubmissionBytes": 10485760 }
```

- `200 OK` with the size limit that `POST /runs` applies, in bytes. It is set by `Ingestion:MaxSubmissionBytes`, as described in [Size limit](#size-limit).
- A client can check a file against it and refuse a larger one before sending it. The limit changes only when the API restarts with another setting.

## Statistics

The statistics are computed by code when the log is submitted, never taken from the model, and never change. Every run has them, whatever its status:

- the number of lines and the number of bytes;
- the number of lines at each level: critical, error, warning, info, debug and trace;
- the time range: the earliest and the latest timestamp.

### Levels

A line has at most one level: that of the leftmost marker starting within its first 100 characters. Markers further along are message text.

| Marker | Examples | Typical source |
|---|---|---|
| A .NET console prefix in lowercase, followed by a colon | `fail: Orders.Api[0] ...` | `Microsoft.Extensions.Logging` console |
| A level name as the last word inside square brackets, in any case | `[ERR]`, `[09:15:02 WRN]`, `[Error]` | Serilog and others |
| A level name in capitals, as a whole word | `ERROR`, `WARN`, `INFO`, `FATAL`, `INF`, `ERR` | log4j, logback, NLog, Python logging, Spring Boot, zerolog |
| A key followed by a level name, in any case | `level=error`, `lvl=WARN`, `severity=critical`, `"@l":"Error"`, `"level":"warn"`, `"LogLevel":"Debug"` | logfmt, JSON logs |

Square brackets whose last word is not a level name, such as `[main]`, are not a marker, so a marker inside them still counts: `[ERROR main]` is an error line. In square brackets, in capitals and after a key, every name in the table below counts, the short ones too. A short name in capitals, such as `INF` or `ERR`, counts only where it stands alone, not touching a letter, a digit, `-`, `.` or `/`: `INF-WEB01` and `welcome.FTL` are not markers. Colour codes (ANSI escape sequences) are ignored when looking for a level, so coloured console output counts too.

The keys, in any letter case, are `level=`, `lvl=` and `severity=`, whose value may be quoted, and the JSON keys `"level"`, `"@l"`, `"LogLevel"` and `"severity"`.

| Level | Names |
|---|---|
| critical | crit, critical, fatal, ftl |
| error | fail, error, err, severe |
| warning | warn, warning, wrn |
| info | info, information, inf |
| debug | dbug, debug, dbg |
| trace | trce, trace, trc, verbose, vrb |

The .NET console prefixes are `crit:`, `fail:`, `warn:`, `info:`, `dbug:` and `trce:`. Lines without a marker, such as stack frames and continuation lines, count toward no level. So does a line in a format the table does not list. Words such as "error" in lowercase message text are not markers: "Processed 0 errors" has no level.

### Timestamps

A line's timestamp is the first ISO 8601 date and time that starts within its first 40 characters:

- `yyyy-MM-dd`;
- then `T` or a space, then `HH:mm:ss`;
- then an optional fraction of a second after `.` or `,`, read to 100 ns;
- then an optional offset, `Z`, `±HH:mm` or `±HHmm`, which may follow one space.

| Line starts with | Read as |
|---|---|
| `2026-10-10T09:00:01Z` | 09:00:01 UTC |
| `2026-10-10 09:00:01.123 +02:00 [INF]` (Serilog) | 07:00:01.123 UTC |
| `2026-10-10T09:00:01.1234567+02:00` (.NET round-trip) | 07:00:01.1234567 UTC |
| `2026-10-10 09:00:01+0200` | 07:00:01 UTC |
| `2026-10-10 09:00:01,123 ERROR` (log4j, Python) | 09:00:01.123, no offset |

Impossible dates and times, such as `2026-02-30`, are not timestamps. Neither is one whose offset is more than 14 hours, or whose offset minutes are above 59.

### Time range

The time range runs from the earliest to the latest timestamp, wherever they are in the log: logs merged from several threads or services are not in time order. The first timestamp found decides which timestamps count:

- If it has an offset, only timestamps with an offset count. They are compared as instants, and the range is in UTC (`utc` is true).
- If it has none, only timestamps without an offset count. They are compared as written, and the range has no offset (`utc` is false). They are not converted, because the log does not say in which time zone it was written.

The time range is returned to the microsecond. A log in which no line has a timestamp has no time range.

## Read a run

`GET /runs/{id}` (needs a session)

```json
{
  "id": "0199d0a5-7c1e-7b3a-9f41-2d6c8e5a1b20",
  "status": "completed",
  "fileName": "orders-api.log",
  "submittedAt": "2026-10-10T09:00:00+00:00",
  "startedAt": "2026-10-10T09:00:01+00:00",
  "finishedAt": "2026-10-10T09:00:03+00:00",
  "failureReason": null,
  "statistics": {
    "lineCount": 3,
    "byteCount": 196,
    "levels": { "critical": 1, "error": 1, "warning": 0, "info": 1, "debug": 0, "trace": 0 },
    "timeRange": { "start": "2026-10-10T09:00:01Z", "end": "2026-10-10T09:00:05Z", "utc": true }
  },
  "summary": "The payment service ran out of database connections.",
  "findings": [
    {
      "title": "Database connection pool exhausted",
      "severity": "critical",
      "category": "resource",
      "lineRefs": [3],
      "evidence": "Database connection pool exhausted",
      "explanation": "Requests wait for a connection until they time out.",
      "suggestedAction": "Check for connections that are not returned to the pool."
    }
  ]
}
```

- `200 OK` with the run. Member names are camelCase. Statuses, severities and categories are lowercase, as the output contract ([output-contract.md](output-contract.md)) spells them.
- `status` is `queued`, then `running`, then `completed` or `failed`. `startedAt` is set when processing starts, and `finishedAt` when the run completes or fails.
- `statistics` is there whatever the status. `timeRange` is null when no line has a timestamp. Its `start` and `end` end with `Z` when `utc` is true, and have no offset when it is false.
- `summary` and `findings` are null until the run completes. A completed run without findings has an empty list. Findings come in the model's order. `lineRefs` are line numbers from 1, and `evidence`, without the white space at its start and end, appears on at least one of the cited lines.
- `failureReason` is null unless the run failed.
- An unknown id, a malformed id such as `7f3c`, and another person's run all get `404 RUN_NOT_FOUND` with the same body, so the answer reveals nothing about other people's runs.

## Read a run's lines

`GET /runs/{id}/lines?from=2&to=3` (needs a session)

```json
{
  "lines": [
    { "number": 2, "text": "2026-10-10T09:00:04Z fail: Orders.Api[0] Payment for order 42 failed" },
    { "number": 3, "text": "2026-10-10T09:00:05Z crit: Orders.Api[0] Database connection pool exhausted" }
  ]
}
```

- `200 OK` with lines `from` to `to` of the run's log, in order, each with its number. Lines are numbered and stored as described in [Lines](#lines).
- `from` and `to` are required whole numbers from -2,147,483,648 to 2,147,483,647. A missing value, or any other value, gets `400` from the framework, without a `code`.
- The range is clamped to the lines that exist. A `from` below 1 starts at line 1, and a `to` past the last line stops at the last line: asking a 500-line log for lines 495 to 520 returns lines 495 to 500. A range that ends before it starts, or begins after the last line, returns an empty list.
- Any range can be read, up to the whole log, whatever the run's status: the lines are stored when the log is submitted.
- An unknown id, a malformed id and another person's run all get `404 RUN_NOT_FOUND` with the same body, as in [Read a run](#read-a-run).

## List your runs

`GET /runs?limit=20&cursor=<nextCursor>` (needs a session)

```json
{
  "items": [
    {
      "id": "0199d0a5-7c1e-7b3a-9f41-2d6c8e5a1b20",
      "fileName": "orders-api.log",
      "submittedAt": "2026-10-10T11:00:00+00:00",
      "status": "completed",
      "highestSeverity": "critical"
    },
    {
      "id": "0199d06e-1a2b-7c3d-8e4f-5a6b7c8d9e0f",
      "fileName": null,
      "submittedAt": "2026-10-10T10:00:00+00:00",
      "status": "queued",
      "highestSeverity": null
    }
  ],
  "nextCursor": null
}
```

- Your own runs only, newest first by submission time, then by id for runs submitted at the same time.
- `limit` is the page size: 1 to 100, and 20 when it is left out. Any other value, including one that is not a whole number, gets `400 INVALID_LIMIT`.
- `nextCursor` requests the next page: send it back as `cursor`, unchanged. It is null on the last page. A cursor the API cannot read gets `400 INVALID_CURSOR`.
- A cursor marks the last run of its page, so runs submitted in the meantime do not shift the pages that follow. It is not a secret: whatever it says, the list holds only your own runs.
- `highestSeverity` is the most serious severity among the run's findings. It is set when the run completes, and is null before that and for a run without findings.

## Processing

A background worker in the API analyzes queued runs, oldest first. It processes `Analysis:MaxConcurrentRuns` runs at a time (environment variable `Analysis__MaxConcurrentRuns`): 2 by default, and 1 to 16. Any other value stops the API at startup with a message naming the setting. A submission wakes the worker at once, and the worker also looks for queued runs every 5 seconds.

1. The run's status becomes `running`, and the model gets every line with its number.
2. The model's response must pass the validator, described in [output-contract.md](output-contract.md), before any part of it is stored or shown. If it passes, the run completes with its summary and findings. A NUL character (U+0000) in their text is left out, because the database cannot store it.
3. If the validator rejects the response, for any reason, the model is asked once more, with the same lines, the rejected response and every reason it was rejected. That includes a response cut off at the model's output limit (`TRUNCATED`) and a refusal (`REFUSED`).
4. If the corrected response passes, the run completes with its findings. If it is rejected too, the run fails with `VALIDATION_FAILED`, and nothing from either response is stored or returned.

A run gets at most two model calls.

### Attempts

Every response the validator evaluated is stored as an attempt of its run, as soon as it is evaluated. No endpoint returns attempts yet; `add-attempt-timeline-and-insights` adds one. An attempt holds:

- its number: 1 for the first response, 2 for the corrected one;
- its outcome, `accepted` or `rejected`;
- for a rejected response, each reason: the reason code and the path of the failing field, such as `LINE_OUT_OF_RANGE` at `findings[0].line_refs[0]`;
- the model and the prompt version that produced it;
- when the model call started and ended;
- what the call cost: the tokens the model read (`input_tokens`, cache reads and writes included) and wrote (`output_tokens`, thinking included), how many times it was sent again after a transient failure (`transport_retries`), and the model service's id for the request that answered (`request_id`). The stand-in records zeros and no request id.

The response text is never stored, and neither are the reasons' messages, which quote it. A path keeps the contract's field names and array indexes. Any other name in it was written by the model, so it is stored as `*`: `findings[0]["max tokens"]` becomes `findings[0].*`.

A model call that yields no response, such as one that times out, is not an attempt.

### The stand-in model

Until Claude is integrated, runs are analyzed by a stand-in model. It makes no network calls, and gives the same answer for the same log. Its attempts record the model and prompt version `stand-in`. It never answers with anything the validator rejects.

- It flags up to three lines with different text: critical lines first, then error lines, then warning lines, by the level rule in [Levels](#levels).
- Each flagged line becomes one finding. Its title is the level and the line number, such as `Error logged on line 12`, and its severity is the line's level. Its category is `exception` or `timeout` when the line says so, and `other` otherwise. Its evidence is the whole line, trimmed.
- The summary and each finding's explanation say that no AI model analyzed the log.
- A log without critical, error or warning lines gets no findings, and a summary that says so.

### Failure reasons

A failed run has exactly one failure reason. The set is closed. The validator's reason codes, such as `EVIDENCE_NOT_FOUND`, appear only on attempts, never as a failure reason.

| Reason | Meaning |
|---|---|
| `VALIDATION_FAILED` | The model's response and its corrected response were both rejected. Nothing from either is stored. |
| `MODEL_UNAVAILABLE` | The model service failed, or did not answer in time, after retries. Submitting the log again later may work. The server log has the cause. |
| `MODEL_CONFIGURATION_ERROR` | The model service refused the request in a way that retrying cannot fix, such as a missing or wrong API key. The API's configuration must be fixed; the server log has the cause. |
| `INTERRUPTED` | The API stopped while it was processing the run. Submit the log again. |
| `INTERNAL_ERROR` | An unexpected error, for example while the run's lines were read. The server log has the details, with the run's id. |

With the stand-in model, a run can fail only with `INTERRUPTED` or `INTERNAL_ERROR`.

## Restarts

Nothing resumes a run that was being processed when the API stopped. Every run is in a defined state after a restart:

- When the API starts, before it processes anything, it fails every run that is still running, with `INTERRUPTED`. Attempts already recorded are kept. Submit the log again to analyze it.
- Runs that were still queued are processed as usual.
- At a graceful shutdown, such as `docker compose stop`, the model call in progress is cancelled and its run fails with `INTERRUPTED` at once. A response that arrived before the shutdown is still recorded, and completes the run if it is accepted, but no new model call starts. After a crash or a kill, the run fails when the API starts again.

This assumes one API instance per database, which is what Docker Compose runs. A second instance would fail the first instance's runs in progress when it starts.
