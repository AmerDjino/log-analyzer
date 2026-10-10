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

On `POST /runs`, a `Content-Type` other than `text/plain`, or none, gets `415` from the framework, without a `code`.

## Submit a log

`POST /runs` (needs a session)

```http
POST /runs?fileName=orders-api.log
Content-Type: text/plain

2026-10-10 09:00:01.120 +00:00 [INF] Application started
2026-10-10 09:00:05.734 +00:00 [ERR] Payment for order 42 failed
```

- The body is the log itself: `Content-Type: text/plain`, with no charset or `charset=utf-8`.
- `fileName` names an uploaded file. A paste sends none, and its run has no file name.
- `202 Accepted` with `{ "id": "<uuid>", "status": "queued" }`. There is no `Location` header: behind nginx it would point at the web app. The run is analyzed in the background.

Send a paste as its text, and an upload as the file's bytes, unchanged. In a browser, send the `File` itself (a `Blob`), never `file.text()`: the browser would replace invalid UTF-8 with U+FFFD, and the API could no longer refuse it.

The file name keeps only its last path segment (after the last `/` or `\`), without control characters, trimmed and cut to 255 characters (Unicode code points). A name that ends up empty is stored as no name.

### Checks

A submission gets exactly one answer. The checks run in this order, and only the first failure is reported:

1. `401 SIGN_IN_REQUIRED` without a session.
2. `415` without a `code` when the `Content-Type` is not `text/plain`.
3. `415 UNSUPPORTED_CHARSET` when the `Content-Type` names a character set other than UTF-8. `utf-8` and `utf8` are accepted in any letter case, quoted or not. No charset means UTF-8.
4. `413 INPUT_TOO_LARGE` when the body is larger than the size limit.
5. `400 UNSUPPORTED_CONTENT` when the bytes are not valid UTF-8, including encoded UTF-16 surrogates, or contain a NUL byte (0x00). Other control characters, such as tabs and ANSI color codes, are kept.
6. One leading UTF-8 byte order mark is removed. Then `400 EMPTY_INPUT` when nothing but white space is left, including for an empty file.

A rejected submission creates no run and never reaches the model.

### Size limit

The limit applies to the bytes as sent. It is set by `Ingestion:MaxSubmissionBytes` (environment variable `Ingestion__MaxSubmissionBytes`):

- The default is 10,485,760 bytes. A MB is 1,048,576 bytes everywhere, so the default is 10 MB.
- It can be set from 1 to 104,857,600 bytes (100 MB). Any other value stops the API at startup with a message naming the setting.
- A body whose `Content-Length` is above the limit is refused before any of it is read. A body sent without `Content-Length` is refused as soon as the byte past the limit arrives.
- nginx accepts bodies up to 12 MB (`client_max_body_size` in `src/web/nginx.conf`) and answers larger ones with its own `413` HTML page. Raise both limits together.

### Lines

- CRLF, LF and a lone CR each end a line. A line break at the very end does not start another line, and empty lines in between count. A paste of three lines, each ending with CRLF, has 3 lines.
- Lines are numbered from 1 and stored without their line breaks, otherwise unchanged. Other characters, such as NEL (U+0085) and the line separator U+2028, stay inside their line, as editors show it.
- Line N is the same line everywhere: in the statistics, in what the model receives, and in a finding's cited lines and evidence.

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
| A level name in capitals, as a whole word | `ERROR`, `WARN`, `INFO`, `FATAL` | log4j, logback, NLog, Python logging, Spring Boot |
| A key followed by a level name, in any case | `level=error`, `lvl=WARN`, `severity=critical`, `"@l":"Error"`, `"level":"warn"`, `"LogLevel":"Debug"` | logfmt, JSON logs |

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
- `summary` and `findings` are null until the run completes. A completed run without findings has an empty list. Findings come in the model's order. `lineRefs` are line numbers from 1, and `evidence` appears on at least one of the cited lines.
- `failureReason` is null unless the run failed.
- An unknown id, a malformed id such as `7f3c`, and another person's run all get `404 RUN_NOT_FOUND` with the same body, so the answer reveals nothing about other people's runs.

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
