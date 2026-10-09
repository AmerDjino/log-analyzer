// Tests for the scenario coverage check. Run with: node --test scripts/check-scenario-coverage.test.mjs
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { checkCoverage, toScenarioId } from './scenario-coverage.mjs';

const scriptsDirectory = path.dirname(fileURLToPath(import.meta.url));
const fixture = (name) => path.join(scriptsDirectory, 'fixtures', 'coverage', name);

function runCli(root) {
  return spawnSync(process.execPath, [path.join(scriptsDirectory, 'check-scenario-coverage.mjs'), '--root', root], {
    encoding: 'utf8',
  });
}

test('scenario ids are kebab-case and drop the [eval] prefix', () => {
  assert.equal(toScenarioId('log-analysis', 'Line numbers start at 1'), 'log-analysis/line-numbers-start-at-1');
  assert.equal(
    toScenarioId('log-analysis', '[eval] Clean log has no findings'),
    'log-analysis/clean-log-has-no-findings',
  );
  assert.equal(toScenarioId('model-gateway', 'Rate limit then success'), 'model-gateway/rate-limit-then-success');
});

test('fails when an archived scenario has no tagged test', () => {
  const result = runCli(fixture('missing'));

  assert.equal(result.status, 1, result.stdout + result.stderr);
  assert.match(result.stdout, /MISSING {2}demo\/greeting-is-shown/);
});

test('passes once every archived scenario is tagged', () => {
  const result = runCli(fixture('covered'));

  assert.equal(result.status, 0, result.stdout + result.stderr);
  assert.match(result.stdout, /PASS$/m);
});

test('counts C# traits, Vitest test names and eval case files, and treats active-change scenarios as pending', async () => {
  const result = await checkCoverage(fixture('covered'));

  assert.deepEqual(
    { archived: result.archived, covered: result.covered, pending: result.pending, pendingWithTests: result.pendingWithTests },
    { archived: 3, covered: 3, pending: 1, pendingWithTests: 1 },
  );
  assert.deepEqual(result.missing, []);
  assert.deepEqual(result.unknown, []);
});

test('fails when a test is tagged with an id that matches no scenario', () => {
  const result = runCli(fixture('unknown'));

  assert.equal(result.status, 1, result.stdout + result.stderr);
  assert.match(result.stdout, /UNKNOWN {2}demo\/greting-is-shown/);
  assert.doesNotMatch(result.stdout, /MISSING/);
});
