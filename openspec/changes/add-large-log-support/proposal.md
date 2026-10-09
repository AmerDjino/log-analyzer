# Proposal: Add large-log support

## Why

Real incident logs run to tens of thousands of lines, far beyond what one model request can hold or should cost. Collapsing repetition and splitting what remains lets the analyzer handle real logs without losing the exact line references that make its findings checkable.

## What Changes

- Collapse lines that differ only in their timestamp, keeping occurrence counts and original line numbers.
- Split input that exceeds the per-request token budget into chunks on line boundaries, merge findings across chunks and produce one summary per run.
- Fail a run cleanly when any chunk fails, instead of reporting partial findings.
- Report progress while a run is in flight, in both the API and the UI, and show occurrence counts on findings.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `log-ingestion`: adds collapsing of repeated lines and the guarantee of original line numbers.
- `model-gateway`: adds the token budget, chunking and merging of results.
- `analysis-history`: adds progress reporting for running runs.
- `analysis-ui`: adds live progress and occurrence counts.

## Non-Goals

- Logs larger than the submission size limit.
- Partial results when a chunk fails.

## Impact

- New preprocessing step before prompting, and a merge step after the chunk results.
- Token and cost impact: collapsing usually shrinks repetitive logs several times over. Very large logs now take several model calls (one per chunk plus one summary call), each within the configured budget.
