# Log Analyzer

A spec-driven AI assistant that turns an application log into a short incident report. Every finding cites the exact log lines that support it, and every model response passes a validation layer before any part of it is stored or shown.

Stack: .NET 10 (ASP.NET Core minimal APIs), PostgreSQL, Angular 22, Claude through the official Anthropic C# SDK, Docker Compose. Behavior is specified first with [OpenSpec](https://github.com/Fission-AI/OpenSpec).

## Prerequisites

| Tool | Version | Used for |
|---|---|---|
| .NET SDK | 10.0.100 or later (see `global.json`) | API and tests |
| Node.js | 24.15 or later (LTS); 22.22.3+ also works | Angular app, OpenSpec CLI, coverage check |
| Docker Desktop | current | Local stack and integration tests (Testcontainers) |
| OpenSpec CLI | 1.14.1 | `npm install --global @fission-ai/openspec@1.14.1` |

## First-time setup

```bash
# 1. Local settings for Docker Compose (.env is git-ignored)
cp .env.example .env                 # PowerShell: Copy-Item .env.example .env

# 2. Angular dependencies. The first install creates src/web/package-lock.json:
#    commit it, because CI and the web Docker image use `npm ci`.
npm --prefix src/web install

# 3. OpenSpec slash commands for your AI coding assistant
openspec init --tools claude         # or github-copilot, cursor, ... (keeps the existing openspec/ folder)
```

## Run

**Everything in Docker:**

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| Web app (nginx) | http://localhost:8080 |
| API health through nginx | http://localhost:8080/api/health |
| API directly | http://localhost:5080/health |
| OpenAPI document (Development) | http://localhost:5080/openapi/v1.json |
| Account endpoints | `/api/auth/...`, see [docs/accounts-api.md](docs/accounts-api.md) |
| Run endpoints | `/api/runs...`, see [docs/runs-api.md](docs/runs-api.md) |
| PostgreSQL | localhost:5432 |

Ports come from `.env`. `docker compose up` waits for PostgreSQL to be healthy before starting the API, and for the API before starting the web app.

**API and web app on your machine, PostgreSQL in Docker:**

```bash
docker compose up --detach postgres

# Once: tell the API where PostgreSQL is (use the password from your .env)
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=loganalyzer;Username=loganalyzer;Password=change-me-local-only" --project src/LogAnalyzer.Api

dotnet run --project src/LogAnalyzer.Api      # http://localhost:5080
npm --prefix src/web start                    # http://localhost:4200, forwards /api to the API
```

If the connection string is missing, the API stops at startup with a message naming the setting.

## Database migrations

The schema is managed by EF Core migrations in `src/LogAnalyzer.Infrastructure/Persistence/Migrations`. The API applies pending migrations when it starts. If PostgreSQL is not reachable yet, the API still starts, retries every 5 seconds, and `/health` reports `Unhealthy` until the schema is current.

```bash
dotnet tool restore                          # once: installs the pinned dotnet-ef

# Add a migration after changing the model
dotnet ef migrations add <Name> --project src/LogAnalyzer.Infrastructure --startup-project src/LogAnalyzer.Infrastructure

# Roll the local database back to empty (or: docker compose down --volumes)
dotnet ef database update 0 --project src/LogAnalyzer.Infrastructure --startup-project src/LogAnalyzer.Infrastructure --connection "Host=localhost;Port=5432;Database=loganalyzer;Username=loganalyzer;Password=change-me-local-only"
```

## Test

```bash
dotnet test                                    # whole solution; integration tests need Docker running
npm --prefix src/web test -- --watch=false     # Angular unit tests (Vitest)
npm --prefix src/web run api:generate          # regenerate the API types, then check they match the committed ones:
git diff --exit-code src/web/src/app/api/schema.d.ts
node --test scripts/check-scenario-coverage.test.mjs
node scripts/check-scenario-coverage.mjs
```

| Project | What it holds |
|---|---|
| `tests/LogAnalyzer.UnitTests` | Fast tests with no network or database, including the rule that `LogAnalyzer.Core` depends on no infrastructure. |
| `tests/LogAnalyzer.IntegrationTests` | The API hosted in memory against PostgreSQL started by Testcontainers. |
| `tests/LogAnalyzer.Evals` | Golden-set evals for `[eval]` scenarios. They call the real model, so they run on demand or nightly, never on pull requests. |

## Spec-driven workflow

The specs come first; code follows them.

```
openspec/
├─ config.yaml              project context and per-artifact rules for the AI
├─ schemas/spec-tdd/        workflow: proposal → specs → design → test-plan → tasks
├─ specs/                   source of truth, filled as changes are archived
└─ changes/                 one folder per change in flight; archive/ keeps finished ones
```

The `spec-tdd` workflow is OpenSpec's default `spec-driven` workflow plus a `test-plan` step that maps every scenario to the test or eval that proves it. Each task group in `tasks.md` starts with the failing tests for its scenarios.

Planned changes, in the order they are archived:

1. `setup-repo-and-ci`: this repository, the local stack and CI (no behavior specs)
2. `add-user-accounts`: registration, sign-in and sessions; every endpoint requires sign-in by default
3. `add-analysis-contract`: the output contract and the validation layer
4. `add-api-skeleton`: ingestion, runs and attempts, against a stand-in model
5. `add-web-shell`: submit, follow a run, see findings next to the cited lines
6. `add-claude-integration`: the real model, prompt versions, usage, evals
7. `add-large-log-support`: collapsing, chunking and merging
8. `add-input-safety`: redaction, prompt injection and rate limits
9. `add-attempt-timeline-and-insights`: the validation layer made visible

Typical loop for one change, in your AI assistant:

```
/opsx:continue add-api-skeleton     write the next planning artifact (design, test plan, tasks)
/opsx:apply add-api-skeleton        implement the tasks, test-first
/opsx:verify add-api-skeleton       check the code against the specs
/opsx:archive add-api-skeleton      merge the spec deltas into openspec/specs
```

GitHub Copilot and Cursor spell the commands `/opsx-continue` and so on. `/opsx:verify` and `/opsx:continue` need the expanded command set: run `openspec config profile`, select the expanded workflows, then `openspec update`.

## Scenario ids and the coverage check

Every scenario has an id: `<capability>/<scenario-name-in-kebab-case>`, dropping any `[eval]` prefix.

```
openspec/specs/log-analysis/spec.md
  #### Scenario: [eval] Clean log has no findings   →   log-analysis/clean-log-has-no-findings
```

A test declares the scenario it proves in one of three ways:

| Where | How |
|---|---|
| C# (xUnit) | `[Trait("Scenario", "log-analysis/unknown-severity-is-rejected")]` |
| Vitest or Playwright | the id in brackets in the test name: `it('[analysis-ui/clean-log-message] says no problems were found', ...)` |
| Eval case | the file path: `tests/LogAnalyzer.Evals/Cases/log-analysis/clean-log-has-no-findings.json` |

`scripts/check-scenario-coverage.mjs` fails when a scenario in `openspec/specs` has no test, or when a test is tagged with an id that matches no scenario. Scenarios in active changes are reported as pending and never fail the check. Example, run against the `missing` fixture:

```
$ node scripts/check-scenario-coverage.mjs --root scripts/fixtures/coverage/missing
Scenario coverage
  Archived scenarios (openspec/specs): 1 - 0 covered, 1 missing
  Pending scenarios (active changes):  0 - 0 already have tests

Archived scenarios without a test or eval case:
  MISSING  demo/greeting-is-shown  (openspec/specs/demo/spec.md)

FAIL
```

## CI

`.github/workflows/ci.yml` runs on every pull request and on pushes to `main`:

| Job | Gates |
|---|---|
| Specs and scenario coverage | `openspec schema validate spec-tdd`, `openspec validate --all --strict`, `openspec validate --archived`, the coverage check and its tests |
| Backend | `dotnet build` with warnings as errors, unit tests, integration tests |
| Frontend | `npm ci`, generated API types up to date (`npm run api:generate`, then `git diff --exit-code`), `ng test`, `ng build` |
| Docker Compose stack | `docker compose up --wait` until every service is healthy, then `/api/health` through nginx |

Evals are not part of this workflow, because they call the real model and cost money.

## Repository layout

```
├─ docs/                             the account and run APIs, and the model's output contract
├─ openspec/                         specs, changes and the spec-tdd workflow
├─ schemas/                          JSON Schema for the model's output contract (docs/output-contract.md)
├─ scripts/                          scenario coverage check, its tests and fixtures
├─ src/
│  ├─ LogAnalyzer.Api/               ASP.NET Core minimal API (composition root)
│  ├─ LogAnalyzer.Core/              domain, validation, orchestration; no infrastructure
│  ├─ LogAnalyzer.Infrastructure/    PostgreSQL, and later the Claude client
│  └─ web/                           Angular 22 app, nginx config, Dockerfile
├─ tests/                            UnitTests, IntegrationTests, Evals
├─ docker-compose.yml
├─ Directory.Build.props             shared build settings
├─ Directory.Packages.props          every NuGet package version
└─ LogAnalyzer.slnx
```
