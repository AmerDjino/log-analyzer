#!/usr/bin/env node
// Scenario coverage check.
//
// Every scenario in openspec/specs (the archived, agreed behavior) must be proven by a test or an eval case.
// Scenarios in active changes under openspec/changes are listed as pending and never fail the check.
// A test tagged with an id that matches no scenario fails the check, because it usually means a typo
// or a scenario that was renamed or removed.
//
// Scenario id: <capability>/<scenario-name-in-kebab-case>, dropping any [eval] prefix.
//   "#### Scenario: [eval] Clean log has no findings" in openspec/specs/log-analysis/spec.md
//   -> log-analysis/clean-log-has-no-findings
//
// Where a test declares the scenario it proves:
//   C#                  [Trait("Scenario", "log-analysis/unknown-severity-is-rejected")]
//   Vitest, Playwright  it('[analysis-ui/clean-log-message] says no problems were found', ...)
//   Eval case file      tests/LogAnalyzer.Evals/Cases/log-analysis/clean-log-has-no-findings.json
//
// Usage: node scripts/check-scenario-coverage.mjs [--root <repository root>]
// Exit code: 0 when every archived scenario is covered and no tag is unknown, 1 otherwise.

import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkCoverage, formatReport } from './scenario-coverage.mjs';

function parseRoot(args) {
  const index = args.indexOf('--root');
  if (index !== -1 && args[index + 1]) return path.resolve(args[index + 1]);
  return path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
}

const result = await checkCoverage(parseRoot(process.argv.slice(2)));
console.log(formatReport(result));
process.exitCode = result.missing.length > 0 || result.unknown.length > 0 ? 1 : 0;
