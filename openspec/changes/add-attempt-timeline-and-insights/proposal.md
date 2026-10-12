# Proposal: Add the attempt timeline and insights

## Why

The validation layer is what makes the assistant trustworthy, yet so far it works out of sight. Showing every attempt, and aggregating rejection rates, cost and latency per prompt version, makes the system's reliability visible in demos and gives the lessons-learned document real numbers instead of impressions.

## What Changes

- Expose a run's attempts to the person who submitted the run, with their outcome, rejection reasons, usage and retries, but never the raw model output.
- Aggregate reliability and cost metrics over the signed-in person's own runs, per prompt version and model, for a chosen period.
- Add an attempt timeline to the run page.
- Add an insights page that compares prompt versions side by side.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `analysis-history`: adds the attempts view and aggregated metrics.
- `analysis-ui`: adds the attempt timeline and the insights page.

## Non-Goals

- Exporting metrics to an external monitoring system.
- Metrics across everyone's runs: each person sees metrics for their own runs (decided 2026-10-12).

## Impact

- New read-only API endpoints for a run's attempts and for metrics, backed by queries over stored attempts. Both answer only for the person's own runs.
- Depends on `add-claude-integration`, which records tokens, latency and retries for each attempt.
- Two new UI views.
- Token and cost impact: none, since only stored data is read.
