// Scenario coverage: shared logic for scripts/check-scenario-coverage.mjs and its tests.
// See that script for the rules and the scenario id convention.

import { readdir, readFile } from 'node:fs/promises';
import path from 'node:path';

const IGNORED_DIRECTORIES = new Set([
  '.angular',
  '.git',
  '.vs',
  'bin',
  'coverage',
  'dist',
  'node_modules',
  'obj',
  'TestResults',
]);

const SEGMENT = '[a-z0-9]+(?:-[a-z0-9]+)*';
const SCENARIO_ID = new RegExp(`^${SEGMENT}(?:/${SEGMENT})+$`);
const SCENARIO_HEADING = /^####\s+Scenario:\s*(.+?)\s*$/gm;
const CSHARP_TAG = /Trait\s*\(\s*"Scenario"\s*,\s*"([^"]+)"\s*\)/g;
const TYPESCRIPT_TAG = new RegExp(`\\[(${SEGMENT}(?:/${SEGMENT})+)\\]`, 'g');
const TYPESCRIPT_TEST_FILE = /\.(?:spec|test|e2e)\.[cm]?[jt]s$/;

/** Turns a scenario heading into its id, for example "log-analysis/line-zero-is-rejected". */
export function toScenarioId(capability, scenarioName) {
  const slug = scenarioName
    .replace(/^\s*\[eval\]\s*/i, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return `${capability}/${slug}`;
}

function toPosix(relativePath) {
  return relativePath.split(path.sep).join('/');
}

async function* walk(directory) {
  let entries;
  try {
    entries = await readdir(directory, { withFileTypes: true });
  } catch (error) {
    if (error.code === 'ENOENT') return;
    throw error;
  }
  for (const entry of entries) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      if (!IGNORED_DIRECTORIES.has(entry.name)) yield* walk(fullPath);
    } else if (entry.isFile()) {
      yield fullPath;
    }
  }
}

/** Reads every scenario heading from spec.md files below specsRoot. Returns Map<id, location>. */
export async function collectScenarios(specsRoot, root) {
  const scenarios = new Map();
  for await (const file of walk(specsRoot)) {
    if (path.basename(file) !== 'spec.md') continue;
    const capability = toPosix(path.relative(specsRoot, path.dirname(file)));
    const content = await readFile(file, 'utf8');
    for (const match of content.matchAll(SCENARIO_HEADING)) {
      scenarios.set(toScenarioId(capability, match[1]), toPosix(path.relative(root, file)));
    }
  }
  return scenarios;
}

/** Reads the scenarios of every active change (openspec/changes/<change>/specs). Returns Map<id, change>. */
export async function collectPendingScenarios(changesRoot) {
  const pending = new Map();
  let changes;
  try {
    changes = await readdir(changesRoot, { withFileTypes: true });
  } catch (error) {
    if (error.code === 'ENOENT') return pending;
    throw error;
  }
  for (const change of changes) {
    if (!change.isDirectory() || change.name === 'archive') continue;
    const deltaRoot = path.join(changesRoot, change.name, 'specs');
    for (const id of (await collectScenarios(deltaRoot, deltaRoot)).keys()) {
      pending.set(id, change.name);
    }
  }
  return pending;
}

/** Finds scenario tags in tests and eval case files below the given directories. Returns Map<id, files[]>. */
export async function collectTags(directories, root) {
  const tags = new Map();
  const add = (id, file) => {
    const files = tags.get(id) ?? [];
    files.push(toPosix(path.relative(root, file)));
    tags.set(id, files);
  };

  for (const directory of directories) {
    for await (const file of walk(directory)) {
      // Eval case files: the path below a Cases folder, without the extension, is the scenario id.
      const segments = toPosix(path.relative(directory, file)).split('/');
      const casesIndex = segments.findIndex((segment) => segment.toLowerCase() === 'cases');
      if (casesIndex !== -1 && segments.length - casesIndex >= 3) {
        const id = segments
          .slice(casesIndex + 1)
          .join('/')
          .replace(/\.[^./]+$/, '');
        if (SCENARIO_ID.test(id)) add(id, file);
      }

      if (file.endsWith('.cs')) {
        const content = await readFile(file, 'utf8');
        for (const match of content.matchAll(CSHARP_TAG)) add(match[1], file);
      } else if (TYPESCRIPT_TEST_FILE.test(file)) {
        const content = await readFile(file, 'utf8');
        for (const match of content.matchAll(TYPESCRIPT_TAG)) add(match[1], file);
      }
    }
  }
  return tags;
}

export async function checkCoverage(root) {
  const scenarios = await collectScenarios(path.join(root, 'openspec', 'specs'), root);
  const pending = await collectPendingScenarios(path.join(root, 'openspec', 'changes'));
  const tags = await collectTags([path.join(root, 'tests'), path.join(root, 'src')], root);

  const missing = [...scenarios].filter(([id]) => !tags.has(id)).map(([id, file]) => ({ id, file }));
  const unknown = [...tags]
    .filter(([id]) => !scenarios.has(id) && !pending.has(id))
    .map(([id, files]) => ({ id, files }));
  const pendingWithTests = [...pending.keys()].filter((id) => !scenarios.has(id) && tags.has(id));

  return {
    archived: scenarios.size,
    covered: scenarios.size - missing.length,
    pending: [...pending.keys()].filter((id) => !scenarios.has(id)).length,
    pendingWithTests: pendingWithTests.length,
    missing,
    unknown,
  };
}

export function formatReport(result) {
  const lines = [
    'Scenario coverage',
    `  Archived scenarios (openspec/specs): ${result.archived} - ${result.covered} covered, ${result.missing.length} missing`,
    `  Pending scenarios (active changes):  ${result.pending} - ${result.pendingWithTests} already have tests`,
  ];
  if (result.missing.length > 0) {
    lines.push('', 'Archived scenarios without a test or eval case:');
    for (const { id, file } of result.missing) lines.push(`  MISSING  ${id}  (${file})`);
  }
  if (result.unknown.length > 0) {
    lines.push('', 'Tests tagged with an id that matches no scenario:');
    for (const { id, files } of result.unknown) lines.push(`  UNKNOWN  ${id}  (${files.join(', ')})`);
  }
  const failed = result.missing.length > 0 || result.unknown.length > 0;
  lines.push('', failed ? 'FAIL' : 'PASS');
  return lines.join('\n');
}
