# Test Plan

Every scenario in this change is deterministic, and none depends on model judgment, so there are no eval cases. Three kinds of tests prove them:

- **Integration** (xUnit): the API runs in memory against a PostgreSQL container from Testcontainers. The two new endpoints are tested in `tests/LogAnalyzer.IntegrationTests/Runs/`.
- **Unit** (Vitest): pages are rendered with Angular's TestBed in jsdom, with the app's real routes. Every API answer comes from Angular's HTTP testing controller, and polling runs on fake timers. The tests live next to the code in `src/web/src/app/`.
- **End-to-end** (Playwright): Chromium against the Docker Compose stack, with the real API and the stand-in model, in `tests/e2e/`.

A Vitest or Playwright test name starts with its scenario id in brackets, for example `it('[analysis-ui/clean-log-message] says that no problems were found', ...)`. For these tests, the Test column shows the file and the rest of the name. An xUnit test carries `[Trait("Scenario", "<id>")]` instead, and its Test column shows the class and method.

| Scenario id | Test | Kind | Project |
|---|---|---|---|
| `analysis-history/line-range-is-returned-with-numbers` | `ReadLinesTests.ReadLines_ReturnsRangeWithLineNumbers` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/range-past-the-end-is-clamped` | `ReadLinesTests.ReadLines_ClampsRangePastTheEnd` | integration | LogAnalyzer.IntegrationTests |
| `analysis-history/another-person-s-log-lines-are-not-found` | `ReadLinesTests.ReadLines_AnswersAnotherPersonsRunWithNotFound` | integration | LogAnalyzer.IntegrationTests |
| `log-ingestion/configured-size-limit-is-reported` | `SubmissionLimitsTests.ReadLimits_ReportsConfiguredSizeLimit` | integration | LogAnalyzer.IntegrationTests |
| `analysis-ui/new-account-is-signed-in` | `pages/register/register.spec.ts`: registers, signs in with the same credentials and opens the submit page | unit | src/web |
| `analysis-ui/short-password-is-explained-at-its-field` | `pages/register/register.spec.ts`: shows the password-length message at the password field, whose `aria-describedby` points to it, and keeps the email address | unit | src/web |
| `analysis-ui/taken-email-address-is-explained-at-its-field` | `pages/register/register.spec.ts`: shows the taken-address message at the email field, whose `aria-describedby` points to it, and keeps the email address | unit | src/web |
| `analysis-ui/signed-out-person-is-sent-to-sign-in-and-back` | `session.e2e.ts`: sends a signed-out visit to a run page to sign-in, then back to that run's page with its finding | e2e | tests/e2e |
| `analysis-ui/failed-sign-in-keeps-the-email-address` | `pages/sign-in/sign-in.spec.ts`: says the email address or password is incorrect and keeps the email address | unit | src/web |
| `analysis-ui/locked-account-is-explained` | `pages/sign-in/sign-in.spec.ts`: says sign-in is blocked for up to 15 minutes | unit | src/web |
| `analysis-ui/sign-out-returns-to-sign-in` | `app.spec.ts`: signs out through the API to the sign-in page, and sends a later visit to the run list to sign-in | unit | src/web |
| `analysis-ui/pasted-log-survives-an-ended-session` | `session.e2e.ts`: keeps the pasted log through sign-in after the session cookie is gone | e2e | tests/e2e |
| `analysis-ui/submit-is-disabled-for-empty-input` | `pages/submit/submit.spec.ts`: disables submit while the paste area is empty and no file is chosen | unit | src/web |
| `analysis-ui/oversized-file-is-refused-before-upload` | `pages/submit/submit.spec.ts`: refuses a 1.5 MB file against a 1 MB limit and sends nothing when submit is clicked | unit | src/web |
| `analysis-ui/file-that-is-not-utf-8-is-explained` | `submit.e2e.ts`: explains a file with invalid UTF-8 and creates no run | e2e | tests/e2e |
| `analysis-ui/chosen-file-keeps-its-name` | `submit.e2e.ts`: lists the new run with the chosen file's name | e2e | tests/e2e |
| `analysis-ui/successful-submission-opens-the-run` | `pages/submit/submit.spec.ts`: opens the new run's page when the API accepts the paste | unit | src/web |
| `analysis-ui/results-appear-when-the-run-completes` | `pages/run/run.spec.ts`: shows the summary and findings when a later read returns the completed run | unit | src/web |
| `analysis-ui/failure-reason-is-explained` | `pages/run/run.spec.ts`: explains VALIDATION_FAILED in plain language | unit | src/web |
| `analysis-ui/findings-are-ordered-by-severity` | `pages/run/run.spec.ts`: orders findings critical, error, warning | unit | src/web |
| `analysis-ui/filtering-by-severity` | `pages/run/run.spec.ts`: shows only critical findings after filtering to critical | unit | src/web |
| `analysis-ui/clean-log-message` | `pages/run/run.spec.ts`: says that no problems were found | unit | src/web |
| `analysis-ui/evidence-is-highlighted-in-context` | `pages/run/run.spec.ts`: shows lines 117 to 123 with their numbers and marks the evidence on line 120 | unit | src/web |
| `analysis-ui/past-run-opens-from-the-list` | `pages/runs/runs.spec.ts`: opens a run's page with its findings when it is selected | unit | src/web |
| `analysis-ui/server-side-size-rejection-keeps-the-input` | `pages/submit/submit.spec.ts`: explains INPUT_TOO_LARGE and keeps the pasted text | unit | src/web |

## Fixtures

- **Integration:**
  - `RunsApiFactory` (existing) sets the limit to 1,048,576 bytes, scripts the model and removes the worker. The limits scenario reads that configured value.
  - `PostgresApiFactory` (existing, as `OpenApiDocumentTests` and `AccessPolicyTests` use it) leaves `Ingestion:MaxSubmissionBytes` unset, for the default limit of 10,485,760 bytes. `SubmissionLimitsTests` takes it as a second class fixture beside `RunsApiFactory`. `RunsClient.SignInNewPersonAsync` accepts any `PostgresApiFactory`.
  - `RunsClient` gains two methods:
    - `ReadLinesAsync(string id, string? from, string? to, CancellationToken)`. It sends each value as given, and leaves out `from` or `to` when it is null, as `ListAsync` does, so a test can send a malformed id and an unreadable or missing value.
    - `ReadLimitsAsync(CancellationToken)`.
  - Two signed-in people from `RunsClient.SignInNewPersonAsync`, for the ownership scenario.
  - A 500-line log built in the test, where line N reads `line N of 500`, so an off-by-one shows. It is submitted as a paste with `SubmitAcceptedAsync`. Lines are stored at submission, so the scenarios need no processing.
  - `ProblemAssert.HasCodeAsync` for 404 `RUN_NOT_FOUND`.
  - The OpenAPI copy `src/web/openapi.json`, found by walking up from the test's directory to the folder that holds `LogAnalyzer.slnx`. With `UPDATE_OPENAPI_SNAPSHOT=1`, the test rewrites it instead of comparing.
- **Unit (Vitest):**
  - `src/app/testing/test-app.ts`, a helper that mirrors `app.config.ts`. It provides:
    - the app's routes and the ended-session interceptor;
    - `provideHttpClient(withInterceptors(...))` and `provideHttpClientTesting()`.

    `src/app/testing/` is added to `exclude` in tsconfig.app.json and to `include` in tsconfig.spec.json, so `npm run build`, in the frontend job and in the web image, never type-checks test code against `types: []`.
  - `HttpTestingController` answers each request, and `verify()` fails a test that sent one more than expected.
  - Rendering:
    - `RouterTestingHarness` drives navigation in page tests.
    - `app.spec.ts` instead creates `App` with `TestBed.createComponent` and navigates with `Router.navigateByUrl`, because the header belongs to `App` and the harness renders only a router outlet.
  - Fake timers:
    - `vi.useFakeTimers()` with `vi.advanceTimersByTimeAsync` drives polling.
    - Every spec that calls `vi.useFakeTimers()` restores real timers in `afterEach` with `vi.useRealTimers()`. The builder runs Vitest with `isolate: false`, so spec files that share a worker share its timers, and Vitest does not restore them between files.
    - While fake timers are on, a test renders by advancing them (`vi.advanceTimersByTimeAsync(0)`) or with `fixture.detectChanges()`, not with `whenStable()`. Zoneless change detection waits for a timer or an animation frame.
  - A test starts signed in or signed out in three steps:
    1. It starts the navigation without awaiting it.
    2. It waits for the guard's request with `vi.waitFor(() => httpTesting.expectOne('/api/auth/me'))`, and answers it with the account or with 401 `SIGN_IN_REQUIRED`.
    3. Only then does it await the navigation.

    Until the guard has its answer, the router counts the navigation as a pending task, so neither the navigation nor `whenStable()` resolves.
  - `src/app/testing/answers.ts`, canned API answers typed with the generated schemas:
    - two accounts, `ana@example.com` and `bob@example.com`, the second for the draft test;
    - runs:
      - queued;
      - running, with the queued run's id;
      - completed with a warning, a critical and an error finding, in that order;
      - completed with critical and warning findings;
      - completed with an error, an info, a second error and a critical finding, in that order;
      - completed without findings;
      - completed with a summary and a finding whose texts contain `<b>bold</b>`;
      - failed with `VALIDATION_FAILED`;
      - completed with a finding that cites line 120 with the evidence "connection pool exhausted", and a second finding that cites lines 40 and 60;
    - lines 117 to 123 of that run, where line 120 holds the evidence inside a longer line, so the mark covers only the excerpt; and lines 37 to 43 and 57 to 63;
    - a run list page with two runs, newest first:
      - the completed run with a warning, a critical and an error finding, named `orders-api.log`, with highest severity critical (the run the selection test opens);
      - an older queued paste with no file name and no highest severity;
    - the submission answer `{ "id": "<the queued run's id>", "status": "queued" }`;
    - problem details with `code` for `PASSWORD_TOO_SHORT`, `EMAIL_TAKEN`, `INVALID_CREDENTIALS`, `ACCOUNT_LOCKED`, `INPUT_TOO_LARGE`, `EMPTY_INPUT`, `RUN_NOT_FOUND` and `SIGN_IN_REQUIRED`; nginx's 413 HTML page without a code; and a 503 without a body;
    - the limits answer `{ "maxSubmissionBytes": 1048576 }`;
    - the five failure reasons from docs/runs-api.md, for `messages.spec.ts`: `VALIDATION_FAILED`, `MODEL_UNAVAILABLE`, `MODEL_CONFIGURATION_ERROR`, `INTERRUPTED` and `INTERNAL_ERROR`. The OpenAPI document types `failureReason` as a plain string, so the generated types cannot list them.
  - Order of work:
    - `answers.ts` takes its types from `src/app/api/schema.d.ts`. `npm run api:generate` writes that file from `src/web/openapi.json`; it is a new script, with `openapi-typescript` as a devDependency.
    - The copy describes `SubmissionLimitsResponse`, `RunLinesResponse` and the `code` enum only after the two endpoints and the schema transformer exist and the snapshot test has run with `UPDATE_OPENAPI_SNAPSHOT=1`. So that work and `api:generate` come before any Vitest test that uses `answers.ts`.
    - The unit-test builder compiles every spec in one build and runs none when the build fails. A new spec must therefore compile when it first fails: whatever it imports, such as a service under test or the interceptor the helper registers, exists first, at least as a stub.
  - Files built in the test: `new File([new Uint8Array(1_572_864)], 'big.log')`, a small `orders-api.log` and an empty `new File([], 'empty.log')`.
  - Inputs built in `evidence.spec.ts`:
    - windows:
      - cited line 2 (window 1 to 5);
      - lines 10 and 17, whose touching windows 7 to 13 and 14 to 20 merge into 7 to 20;
      - lines 30 and 38, whose windows 27 to 33 and 35 to 41 stay apart;
      - lines 50 and 55, whose overlapping windows merge into 47 to 58;
    - evidence and lines:
      - an evidence that starts with U+0085 and ends with U+FEFF, on a line that holds the excerpt with its U+FEFF;
      - a cited line with the evidence twice;
      - a context line that also holds the evidence;
      - a cited line without it;
      - a cited line with it in other letter case;
      - a line with `<b>` markup.
  - The oversized-file test:
    - It answers `GET /api/runs/limits` with 1,048,576 bytes before it chooses `big.log`, and checks the message.
    - It then clicks submit, so `verify()` fails it if the file was sent.
    - An `expectNone` for that POST needs a function matcher, such as `req => req.method === 'POST' && req.url.split('?')[0] === '/api/runs'`. A string or `{ method, url }` matcher compares `urlWithParams`, so it misses `/api/runs?fileName=big.log`.
  - The sign-out test in `app.spec.ts` clicks the header's sign-out button. It expects exactly one `POST /api/auth/sign-out`, answered 204, before it checks the sign-in page. `verify()` cannot catch a request that was never sent.
- **End-to-end (Playwright):**
  - `playwright.config.ts`:
    - `testMatch: '**/*.e2e.ts'`, because Playwright's default only finds `*.spec.*` and `*.test.*` files;
    - Chromium only;
    - `baseURL` from `BASE_URL` (default http://localhost:8080);
    - `trace: 'retain-on-failure'` and `retries: 0`;
    - `reporter: [['list'], ['html', { open: 'never' }]]`, so a run writes the `playwright-report/` folder that CI uploads.
  - A helper creates a fresh account per test through `POST /api/auth/register`, with a unique email address, then signs in through the sign-in page.
  - Logs:
    - a three-line log whose second line is an error line (`2026-10-10T09:00:04Z fail: Orders.Api[0] Payment for order 42 failed`), so the stand-in returns one finding, `Error logged on line 2`, at once;
    - the same text as the file `orders-api.log`;
    - a file `not-utf-8.log` whose bytes are not valid UTF-8 (`66 6f ff 0a`).

    Both files are passed to `setInputFiles` as buffers with `mimeType: 'application/octet-stream'`, for example `{ name: 'not-utf-8.log', mimeType: 'application/octet-stream', buffer }`. A page that sends a file with its own type, instead of an explicit `Content-Type: text/plain`, then gets routing's 415, and the tests fail.
  - `context.clearCookies()` ends a session from the browser's side.
  - The signed-out test:
    - It submits the three-line log while signed in and reads the run id from the URL.
    - It then opens `/runs/<id>` with `page.goto` in a new browser context (`browser.newContext()`, which inherits `baseURL`), so neither the session cookie nor the app's in-memory account is left. Navigating inside the first page instead would reach sign-in through the ended-session interceptor, not the guard.
    - After it signs in there with the same credentials, it checks the URL and the stand-in's finding `Error logged on line 2`.
  - The invalid-UTF-8 test checks that no run was created through the run list's own request:
    - It opens `/runs` and reads the `GET /api/runs` answer with `page.waitForResponse`, matching the path `/api/runs` exactly so that `GET /api/runs/limits` does not count. It expects an empty `items` list.
    - It never relies on the absence of a row, which already holds before the list loads. The account is fresh, so any item would be the rejected file's run.
  - Tests wait with Playwright's auto-waiting assertions, never with fixed sleeps.

## Tests without a scenario

These pin rules from design.md that no scenario names. None carries a scenario id of this change.

- **Integration:**
  - `ReadLinesTests`:
    - a malformed id and an unknown id get the same 404 `RUN_NOT_FOUND` body as another person's run;
    - `from` below 1 starts at line 1;
    - a range that ends before it starts, and one that begins after the last line, return an empty list;
    - a missing or non-numeric `from` or `to` gets 400 without a code.
  - `SubmissionLimitsTests`: without the setting, the reported limit is 10,485,760 bytes.
  - `OpenApiDocumentTests`:
    - rows for `GET /runs/limits` (200 with `SubmissionLimitsResponse`; 401) and `GET /runs/{id}/lines` (200 with `RunLinesResponse`; 400, 401 and 404);
    - the `ProblemDetails` schema's `code` enum lists exactly the codes in the error tables of docs/accounts-api.md and docs/runs-api.md;
    - the served document equals `src/web/openapi.json` as JSON. A difference fails with a message that names `UPDATE_OPENAPI_SNAPSHOT=1`.
  - `AccessPolicyTests.NonPublicEndpoints_RefuseAnonymousRequests` (existing; keeps its `user-accounts/non-public-endpoints-refuse-anonymous-requests` tag) reaches the two new endpoints through `EndpointDataSource`, without changes. The fallback policy refuses the request before `from` and `to` are bound, so both answer 401 `SIGN_IN_REQUIRED`.
- **Unit (Vitest):**
  - `api/api-client.spec.ts`: each method's HTTP method, URL, query and body. A file is sent as the same `File` object with `Content-Type: text/plain` and `fileName`; a paste as a string with `Content-Type: text/plain`.
  - `api/api-error.spec.ts`: reads the status and the `code` from problem details; nginx's 413 HTML page has no code.
  - `messages.spec.ts`:
    - every failure reason has an explanation;
    - 1,048,576 bytes is "1 MB", 1,572,864 is "1.5 MB" and 1,500,000 is "1.43 MB";
    - `INVALID_EMAIL` and `EMAIL_TAKEN` belong to the email field, and `PASSWORD_TOO_SHORT` and `PASSWORD_TOO_LONG` to the password field;
    - an answer without a known code, such as the framework's 400 or a 500, gets the general message.
  - `session/require-session.spec.ts`: several navigations cost one `GET /auth/me`.
  - `session/session-ended.interceptor.spec.ts`: `SIGN_IN_REQUIRED` navigates to sign-in with the current URL and `ended=1`; `INVALID_CREDENTIALS` from sign-in and a 401 from `GET /auth/me` do not.
  - `session/submission-draft.spec.ts`:
    - the draft is cleared on sign-out, after a successful submission and when another account signs in;
    - it is kept when the same account signs in again;
    - a chosen file is kept like pasted text, as the same `File` object;
    - nothing is written to `localStorage` or `sessionStorage`.
  - `pages/submit/submit.spec.ts`:
    - while a file is chosen, the paste area is disabled and the page shows the file's name and size; removing the file enables the paste area again;
    - once the limits answer has arrived, opening the submit page again sends no second `GET /runs/limits`;
    - a failed limits request leaves files unchecked;
    - a paste of only spaces and line breaks enables submit and is sent unchanged, and so is an empty file; `EMPTY_INPUT` gets its message, and the input stays;
    - a 413 without a code gets the size message;
    - the button is disabled while a submission is being sent;
    - a request error appears in an `aria-live` region.
  - `pages/run/run.spec.ts`:
    - polling:
      - it polls every second for a minute, then every 5 seconds;
      - it stops when the run finishes and when the page is destroyed;
      - the status shown changes from queued to running as reads return them;
      - a network error or a 503 shows the retry notice, and polling goes on;
      - a 404 says there is no run at this address;
      - the status, the retry notice and the 404 message are in an `aria-live` region;
    - findings:
      - findings of the same severity keep the model's order, and info comes last;
      - the filter has one button per severity present, with its count, plus All, which shows every finding again;
      - a summary, and a finding's title, explanation and suggested action, that contain `<b>` markup render as text;
    - evidence:
      - only one finding is expanded at a time;
      - selecting a finding again sends no second lines request;
      - a finding that cites lines 40 and 60 requests lines 37 to 43 and 57 to 63 before either answer arrives.
  - `pages/run/evidence.spec.ts`:
    - windows start no lower than line 1, and merge when they overlap or touch;
    - the evidence is trimmed by the API's white-space rule (U+0085 trimmed, U+FEFF kept);
    - every occurrence on a cited line gets a `<mark>`, and context lines never do;
    - each cited line is marked as cited, and one without the evidence has no `<mark>`;
    - an occurrence in other letter case, such as "Connection pool exhausted" for the evidence "connection pool exhausted", gets no `<mark>`;
    - a line with `<b>` markup renders as text.
  - `pages/runs/runs.spec.ts`: each run shows its file name ("Pasted log" when it has none), submission time, status and highest severity, newest first.
  - `pages/sign-in/sign-in.spec.ts`:
    - a signed-in person who opens `/sign-in` is sent to `/`;
    - `ended=1` shows that the session has ended;
    - after signing in with `returnUrl` set to `//evil.example`, `/\evil.example` or `https://evil.example`, the page opens `/`;
    - a request error appears in an `aria-live` region.
  - `pages/register/register.spec.ts`: a signed-in person who opens `/register` is sent to `/`.
  - `app.spec.ts` (the existing tests stay):
    - only when signed in, the header shows links to the submit page (`/`) and the run list (`/runs`), the email address and sign-out;
    - a signed-in person who opens an unknown path, such as `/nope`, lands on `/`.

## Checks in CI

- **Frontend job:**
  - `npm run api:generate`, then `git diff --exit-code`, so the committed types match the committed OpenAPI copy.
  - The production build (`npm run build`), already in the job, fails when `messages.ts`, a record over the generated `code` union, has no message for a code.
- **Stack job:** the Playwright tests run after the health check. Their report is uploaded when they fail.
- **Specs job:**
  - Until the change is archived, the coverage check counts these 25 scenarios in its pending total for all active changes, with how many already have tests, and never fails on them. The report shows totals, not ids.
  - Once the change is archived, they are archived scenarios, and any of them without a tagged test fails the check as MISSING.
  - A test tagged with an id that matches no archived or pending scenario fails the check as UNKNOWN.
