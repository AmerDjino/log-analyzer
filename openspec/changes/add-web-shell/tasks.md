# Tasks

## 1. The lines and limits endpoints

- [ ] 1.1 On a branch that holds this plan and starts from a `main` that includes `add-api-skeleton` (PR #7), write the failing integration tests below, with the fixtures from test-plan.md; verify each fails because the routes do not exist. Today a signed-in `GET /runs/limits` answers 404 `RUN_NOT_FOUND`, because `/runs/{id}` takes "limits" as an id, and `GET /runs/{id}/lines` answers routing's 404.
  - `ReadLinesTests` for the three analysis-history scenarios, and the `ReadLinesTests` checks listed under "Tests without a scenario".
  - `SubmissionLimitsTests.ReadLimits_ReportsConfiguredSizeLimit` with `RunsApiFactory`, and the default-limit test with `PostgresApiFactory` as a second class fixture.
  - `RunsClient.ReadLinesAsync` and `ReadLimitsAsync`.
  - The `OpenApiDocumentTests` rows for `GET /runs/limits` and `GET /runs/{id}/lines`.
- [ ] 1.2 Add `RunStore.ReadOwnedLinesAsync` and `GET /runs/{id}/lines`, as listed below; verify `ReadLinesTests` and its `OpenApiDocumentTests` row pass, and `AccessPolicyTests` still passes without changes
  - `ReadOwnedLinesAsync` is one SQL statement that selects `lines[from:to]` from `run_logs`, joined to `runs` through the ownership filter.
  - The endpoint answers `RunLinesResponse` with `RunLineResponse` items. It reads the id as text, and `from` and `to` as required whole numbers.
  - It declares 200, 400, 401 and 404 for OpenAPI.
- [ ] 1.3 Add `GET /runs/limits`, answering `SubmissionLimitsResponse` from `IngestionOptions`, with 200 and 401 declared for OpenAPI; verify `SubmissionLimitsTests` and its `OpenApiDocumentTests` row pass, and `AccessPolicyTests` and `ReadRunTests` still pass without changes
- [ ] 1.4 Document both endpoints in docs/runs-api.md: their paths and answers, clamping, the framework's 400 for an unreadable `from` or `to`, and `RUN_NOT_FOUND`; verify every status and code the group's tests assert appears in it

## 2. Error codes in the OpenAPI document, and its committed copy

- [ ] 2.1 Write the failing `OpenApiDocumentTests` below; verify the enum test fails because `ProblemDetails` has no `code`, and the copy test fails because `src/web/openapi.json` does not exist
  - The `ProblemDetails` schema's `code` enum lists exactly the codes in the error tables of docs/accounts-api.md and docs/runs-api.md.
  - The served document equals `src/web/openapi.json` as JSON. A difference fails with a message that names `UPDATE_OPENAPI_SNAPSHOT=1`, and with that variable set the test rewrites the copy instead.
- [ ] 2.2 Add `ApiErrorCodes` and the `code` schema transformer, as listed below; verify the enum test passes, and every other integration test except the copy test still passes
  - `ApiErrorCodes` is the one list of codes that `AccountErrors`, `RunErrors` and `Utf8RequestBodies` use.
  - The transformer, registered in `Program.cs`, adds `code` to `ProblemDetails` as a string enum of those codes. Its description says that answers the framework produces carry no `code`.
- [ ] 2.3 Write `src/web/openapi.json` by running the copy test with `UPDATE_OPENAPI_SNAPSHOT=1`; verify the test then passes without the variable, and the copy holds `/runs/limits`, `/runs/{id}/lines` and the `code` enum
- [ ] 2.4 Say in docs/accounts-api.md that the OpenAPI document lists every code, and in src/web/README.md how to update the copy after an API change; verify the documented command rewrites the copy and the test passes afterwards

## 3. Generated types and test scaffolding in the web app

- [ ] 3.1 Add `openapi-typescript` and the `api:generate` script, as listed below; run the script and stage its output with `git add` (it is committed with the pull request); verify `npm ci` succeeds from an empty `node_modules`, `npm run api:generate` followed by `git diff --exit-code -- src/app/api/schema.d.ts` is clean, `npm run build` passes, and `docker compose build web` succeeds
  - `openapi-typescript` at an exact version, as a devDependency.
  - `"overrides": { "openapi-typescript": { "typescript": "$typescript" } }` in package.json. Every 7.x release declares the peer `typescript ^5.x`, while Angular 22 needs TypeScript 6.0, so a plain install stops with ERESOLVE.
  - No `--legacy-peer-deps`, `--force` or `.npmrc`: the frontend job and the web Dockerfile run plain `npm ci`, and the Dockerfile copies only package.json and package-lock.json before it.
  - If the generator fails under TypeScript 6.0, stop and revisit design.md's choice of generator with the user.
  - The `api:generate` script writes `src/app/api/schema.d.ts` from `openapi.json`.
- [ ] 3.2 Add a step to the frontend CI job that runs `npm run api:generate`, then `git diff --exit-code`; verify locally that `npm run api:generate` followed by `git diff --exit-code -- src/app/api/schema.d.ts` is clean (the CI step's unscoped `git diff --exit-code` runs on a clean checkout)
- [ ] 3.3 Add the test helpers, as listed below, and exclude `src/app/testing/` in tsconfig.app.json and include it in tsconfig.spec.json; verify `npm test -- --watch=false` and `npm run build` both pass
  - `src/app/testing/test-app.ts` provides the app's routes, `provideHttpClient(withInterceptors([]))` and `provideHttpClientTesting()`. The ended-session interceptor joins them in 5.3.
  - `src/app/testing/answers.ts` holds the canned answers from test-plan.md's Unit fixtures, typed with `schema.d.ts`.
- [ ] 3.4 Document the scaffolding, as listed below; verify the documented commands run as written
  - src/web/README.md: `npm run api:generate`, why package.json overrides openapi-typescript's TypeScript peer, and the `src/app/testing/` folder.
  - The root README's CI table: the new frontend gate.
  - The root README's Test section: `npm --prefix src/web run api:generate`, then `git diff --exit-code src/web/src/app/api/schema.d.ts`.

## 4. The API client, error reading and messages

- [ ] 4.1 Write the failing `api/api-client.spec.ts`, `api/api-error.spec.ts` and `messages.spec.ts` from test-plan.md, with stubs of `ApiClient`, `api-error.ts` and `messages.ts` that compile; verify the suite compiles and each new test fails on its assertions
- [ ] 4.2 Implement `ApiClient`, as listed below; verify `api-client.spec.ts` passes
  - One method per endpoint the pages use, on `HttpClient` with the base path `/api` and the generated types.
  - A paste is sent as a string, and a file as the same `File`, both with `Content-Type: text/plain`. A file also sends `fileName`.
  - Add `provideHttpClient(withFetch())` to the providers in app.config.ts, which has none yet. 5.3 adds `withInterceptors` with the ended-session interceptor.
- [ ] 4.3 Implement `api-error.ts` and `messages.ts`, as listed below; verify `api-error.spec.ts`, `messages.spec.ts` and the whole web suite (`npm test -- --watch=false`) pass, and `npm run build` passes, fails when one code's entry is removed from messages.ts, and passes again once the entry is restored
  - `api-error.ts` reads the status and the `code` from a failed request.
  - `messages.ts` is a record over the generated code union. It gives the account codes their fields, explains the five failure reasons, holds a general message, and writes sizes in MB of 1,048,576 bytes with up to two decimals.

## 5. Sign-in, account creation, sign-out and the session guard

- [ ] 5.1 Write the failing tests below, with stubs that compile of `Session`, `requireSession`, the ended-session interceptor and the sign-in and register pages; verify the suite compiles, the existing `app.spec.ts` tests still pass, and every new test fails, most because `app.routes.ts` has no routes yet, so no page opens and no guard sends `GET /api/auth/me`. A check that something does not happen, such as the interceptor leaving `INVALID_CREDENTIALS` and a 401 from `GET /auth/me` alone, may already pass against the stubs, as a guard.
  - `pages/register/register.spec.ts`: the three account-creation scenarios, and a signed-in person who opens `/register` is sent to `/`.
  - `pages/sign-in/sign-in.spec.ts`: the failed-sign-in and locked-account scenarios, and its checks listed under "Tests without a scenario".
  - `app.spec.ts`: the sign-out scenario, the header links and an unknown path.
  - `session/require-session.spec.ts` and `session/session-ended.interceptor.spec.ts`.
- [ ] 5.2 Add `Session`, which holds the account as a signal read once from `GET /auth/me`, the `requireSession` guard and the routes from design.md, with placeholder pages for `/`, `/runs` and `/runs/:id` that later groups fill; verify `require-session.spec.ts` and the routing tests in `app.spec.ts` pass
- [ ] 5.3 Add the ended-session interceptor and register it in app.config.ts (`withInterceptors`) and test-app.ts; verify `session-ended.interceptor.spec.ts` passes
  - On a 401 `SIGN_IN_REQUIRED` from any request except `GET /auth/me`, it clears the account and navigates to `/sign-in?returnUrl=<the current URL>&ended=1`.
- [ ] 5.4 Add the sign-in page, as listed below; verify `sign-in.spec.ts` passes
  - Messages from messages.ts appear in an `aria-live` region, and the email address is kept.
  - It shows the session-ended note, and checks that `returnUrl` is an app path.
  - Signed-in people are sent to `/`.
- [ ] 5.5 Add the register page, as listed below; verify `register.spec.ts` passes
  - It sends `POST /auth/register`, then `POST /auth/sign-in` with the same credentials.
  - Field errors are tied to their fields with `aria-describedby`, and the email address is kept.
  - Signed-in people are sent to `/`.
- [ ] 5.6 Add the header's links, email address and sign-out button, shown only when signed in. Sign-out sends `POST /auth/sign-out`, clears the account and shows the sign-in page; verify `app.spec.ts` and the whole web suite (`npm test -- --watch=false`) pass, and `npm run build` stays within its budgets
- [ ] 5.7 Document in the README's Run section how to create an account and sign in at http://localhost:8080, in Chrome, Edge or Firefox, because Safari drops the `Secure` session cookie on `http://localhost`; verify the steps work on the running stack

## 6. The run page: status, findings and evidence

- [ ] 6.1 Write the failing `pages/run/run.spec.ts`, for the six run-page scenarios and its checks listed under "Tests without a scenario", and `pages/run/evidence.spec.ts`, with a run page and evidence helpers that compile; verify the suite compiles and each new test fails on its assertions
- [ ] 6.2 Make the run page follow the run, as listed below; verify the polling, status and failure tests in `run.spec.ts` pass, except the results-appear scenario test, which 6.3 makes pass
  - It reads the run when the page opens.
  - While the run is queued or running, it reads again every second for the first minute, then every 5 seconds.
  - It stops when the run completes or fails, and when the page is left.
  - It shows the retry notice for a network error or a 5xx, the 404 message, and the failure reason's explanation.
  - The status and notices are in an `aria-live` region.
- [ ] 6.3 Show the summary and findings, as listed below; verify the results-appear, ordering, filtering, clean-log and markup tests pass
  - Findings are sorted by severity rank, with a stable sort.
  - The filter has one button per severity present, with its count, plus All.
  - A run without findings says "No problems were found".
  - Every model text is shown as text.
- [ ] 6.4 Add the evidence helpers and view, as listed below; verify `evidence.spec.ts` and every test in `run.spec.ts` pass, `npm run build` passes within its budgets, and the whole web suite (`npm test -- --watch=false`) still passes, with any earlier spec whose tests open `/runs/:id` signed in now answering the page's `GET /api/runs/{id}`
  - Windows of three lines either side of each cited line, starting no lower than line 1, merged when they overlap or touch.
  - Merged windows requested in parallel from `GET /runs/{id}/lines`, and lines kept per run.
  - One finding expanded at a time.
  - Each window shown as a block of numbered lines, in monospace with white space preserved, with its cited lines marked. Its text is rendered by interpolation only, never `innerHTML`.
  - The evidence trimmed by the API's white-space rule, and every exact occurrence on a cited line put in a `<mark>`.

## 7. The run list

- [ ] 7.1 Write the failing `pages/runs/runs.spec.ts`, for the past-run scenario and its checks listed under "Tests without a scenario", with a run list page that compiles; verify each test fails on its assertions
- [ ] 7.2 Add the run list, as listed below; verify `runs.spec.ts` passes, with the run page showing the selected run's findings, `npm run build` passes within its budgets, and the whole web suite (`npm test -- --watch=false`) still passes, with any earlier spec whose tests open `/runs` signed in now answering its `GET /api/runs`
  - The first page of `GET /runs`, newest first as the API returns it. Paging is not part of this change.
  - Each run's file name ("Pasted log" when it has none), submission time, status and highest severity.
  - Selecting a run opens its page.

## 8. The submit page, the draft and the end-to-end flows

- [ ] 8.1 Write the failing `pages/submit/submit.spec.ts`, for the four submit scenarios and its checks listed under "Tests without a scenario", and `session/submission-draft.spec.ts`, with a submit page and `SubmissionDraft` that compile; verify the suite compiles and each new test fails on its assertions. The exceptions are checks a stub can already satisfy, such as nothing being written to `localStorage` or `sessionStorage` and the draft being kept for the same account: they may pass, as guards.
- [ ] 8.2 Create the end-to-end package and write the four failing Playwright tests, as listed below; verify, against `docker compose up --build --wait`, that Playwright finds all four and each fails because the page cannot submit a log yet
  - `tests/e2e/package.json` with `@playwright/test` at an exact version, and its committed `package-lock.json`.
  - `playwright.config.ts` and the account helper, as test-plan.md describes.
  - `tests/e2e/.gitignore` for `test-results/` and `playwright-report/`.
  - `session.e2e.ts` and `submit.e2e.ts`, for the four end-to-end scenarios.
- [ ] 8.3 Add `SubmissionDraft`: the pasted text, the chosen file and the account they belong to, kept in memory only, and cleared on sign-out and when another account signs in; verify the sign-out, other-account, same-account, file and storage tests in `submission-draft.spec.ts` pass
- [ ] 8.4 Add the submit page, as listed below; verify `submit.spec.ts` and `submission-draft.spec.ts` pass, and the whole web suite (`npm test -- --watch=false`) still passes, with every earlier spec whose tests land on `/` signed in (such as `register.spec.ts`, `sign-in.spec.ts` and `app.spec.ts`) now answering the submit page's `GET /api/runs/limits`
  - The paste area and file picker, bound to the draft. While a file is chosen, its name and size are shown and the paste area is disabled.
  - Submit is disabled while the paste area is empty and no file is chosen, and while a submission is being sent. A paste of only white space and an empty file are sent anyway, unchanged, and the API's `EMPTY_INPUT` answer gets its message.
  - Limits:
    - `GET /runs/limits` is sent the first time the submit page opens. Once its answer has arrived, it is kept for the app's lifetime, so a later visit sends no second request.
    - A file over the limit gets the size message and is never sent.
    - If the limits request fails, files are not checked, and the API's 413 answer covers them.
  - A paste is sent as text, and a file as the same `File` with `Content-Type: text/plain` and `fileName`.
  - A 202 clears the draft and opens the run's page.
  - Errors get their message from messages.ts in an `aria-live` region, and the input stays. A 413 gets the size message with or without a code, since nginx's own 413 page has none.
- [ ] 8.5 Rebuild the stack and run the Playwright tests; verify all four pass
- [ ] 8.6 Add the Playwright steps to the stack job, as listed below; verify the same commands pass locally against the stack
  - Place them between "API health through nginx" and "Show container logs", so the container logs still print on a failure and "Stop the stack" still runs last.
  - `actions/setup-node` with `node-version: ${{ env.NODE_VERSION }}`, `cache: npm` and `cache-dependency-path: tests/e2e/package-lock.json`.
  - `npm ci`, `npx playwright install --with-deps chromium` and `npx playwright test`, each with `working-directory: tests/e2e`.
  - `actions/upload-artifact` with `if: failure()` and the paths `tests/e2e/playwright-report/` and `tests/e2e/test-results/`. The paths start at the repository root, because `working-directory` applies only to `run` steps.
- [ ] 8.7 Document the end-to-end tests in the root README's Test section, CI table and repository layout, and in src/web/README.md: start the stack, then run `npm ci`, `npx playwright install chromium` and `npx playwright test` in `tests/e2e`, with `BASE_URL` to point elsewhere; verify the documented commands run as written

## 9. Integration checks

- [ ] 9.1 On a fresh database, check the whole flow by hand, as listed below; verify each step behaves as the specs say and `/api/health` stays Healthy
  - Reset the stack with `docker compose down --volumes`, then `docker compose up --build --wait`.
  - In Chrome at http://localhost:8080: create an account; submit `orders-api-40.log` as a file and as a paste; follow a run to completion; filter its findings.
  - Open a finding's evidence and see its lines numbered and in monospace.
  - Browse the run list, then sign out.
- [ ] 9.2 Run the full test suite (`dotnet test`, `npm --prefix src/web test -- --watch=false`, `npm --prefix src/web run build` and the Playwright tests), `node scripts/check-scenario-coverage.mjs` and `openspec validate add-web-shell --strict`; verify all pass and the coverage report shows the 25 scenarios of this change as already having tests

## Workflow follow-up

- Run `/opsx:verify add-web-shell`, fix what it finds, and run `/opsx:archive add-web-shell` in the same pull request.
- The user merges the pull request once all four CI jobs pass, including the stack job's Playwright steps; stop at ready to merge.
