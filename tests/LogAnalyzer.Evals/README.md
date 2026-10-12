# LogAnalyzer.Evals

Golden-set evals for scenarios marked `[eval]` in the specs: behavior that depends on the model's judgment, such as whether a planted incident is reported or a clean log produces no findings.

Unlike unit tests, evals are scored by pass rate: each case runs 3 times, and passes when at least 2 runs complete and pass every grader.

## Cases

Each eval case lives at:

```
Cases/<capability>/<scenario-name-in-kebab-case>.json
```

The path below `Cases/`, without the extension, is the scenario id, so `Cases/log-analysis/clean-log-has-no-findings.json` proves the scenario `[eval] Clean log has no findings` in the `log-analysis` capability. The scenario coverage check (`scripts/check-scenario-coverage.mjs`) reads these paths, so every file below `Cases/` must be a case.

A case names its log and its graders:

```json
{
  "log": "planted-exception-burst-is-reported.log",
  "graders": [
    { "kind": "findings", "lines": [[200, 260]], "categories": ["exception"], "severities": ["error", "critical"], "min": 1 }
  ]
}
```

| Grader | Passes when |
|---|---|
| `findings` | The number of findings that cite a line in one of `lines` (each `[from, to]`, inclusive), and whose category and severity are among `categories` and `severities` when given, is from `min` (0 when missing) to `max` (no limit when missing). |
| `noFindings` | The analysis has no findings. |
| `summary` | A judge model, `claude-haiku-5-5`, answers `question` about the summary with `expected` (`yes` or `no`). The only grader that is not code. |

A run that does not complete, for example because both responses were rejected, fails.

## Golden logs

The logs live in `GoldenLogs/<capability>/`, outside `Cases/`. `GoldenLogGenerator` writes them and the case files from fixed seeds, with each planted incident at the lines its graders name. They are committed, and `GoldenLogTests` checks that the generator still writes exactly them. After changing the generator, rewrite them:

```bash
UPDATE_GOLDEN_LOGS=1 dotnet test tests/LogAnalyzer.Evals --filter "FullyQualifiedName~GoldenLogTests"
```

`GoldenLogTests` needs no key and runs whenever the project's tests run.

## Running

Evals call the real model with your key, so they cost money: a full run sends roughly a million input tokens to Claude Opus 5.5, two thirds of them for the 5,000-line log, which comes to a few dollars. They never run in CI, and the repository holds no key.

```bash
# Once: the key, kept outside the repository (the API's user secrets, shared with running the API)
dotnet user-secrets set "Model:Claude:ApiKey" "<your Anthropic API key>" --project src/LogAnalyzer.Api

RUN_EVALS=1 dotnet test tests/LogAnalyzer.Evals
```

The `ANTHROPIC_API_KEY` environment variable works instead of user secrets. Without `RUN_EVALS=1` the eval cases are skipped, so a plain `dotnet test` of the solution never spends money; with it and without a key, they fail at once with a message naming `Model:Claude:ApiKey`.

The cases use `claude-opus-5-5` at effort `medium`, the API's defaults, and the `log-analysis/v1` prompts the API ships. `Model:Claude:Model` and `Model:Claude:Effort`, in user secrets or as environment variables (`Model__Claude__Effort`), grade another model or effort.

Each run writes `eval-report.json` next to the test assembly (`bin/Debug/net10.0/`): per case, the passes, the tokens, and per run every attempt's outcome, rejection reasons, tokens, retries, request id and time. The test output prints its path. Keep the report of the run that verifies a prompt change with that change.
