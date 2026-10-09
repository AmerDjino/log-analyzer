# Tasks

## 1. Repository layout

- [x] 1.1 Create the .NET 10 solution with `LogAnalyzer.Api`, `LogAnalyzer.Core` and `LogAnalyzer.Infrastructure`, with `LogAnalyzer.Core` referencing no other project; verify `dotnet build` succeeds
- [x] 1.2 Create the `LogAnalyzer.UnitTests`, `LogAnalyzer.IntegrationTests` and `LogAnalyzer.Evals` xUnit projects; verify `dotnet test` runs with zero failures
- [x] 1.3 Create the Angular 22 workspace in `src/web` with Vitest; verify `ng test` and `ng build` succeed
- [x] 1.4 Add `.gitignore` (including `.env`), `.editorconfig` and a README listing prerequisites and the one-command start; verify a clean clone builds by following the README

## 2. Local stack

- [x] 2.1 Add the API Dockerfile (multi-stage, non-root `app` user) and a `/health` endpoint that checks database connectivity; verify the image builds and `/health` returns 200 against a running Postgres
- [x] 2.2 Add the web Dockerfile (Node build stage, nginx runtime) with `/api` proxied to the `api` service, plus the Angular CLI proxy config for development; verify the image builds and `/api/health` answers through nginx
- [x] 2.3 Add `docker-compose.yml` (postgres with a `pg_isready` health check and a named volume, api after a healthy postgres, web) and `.env.example`; verify `docker compose up` on a fresh clone reports all three services healthy

## 3. CI and coverage

- [x] 3.1 Add `scripts/check-scenario-coverage` with fixtures under `scripts/fixtures/coverage/`; verify it fails on the fixture scenario without a tagged test and passes once the test is tagged
- [x] 3.2 Add the CI workflow running `openspec validate --all --strict`, `openspec validate --archived`, `dotnet test`, `npm ci`, `ng test`, `ng build` and the coverage script; verify a pull request runs every step green
- [x] 3.3 Document the scenario id convention and the CI gates in the README; verify the README example matches the script's output

## Workflow follow-up

- Archive the change with `/opsx:archive setup-repo-and-ci` once CI is green.
