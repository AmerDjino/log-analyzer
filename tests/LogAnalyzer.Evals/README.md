# LogAnalyzer.Evals

Golden-set evals for scenarios marked `[eval]` in the specs: behavior that depends on the model's judgment, such as whether a planted incident is reported or a clean log produces no findings.

Unlike unit tests, evals are scored by pass rate across runs rather than required to pass every time.

## Case files

Each eval case lives at:

```
Cases/<capability>/<scenario-name-in-kebab-case>.<ext>
```

The path below `Cases/`, without the extension, is the scenario id, so `Cases/log-analysis/clean-log-has-no-findings.json` proves the scenario `[eval] Clean log has no findings` in the `log-analysis` capability. The scenario coverage check (`scripts/check-scenario-coverage.mjs`) reads these paths.

## Running

Evals call the real model, so they cost money and need an API key. They are not part of pull-request CI; run them on demand or in the nightly job. The harness, the case format and the first cases arrive with the `add-claude-integration` change.
