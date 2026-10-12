# web

The Angular 22 interface for the log analyzer: standalone components, signals, zoneless change detection, Vitest for unit tests.

## Commands

Run these in `src/web`.

| Command | What it does |
|---|---|
| `npm install` | Installs dependencies. The first run creates `package-lock.json`; commit it, because CI and the Docker build use `npm ci`. |
| `npm start` | Serves the app at http://localhost:4200 and forwards `/api` to the API at http://localhost:5080 (see `proxy.conf.json`). |
| `npm test` | Runs the Vitest unit tests (watch mode locally; add `-- --watch=false` for a single run). |
| `npm run build` | Builds for production into `dist/web/browser`. |
| `npm run api:generate` | Writes the API's TypeScript types, `src/app/api/schema.d.ts`, from `openapi.json`. |

## The API's OpenAPI document

`openapi.json` is a copy of the document the API serves at `/openapi/v1.json`. After an API change, update it from the repository root (Docker must be running):

```sh
UPDATE_OPENAPI_SNAPSHOT=1 dotnet test tests/LogAnalyzer.IntegrationTests --filter FullyQualifiedName~OpenApiDocumentTests
```

Until then, `OpenApiDocumentTests.Document_EqualsWebCopy` fails and names this command.

Then run `npm run api:generate`, and commit both files. CI regenerates the types and fails when they differ from the committed ones.

The types come from [openapi-typescript](https://openapi-ts.dev/). Every 7.x release declares the peer `typescript ^5.x`, while Angular 22 needs TypeScript 6.0, so `package.json` overrides that peer with the app's own TypeScript version (`"overrides"`). Do not install with `--legacy-peer-deps` or `--force` instead: CI and the Docker build run a plain `npm ci`.

## Test helpers

`src/app/testing/` holds what the unit tests share: `test-app.ts` gives the app's routes and HTTP setup with every request answered through `HttpTestingController`, and `answers.ts` holds canned API answers typed with the generated types. The production build leaves the folder out (`tsconfig.app.json`).

## End-to-end tests

The Playwright tests in `tests/e2e` drive this app in Chromium against the Docker Compose stack, with the real API. From the repository root:

```sh
docker compose up --build --wait
cd tests/e2e
npm ci
npx playwright install chromium
npx playwright test
```

They use http://localhost:8080; set `BASE_URL` to test another address. CI runs them in the Docker Compose stack job.

## Tests and scenario ids

When a test proves a spec scenario, put the scenario id in brackets in the test name, so the coverage check in `scripts/check-scenario-coverage.mjs` can find it:

```ts
it('[analysis-ui/submit-is-disabled-for-empty-input] disables submit while the input is empty', () => {
  // ...
});
```
