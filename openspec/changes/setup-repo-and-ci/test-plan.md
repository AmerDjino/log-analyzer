# Test Plan

This change sets `skip_specs: true`, so it has no scenarios. These operational checks prove it:

| Check | How | Expected |
|---|---|---|
| Fresh clone builds | Run `dotnet build` and `ng build` in a clean checkout | Both succeed |
| Local stack starts | Run `docker compose up` on a fresh clone with `.env` copied from `.env.example` | `postgres`, `api` and `web` report healthy |
| Coverage check catches gaps | Run the coverage script against the fixture spec with no matching test | The script fails and names the scenario id |
| Coverage check passes when tagged | Run it against the fixture spec with a tagged test | The script passes |
| CI runs every gate | Open a pull request | Every CI step runs and passes |

## Fixtures

- `scripts/fixtures/coverage/`: one spec with a single scenario, one test file without the scenario tag and one with it.
