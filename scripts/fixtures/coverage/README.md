# Coverage check fixtures

Tiny repositories used by `scripts/check-scenario-coverage.test.mjs`.

| Fixture | Expected result |
|---|---|
| `missing/` | Fails: `demo/greeting-is-shown` has no tagged test. |
| `covered/` | Passes: every scenario is proven by a C# trait, a Vitest test name or an eval case file; a test for a scenario in an active change counts as pending. |
| `unknown/` | Fails: a test is tagged with `demo/greting-is-shown`, which matches no scenario. |

These files live under `scripts/`, so neither `dotnet build`, the Angular build, OpenSpec nor the real coverage check reads them.
