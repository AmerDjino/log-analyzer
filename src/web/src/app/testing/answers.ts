// Canned API answers for the unit tests, typed with the generated schemas (test-plan.md, Unit fixtures).
import { components } from '../api/schema';

type Schemas = components['schemas'];

/** An error answer: the status and the body to flush with HttpTestingController. */
export interface ErrorAnswer {
  status: number;
  body: Schemas['ProblemDetails'] | string | null;
}

export const ana: Schemas['AccountResponse'] = {
  id: '0199d0a5-0000-7000-8000-00000000a0a0',
  email: 'ana@example.com',
};

export const bob: Schemas['AccountResponse'] = {
  id: '0199d0a5-0000-7000-8000-00000000b0b0',
  email: 'bob@example.com',
};

const statistics: Schemas['RunStatisticsResponse'] = {
  lineCount: 500,
  byteCount: 31_250,
  levels: { critical: 1, error: 2, warning: 1, info: 400, debug: 0, trace: 0 },
  timeRange: { start: '2026-10-10T09:00:01Z', end: '2026-10-10T09:41:20Z', utc: true },
};

function finding(
  severity: Schemas['Severity'],
  title: string,
  lineRefs: number[],
  evidence: string,
): Schemas['FindingResponse'] {
  return {
    title,
    severity,
    category: 'other',
    lineRefs,
    evidence,
    explanation: `${title} explained.`,
    suggestedAction: `Look into ${title.toLowerCase()}.`,
  };
}

function completed(
  id: string,
  findings: Schemas['FindingResponse'][],
  summary?: string,
): Schemas['RunResponse'] {
  return {
    id,
    status: 'completed',
    fileName: 'orders-api.log',
    submittedAt: '2026-10-10T09:45:00+00:00',
    startedAt: '2026-10-10T09:45:01+00:00',
    finishedAt: '2026-10-10T09:45:03+00:00',
    failureReason: null,
    statistics,
    summary:
      summary ??
      (findings.length === 0 ? 'No problems were found in the log.' : 'The log shows problems.'),
    findings,
  };
}

export const queuedRun: Schemas['RunResponse'] = {
  id: '0199d0a5-1000-7000-8000-000000000001',
  status: 'queued',
  fileName: null,
  submittedAt: '2026-10-10T09:50:00+00:00',
  startedAt: null,
  finishedAt: null,
  failureReason: null,
  statistics,
  summary: null,
  findings: null,
};

/** The queued run once the worker has claimed it. */
export const runningRun: Schemas['RunResponse'] = {
  ...queuedRun,
  status: 'running',
  startedAt: '2026-10-10T09:50:01+00:00',
};

/** Completed with a warning, a critical and an error finding, in that order. */
export const warningCriticalErrorRun = completed('0199d0a5-1000-7000-8000-000000000002', [
  finding('warning', 'Slow response', [10], 'took 2.5 s'),
  finding('critical', 'Connection pool exhausted', [20], 'pool exhausted'),
  finding('error', 'Payment failed', [30], 'Payment for order 42 failed'),
]);

/** Completed with a critical and a warning finding. */
export const criticalWarningRun = completed('0199d0a5-1000-7000-8000-000000000003', [
  finding('critical', 'Connection pool exhausted', [20], 'pool exhausted'),
  finding('warning', 'Slow response', [10], 'took 2.5 s'),
]);

/** Completed with an error, an info, a second error and a critical finding, in that order. */
export const mixedSeveritiesRun = completed('0199d0a5-1000-7000-8000-000000000004', [
  finding('error', 'First error', [11], 'first'),
  finding('info', 'Cache warmed', [12], 'warmed'),
  finding('error', 'Second error', [13], 'second'),
  finding('critical', 'Disk full', [14], 'disk full'),
]);

export const cleanRun = completed('0199d0a5-1000-7000-8000-000000000005', []);

/** Completed with a summary and a finding whose texts contain markup. */
export const markupRun: Schemas['RunResponse'] = {
  ...completed(
    '0199d0a5-1000-7000-8000-000000000006',
    [
      {
        title: 'A <b>bold</b> title',
        severity: 'error',
        category: 'other',
        lineRefs: [5],
        evidence: '<b>bold</b>',
        explanation: 'An explanation with <b>bold</b> text.',
        suggestedAction: 'An action with <b>bold</b> text.',
      },
    ],
    'A summary with <b>bold</b> text.',
  ),
};

export const validationFailedRun: Schemas['RunResponse'] = {
  ...queuedRun,
  id: '0199d0a5-1000-7000-8000-000000000007',
  status: 'failed',
  startedAt: '2026-10-10T09:50:01+00:00',
  finishedAt: '2026-10-10T09:50:09+00:00',
  failureReason: 'VALIDATION_FAILED',
};

/** Completed with a finding that cites line 120, and a second that cites lines 40 and 60. */
export const evidenceRun = completed('0199d0a5-1000-7000-8000-000000000008', [
  finding('critical', 'Connection pool exhausted', [120], 'connection pool exhausted'),
  finding('error', 'Payment failed', [40, 60], 'Payment for order 42 failed'),
]);

function lines(
  from: number,
  to: number,
  text: (number: number) => string,
): Schemas['RunLinesResponse'] {
  const result: Schemas['RunLineResponse'][] = [];
  for (let number = from; number <= to; number++) {
    result.push({ number, text: text(number) });
  }
  return { lines: result };
}

const plainLine = (number: number) =>
  `2026-10-10T09:00:00Z info: Orders.Api[0] Request ${number} finished`;

/** Lines 117 to 123 of the evidence run. Line 120 holds the evidence inside a longer line. */
export const lines117To123 = lines(117, 123, (number) =>
  number === 120
    ? '2026-10-10T09:41:20Z crit: Orders.Api[0] Database connection pool exhausted after 30 s'
    : plainLine(number),
);

export const lines37To43 = lines(37, 43, (number) =>
  number === 40
    ? '2026-10-10T09:10:00Z fail: Orders.Api[0] Payment for order 42 failed'
    : plainLine(number),
);

export const lines57To63 = lines(57, 63, plainLine);

/** Two runs, newest first: the completed run named orders-api.log, and an older queued paste. */
export const runListPage: Schemas['RunPageResponse'] = {
  items: [
    {
      id: warningCriticalErrorRun.id,
      fileName: 'orders-api.log',
      submittedAt: '2026-10-10T09:45:00+00:00',
      status: 'completed',
      highestSeverity: 'critical',
    },
    {
      id: '0199d0a5-1000-7000-8000-000000000009',
      fileName: null,
      submittedAt: '2026-10-10T08:30:00+00:00',
      status: 'queued',
      highestSeverity: null,
    },
  ],
  nextCursor: null,
};

export const submissionAccepted: Schemas['SubmissionResponse'] = {
  id: queuedRun.id,
  status: 'queued',
};

export const limits: Schemas['SubmissionLimitsResponse'] = { maxSubmissionBytes: 1_048_576 };

function problem(
  status: number,
  code: NonNullable<Schemas['ProblemDetails']['code']>,
  title: string,
): ErrorAnswer {
  return { status, body: { type: 'about:blank', title, status, detail: `${title}.`, code } };
}

export const problems = {
  passwordTooShort: problem(400, 'PASSWORD_TOO_SHORT', 'Password too short'),
  emailTaken: problem(409, 'EMAIL_TAKEN', 'Email address already registered'),
  invalidCredentials: problem(401, 'INVALID_CREDENTIALS', 'Sign-in failed'),
  accountLocked: problem(403, 'ACCOUNT_LOCKED', 'Account locked'),
  inputTooLarge: problem(413, 'INPUT_TOO_LARGE', 'Log too large'),
  emptyInput: problem(400, 'EMPTY_INPUT', 'Empty log'),
  runNotFound: problem(404, 'RUN_NOT_FOUND', 'Run not found'),
  signInRequired: problem(401, 'SIGN_IN_REQUIRED', 'Sign-in required'),
  /** nginx's own page for a body over its 12 MB limit: HTML, without a code. */
  nginxTooLarge: {
    status: 413,
    body: '<html><head><title>413 Request Entity Too Large</title></head><body>nginx</body></html>',
  } satisfies ErrorAnswer,
  serviceUnavailable: { status: 503, body: null } satisfies ErrorAnswer,
};

/** The closed set of failure reasons in docs/runs-api.md. The OpenAPI document types failureReason as a plain string. */
export const failureReasons = [
  'VALIDATION_FAILED',
  'MODEL_UNAVAILABLE',
  'MODEL_CONFIGURATION_ERROR',
  'INTERRUPTED',
  'INTERNAL_ERROR',
] as const;
