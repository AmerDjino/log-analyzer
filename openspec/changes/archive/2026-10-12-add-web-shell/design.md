# Design: Add the web shell

## Context

See proposal.md for motivation and the delta specs for the rules.

The Angular app in `src/web` is an empty shell: a header, a router outlet and no routes. It is zoneless, uses standalone components, and runs Vitest unit tests through the Angular CLI.

The browser reaches the API on the same origin:
- nginx serves the build and forwards `/api/` to the API, stripping the prefix. The Angular dev server's proxy does the same.
- Every API endpoint needs a session, except the health check, registration, sign-in and, in Development, the OpenAPI document.
- The session cookie `la_session` is `HttpOnly`, `Secure` and `SameSite=Strict`, so page scripts cannot see whether it exists.

The API from `add-user-accounts` and `add-api-skeleton` answers errors as problem details with a `code`. A run's lines are stored as one `text[]` per run in `run_logs`, where line N is `lines[N]`. Every run query goes through `RunStore`'s ownership filter.

The OpenAPI document describes every route's answers, and it describes the `POST /runs` body as binary. It describes errors as plain problem details, without `code`. The `add-user-accounts` design left describing `code` to this change.

`add-api-skeleton` left two follow-ups for this change:
- send an uploaded file as its own bytes;
- learn the configured size limit rather than assume 10 MB.

CI has four jobs: specs, backend, frontend and stack. The stack job starts Docker Compose and checks `/api/health`. No Playwright tests exist yet.

## Goals / Non-Goals

**Goals:**
- Every scenario of the three delta specs is proven by a test:
  - UI behavior in Vitest;
  - flows that cross pages or need the real API in Playwright;
  - the two new endpoints in xUnit integration tests.
- The interface takes the size limit, the error codes and the response shapes from the API. When the API changes them, the build fails, not the page.
- Log text and model text are only ever shown as text.

**Non-Goals:**
- Pushing run updates from the server (server-sent events or WebSockets).
- Keeping unsent input across a page reload or in another tab.
- Translations, and a component library or design system.
- End-to-end tests in WebKit. Safari drops the `Secure` cookie on `http://localhost`.

## Decisions

### Five pages behind one guard

| Path | Page | Session |
|---|---|---|
| `/sign-in` | Sign in. Takes an optional `returnUrl` | public |
| `/register` | Create an account | public |
| `/` | Submit a log | needed |
| `/runs` | The person's runs | needed |
| `/runs/:id` | One run: status, summary, findings and evidence | needed |

Any other path redirects to `/`. A signed-in person who opens `/sign-in` or `/register` is sent to `/`.

When signed in, the header shows links to the submit page and the run list, the account's email address and a sign-out button.

Errors are tied to their fields with `aria-describedby`. A run's status and request errors are announced through an `aria-live` region.

- Alternative: the run list as the start page. Rejected: submitting a log is the main task, and a new account has no runs.

### The app learns the session from `GET /auth/me`

A root `Session` service holds the account as a signal. Its value is unknown at first, then either the account or signed out.
- On the first navigation to a page that needs a session, the `requireSession` guard waits for one `GET /auth/me`.
- A 401 means signed out. The guard then redirects to `/sign-in?returnUrl=<the requested URL>`.
- Any other failure, such as a 502 while the API restarts, also sends that navigation to sign-in, but is not kept: the next navigation asks again.
- Signing in sets the account. Signing out and an ended session clear it. A sign-out that fails with anything but a 401 leaves the person signed in and shows the error, because their session is still alive on the server.

The sign-in page navigates to `returnUrl` only when it is an app path: it starts with one `/`, and not with `//` or `/\`. Otherwise it navigates to `/`.

Creating an account calls `POST /auth/register`, then `POST /auth/sign-in` with the same credentials, because registration does not sign anyone in. It then opens `/`. If the registration succeeded but the sign-in failed, trying again with the same address only signs in: registering again would answer `EMAIL_TAKEN` for the person's own new account.

- Alternative: let each page's first request discover the 401. Rejected: the page would render for a moment, then jump to sign-in, and every page would repeat the handling.
- Alternative: remember a "signed in" flag in localStorage. Rejected: it goes stale when the session ends on the server, and only the API knows the truth.

### An ended session sends the person to sign-in and keeps their draft in memory

An HTTP interceptor watches every API answer except `GET /auth/me`, whose 401 the `Session` service handles. On 401 with code `SIGN_IN_REQUIRED`, it:
1. clears the account;
2. navigates to `/sign-in?returnUrl=<the current URL>&ended=1`, where the page says that the session has ended.

A wrong password answers 401 with `INVALID_CREDENTIALS`, so the sign-in form shows its own message instead.

The submit page keeps its input in a root `SubmissionDraft` service rather than in the component. The draft holds the pasted text, the chosen file, and the account it belongs to. So leaving the page for sign-in loses nothing, and coming back shows the same input.
- The draft is cleared on sign-out and after a successful submission.
- It is also cleared when a different account signs in.
- The submission is not resent automatically after sign-in. The person submits again, which avoids surprise duplicates.

- Alternative: sessionStorage. Rejected: a log can be larger than the browser's storage quota, and logs may contain secrets that would then stay on disk.
- Alternative: a sign-in dialog over the current page. Rejected: a second sign-in form to keep in step with the page, for a case the in-memory draft already covers.

### A paste is sent as text, a file as its own bytes

`POST /api/runs` always gets `Content-Type: text/plain`:
- **A paste** is the body as a string, which the browser encodes as UTF-8.
- **A file** is the body as the `File` object itself, a Blob, with `fileName=<file.name>` in the query string. The browser sends its bytes unchanged, so the API still refuses a file that is not UTF-8 text. `file.text()` would replace invalid bytes with U+FFFD.

The content type is set explicitly for files. Otherwise the browser sends the file's own type, such as `application/octet-stream`, and routing answers 415 without a code.

While a file is chosen, the paste area is disabled and the page shows the file's name and size. Removing the file enables the paste area again.

The submit button is disabled while the paste area is empty and no file is chosen, and while a submission is being sent. A paste of only white space, or an empty file, is sent anyway. The API answers `EMPTY_INPUT` and the page explains it. The browser's idea of white space differs from the API's (`char.IsWhiteSpace`), and only the API's counts.

- Alternative: multipart form data. Rejected: the API takes the log as the body (see the add-api-skeleton design).
- Alternative: read the file in the browser and send its text. Rejected: invalid UTF-8 would be silently changed, and the API could no longer refuse it.

### The submit page checks files against the limit the API reports

A new endpoint, `GET /runs/limits`, answers `200 { "maxSubmissionBytes": 10485760 }`. It reads the same `IngestionOptions` value that `POST /runs` enforces, and it needs a session like every endpoint.

The submit page asks for it the first time it opens and keeps the answer for the app's lifetime. The limit changes only when the API restarts with new settings. A stale value only means the API's 413 answers instead.
- A chosen file larger than the limit gets a message such as "This file is 1.5 MB. The limit is 1 MB." It is never sent.
- The size is in MB of 1,048,576 bytes, with up to two decimals, as in the API's own `INPUT_TOO_LARGE` detail. Sizes under 1 MB, such as a small chosen file or a limit set below 1 MB, are written in KB or bytes, which reads better than "0.01 MB".
- If the limits request fails, the page does not check sizes, and the API's 413 answer covers it.
- Pasted text is not checked (see proposal.md, Non-Goals).

- Alternative: the limit in the `GET /auth/me` answer. Rejected: it mixes account data with ingestion settings.
- Alternative: a `config.json` that nginx serves, written from an environment variable. Rejected: two settings to keep equal, and the API is the one that enforces the limit.
- Alternative: build the limit into the Angular bundle. Rejected: changing the API's setting would need a web rebuild.
- Alternative: the OpenAPI document. Rejected: it is served only in Development.

### The run page polls until the run finishes

The run page reads `GET /api/runs/{id}` when it opens:
- While the run is queued or running, it reads again every second for the first minute, then every 5 seconds.
- Polling stops when the run completes or fails, and when the page is left (`DestroyRef`).
- The status is shown as it changes. The summary and findings appear as soon as a read returns them, without a reload.

Errors while polling:
- A network error or a 5xx shows "Cannot reach the server; retrying" and keeps polling.
- A 401 goes through the ended-session flow, with the run's URL as `returnUrl`.
- A 404 shows that there is no run at this address. So does an id that is not a UUID, without a request: `/runs/limits` would otherwise reach the API's size-limit endpoint.

A failed run shows the plain-language text for its failure reason (see the message table below).

- Alternative: server-sent events. Rejected: a streaming endpoint, nginx buffering settings and reconnect handling. The stand-in finishes in milliseconds, and polling is enough for runs that take seconds. `add-large-log-support` can revisit this for progress per chunk.
- Alternative: a fixed 1-second interval. Rejected: a long Claude run would cost a request per second for its whole duration.

### Findings are sorted and filtered in the browser

The API returns findings in the model's order. The page sorts them by severity rank (critical, error, warning, info) with a stable sort, so the model's order holds within a severity.

The filter is one button per severity present in the run, with its count, plus "All". Choosing a severity shows only its findings. A completed run without findings shows "No problems were found" in place of the list.

- Alternative: sort in the API. Rejected: the stored model order is what attempts and evals refer to, and a filter needs the whole list in the browser anyway.

### The evidence view reads three lines either side of each cited line

Selecting a finding expands it in place:
1. For each cited line N, the page takes the window N−3 to N+3, starting no lower than line 1.
2. It merges windows that overlap or touch.
3. It requests each merged window from `GET /api/runs/{id}/lines?from=&to=`, in parallel. The API clamps a window's end to the last line.
4. It shows each window as a block of numbered lines, in monospace with white space preserved, and marks the cited lines.

Lines already read are kept per run, so selecting a finding again does not request them again. Only one finding is expanded at a time.

The evidence is trimmed by the API's white-space rule (`char.IsWhiteSpace`), not JavaScript's `trim()`: the two differ for U+0085 and U+FEFF. It is then matched exactly, case included, as the validator does. Every occurrence on a cited line is highlighted with `<mark>`. Context lines are never highlighted. A cited line where it does not occur is shown without a highlight.

Lines are split into text segments and rendered with interpolation only, never `innerHTML`. A log can contain markup.

- Alternative: download the whole log once and slice it in the browser. Rejected: up to the size limit transferred and held in memory to show a few lines.
- Alternative: an endpoint that takes a line and a context size. Rejected: less general than a range, and merging windows needs ranges anyway.
- Alternative: a side panel. Rejected: in-place expansion works at every screen width without a second layout.

### The lines endpoint slices the stored array through the ownership filter

`GET /runs/{id}/lines?from=117&to=123` answers:

```json
{ "lines": [ { "number": 117, "text": "..." }, { "number": 118, "text": "..." } ] }
```

- The id is read as text, as in `GET /runs/{id}`. A malformed id, an unknown id and another person's run all get 404 `RUN_NOT_FOUND`.
- `from` and `to` are required whole numbers. The spec names no error for a value the API cannot read, so the framework answers 400 without a code.
- A range is clamped at both ends, so `from` below 1 starts at line 1. A range that ends before it starts, or begins after the last line, returns an empty list.

`RunStore.ReadOwnedLinesAsync(userId, runId, from, to)` runs one SQL statement. It selects `lines[from:to]` from `run_logs`, joined to `runs` through the ownership filter:
- No row means 404.
- A row holds the slice, possibly empty. Its first line's number is the larger of `from` and 1.

PostgreSQL clamps the slice itself, so the spec's clamping needs no code.

- Alternative: load the array and slice it in C#. Rejected: it moves the whole log from the database for every few lines.
- Alternative: refuse a reversed range or one below line 1. Rejected for now: the spec defines no code for it, and clamping gives a harmless answer.

### One table of messages per error code and failure reason

`src/app/messages.ts` maps:
- every API error code to a plain-language message, and, for registration and sign-in, to the field it belongs to: `INVALID_EMAIL` and `EMAIL_TAKEN` to the email field, `PASSWORD_TOO_SHORT` and `PASSWORD_TOO_LONG` to the password field;
- every failure reason to an explanation. `VALIDATION_FAILED`, for example, says that the model's answers did not pass validation and that nothing from them was kept.

A 413 always gets the size message, with or without a code. nginx answers bodies over 12 MB with its own HTML page. An answer without a known code gets a general message.

A request that fails never clears the input. Forms keep their fields, and the submit page keeps its draft.

- Alternative: show the problem's `detail`. Rejected: the API's texts are written for developers. The code is the contract.

### The OpenAPI document describes `code` as a closed list

A schema transformer adds `code` to the `ProblemDetails` schema:
- a string whose `enum` lists every code the API reports;
- a description saying that answers produced by the framework carry none.

The codes come from one list, `ApiErrorCodes`, which the error tables use. A new code therefore appears in the document without a second edit.

The generated types turn the enum into a union, and `messages.ts` is typed as a record over it. A new code fails the web build until it has a message.

- Alternative: describe `code` as a plain string. Rejected: nothing would make the interface handle a new code.
- Alternative: a problem schema per endpoint, listing only its own codes. Rejected: more transformer code, and the interface's messages are global anyway.

### Types are generated from a committed copy of the OpenAPI document

The API's document is committed as `src/web/openapi.json`:
- An integration test compares the served document with it as JSON. It fails with the command that updates the copy: running the tests with `UPDATE_OPENAPI_SNAPSHOT=1` rewrites it.
- `npm run api:generate` runs `openapi-typescript` on the copy and writes `src/app/api/schema.d.ts`, which is committed too.
- The frontend CI job regenerates the types and fails if git shows a difference.

So an API change fails the backend job until the copy is updated, and the frontend job until the types are regenerated.

Requests go through one hand-written `ApiClient`: about ten methods on `HttpClient` with the base path `/api`, typed with the generated schemas. HttpClient uses the fetch backend (`withFetch()`), which sends a Blob body unchanged. The "generated client" in proposal.md is these generated types plus this thin typed layer.

- Alternative: fetch the document from a running API at build time. Rejected: the web image builds from `src/web` alone, and the frontend job would need the backend.
- Alternative: a full client generator, such as ng-openapi-gen or @hey-api/openapi-ts. Rejected:
  - much more generated code, and a generator's style to follow, for ten calls;
  - support for OpenAPI 3.1 (which .NET 10 emits) and for a binary `text/plain` body varies, and the client must get both right.
- Alternative: openapi-fetch. Rejected: it bypasses HttpClient, and with it the interceptor and Angular's HTTP testing tools.
- Alternative: generate the document while the API builds (Microsoft.Extensions.ApiDescription.Server). Rejected: it runs the API's startup during every build, and our startup validates settings. A test is explicit and runs where the API already runs.

### Plain CSS, no component library

Pages use the colour tokens in `styles.css` and component styles within the existing budgets.

- Alternative: Angular Material or PrimeNG. Rejected: weight against the 500 kB initial budget for five pages of forms and lists.
- Alternative: Tailwind. Rejected: build configuration and a new convention for little gain.

### Code placement

- **Web** (`src/web/src/app`):
  - `api/`: `schema.d.ts` (generated), `api-client.ts`, and `api-error.ts`, which reads the status and the code from a failed request.
  - `session/`: `Session`, the `requireSession` guard, the ended-session interceptor and `SubmissionDraft`.
  - `pages/submit/submission-limits.ts`: the limits answer, kept for the app's lifetime.
  - `messages.ts`: messages per code and failure reason, and size formatting.
  - `pages/`: `sign-in/`, `register/`, `submit/`, `runs/` and `run/` (findings and evidence).
  - `app.routes.ts` and `app.config.ts`: `provideHttpClient(withFetch(), withInterceptors(...))`. `app.html` gains the navigation.
- **Api:**
  - `Runs/RunEndpoints.cs`: `GET /runs/limits` and `GET /runs/{id}/lines`, each declaring its answers for the OpenAPI document.
  - `Runs/RunResponses.cs`: `SubmissionLimitsResponse`, `RunLinesResponse` and `RunLineResponse`.
  - `ApiErrorCodes.cs` and the `code` schema transformer, registered in `Program.cs`.
- **Infrastructure:** `RunStore.ReadOwnedLinesAsync`.
- **Tests:** `tests/e2e`, a Node package with Playwright, `playwright.config.ts` and the `*.e2e.ts` tests.
- **Docs:**
  - docs/runs-api.md documents both endpoints.
  - docs/accounts-api.md says that the OpenAPI document lists the codes.
  - `src/web/README.md` gains `api:generate` and the end-to-end tests; the root README's Test section names them.

### Tests at three levels

- **xUnit integration tests** prove the lines and limits scenarios through HTTP against PostgreSQL. `RunsApiFactory` already sets the limit to 1,048,576 bytes, which the limits scenario needs. `AccessPolicyTests` and `OpenApiDocumentTests` gain the two endpoints, and the snapshot comparison joins `OpenApiDocumentTests`.
- **Vitest** proves page behavior with `provideHttpClientTesting()`, Angular's router testing harness and fake timers for polling. This covers answers the stand-in model cannot produce, such as `VALIDATION_FAILED` or `ACCOUNT_LOCKED`, and the 1 MB limit, without reconfiguring a real API.
- **Playwright** runs in Chromium against the Docker Compose stack at http://localhost:8080. It proves the scenarios whose point is crossing pages or the real API:
  - being sent to sign-in and back;
  - a pasted log surviving an ended session (the test clears the cookie);
  - a file that is not UTF-8;
  - a chosen file's name.

  Each test creates its own account, so tests share the stack's database without seeing each other's runs.
- CI runs Playwright in the existing stack job, after the health check. It sets up Node, runs `npm ci` and `playwright install --with-deps chromium` in `tests/e2e`, runs the tests, and uploads the report when they fail.

test-plan.md assigns each scenario to exactly one of these tests.

- Alternative: Playwright against `ng serve` and `dotnet run`. Rejected: two processes and a database to start in CI, while the stack job already starts all three.
- Alternative: Playwright for every scenario. Rejected: slow, timing-dependent for polling, and the stand-in cannot produce most failure answers.
- Alternative: a fifth CI job for Playwright. Rejected: it would build and start the stack a second time.

## Risks / Trade-offs

- [A draft lives only in memory, so a page reload after the session ended loses a pasted log] → Accepted. Browser storage would keep possibly sensitive logs on disk and cannot hold large ones.
- [The draft could be shown to another person who signs in on the same tab] → It is restored only for the account it belongs to, and cleared on sign-out.
- [The lines endpoint returns any range, so one request can ask for a whole log] → Bounded by the size limit. The interface requests only small windows. The user chose on 2026-10-10 not to cap ranges in this change; a cap with its own code can be added later without breaking the interface.
- [Unreadable `from` or `to` values get the framework's 400 without a code] → Only a hand-made request can send them. The interface's general message covers the case.
- [A copy of the OpenAPI document must be updated by hand when the API changes] → The failing test names the command, and CI stops both jobs until the copy and the types match.
- [Hand-written request code can disagree with the API in ways the types do not catch, such as a wrong path] → Typed path keys catch most cases. The Playwright tests use the real API, and the Vitest tests check each request's method, URL and body.
- [Polling keeps the session alive while a run page waits] → Only until the run finishes, and at most one request every 5 seconds after the first minute.
- [ANSI colour codes in a log show up as stray characters, and its escape character is invisible] → Accepted for now. Removing colour codes for display, while keeping highlight positions right, can come later.
- [Two tabs: signing out in one leaves the other showing data until its next request] → That request gets 401, and the tab goes to sign-in.
- [End-to-end tests share one database] → Every test uses its own account, and the stack job removes the volumes afterwards.
- [Signing in on the local stack fails in Safari] → Documented in the README. Playwright runs Chromium only.

## Migration Plan

There is no schema migration. The new endpoints read existing tables. `docker compose up --build` deploys the API and the web image together. Rolling back means deploying the previous images. No data changes in either direction.
