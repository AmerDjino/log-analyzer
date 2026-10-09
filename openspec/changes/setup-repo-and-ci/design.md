# Design: Set up the repository and CI

## Context

Greenfield repository; see proposal.md for motivation. These decisions shape every later change, so each one is recorded with the alternatives that lost.

## Goals / Non-Goals

**Goals:**
- `docker compose up` starts a working stack on a fresh clone.
- CI fails when specs are malformed, tests fail, or an archived scenario has no test.

**Non-Goals:**
- Cloud deployment, infrastructure as code, release automation.

## Decisions

### One repository with one `openspec/` root

A feature usually touches both the API and the UI, and one OpenSpec change folder can carry the deltas for both.

- Alternative: separate API and UI repositories sharing an OpenSpec store. Rejected: stores are still beta, and a cross-repo feature would be split across several plans.

### Three backend projects

- `LogAnalyzer.Core`: domain types, validation and orchestration, with no infrastructure dependencies, so the validator is testable without a network or database.
- `LogAnalyzer.Infrastructure`: the Claude client, EF Core and Npgsql.
- `LogAnalyzer.Api`: minimal API endpoints and the composition root.
- Alternative: a single project. Rejected: validation and orchestration would depend on infrastructure.
- Alternative: a full clean-architecture split (Domain, Application, Infrastructure, Presentation). Rejected: more ceremony than a project this size needs.

### Compose services

- `postgres`: official image, `pg_isready` health check, named volume.
- `api`: multi-stage build that runs as the non-root `app` user of the .NET images and starts only after `postgres` is healthy.
- `web`: Angular build in a Node stage, served by nginx, which also proxies `/api` to `api`, so the browser sees one origin and no CORS setup is needed. In development the Angular CLI proxy does the same job.
- Alternative: serve the Angular build from the API's `wwwroot`. Rejected: it couples UI and API releases and hides how the two talk in production.

### Local secrets

`dotnet user-secrets` when the API runs outside Docker; a git-ignored `.env` with a committed `.env.example` for Compose. Docker secrets replace `.env` for the model API key in `add-claude-integration`.

### CI gates

In order: `openspec validate --all --strict`, `openspec validate --archived`, `dotnet test`, `npm ci`, `ng test`, `ng build`, then the scenario-coverage check. Evals call the real model and cost money, so they run in a separate manual or nightly job, not on every pull request.

### Scenario-coverage check

Every scenario has the id `<capability>/<scenario-name-in-kebab-case>`, with any `[eval]` prefix dropped. Tests carry it as an xUnit `[Trait("Scenario", "<id>")]`, in brackets in a Vitest or Playwright test name, or as an eval case file name. The script reads `#### Scenario:` headings from `openspec/specs/**/spec.md` and fails when an archived scenario has no matching test. Scenarios in active changes are listed but do not fail the build, because they are still being implemented.

- Alternative: rely on `/opsx:verify`. Rejected as the only gate: it is advisory and does not block an archive.

## Risks / Trade-offs

- [Testcontainers needs Docker in CI] → Use a runner with Docker available and fail fast with a clear message when it is missing.
- [A naming convention can be satisfied by an empty test] → Code review checks that each tagged test asserts the scenario's THEN clauses.

## Open Questions

- CI platform: GitHub Actions or Azure DevOps. The gates above are platform-neutral, so the choice changes neither the specs nor the tasks.
