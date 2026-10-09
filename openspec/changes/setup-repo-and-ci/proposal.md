# Proposal: Set up the repository and CI

## Why

Every later change needs the same foundation: one repository, a local stack that starts with a single command, and a pipeline that keeps specs and tests honest. Building it first means each feature change can be verified the same way from its first commit.

## What Changes

- Create the repository layout: a .NET 10 solution (`LogAnalyzer.Api`, `LogAnalyzer.Core`, `LogAnalyzer.Infrastructure`), xUnit test projects, and an Angular 22 workspace under `src/web`.
- Add Docker Compose with `api`, `web` (nginx) and `postgres` services, health checks, and a git-ignored `.env` for local secrets.
- Add a CI pipeline that runs OpenSpec validation, backend and frontend tests, and a scenario-coverage check.
- No behavior changes: this change sets `skip_specs: true`.

## Capabilities

### New Capabilities

- None. This change is tooling only.

### Modified Capabilities

- None.

## Non-Goals

- Any API endpoint other than a health check.
- Deployment beyond the local Docker Compose stack.

## Impact

- New: solution and project files, Dockerfiles, `docker-compose.yml`, `.env.example`, the CI workflow and `scripts/check-scenario-coverage`.
- Developers need Docker, the .NET 10 SDK, a current Node.js LTS (22 or 24) and the OpenSpec CLI.
