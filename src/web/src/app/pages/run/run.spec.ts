import { TestBed } from '@angular/core/testing';
import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { Run } from '../../api/api-client';
import {
  ana,
  cleanRun,
  criticalWarningRun,
  evidenceRun,
  lines117To123,
  lines37To43,
  lines57To63,
  markupRun,
  mixedSeveritiesRun,
  problems,
  queuedRun,
  runningRun,
  validationFailedRun,
  warningCriticalErrorRun,
} from '../../testing/answers';
import { answerSession, flushError, provideTestApp } from '../../testing/test-app';

describe('RunPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  // The builder runs spec files in one context, so fake timers must not outlive their test.
  afterEach(() => {
    vi.useRealTimers();
    http.verify();
  });

  /** Opens the run's page signed in, and returns its first read, still unanswered. */
  async function open(id: string): Promise<TestRequest> {
    const navigation = harness.navigateByUrl(`/runs/${id}`);
    await answerSession(http, ana);
    await navigation;
    return http.expectOne(`/api/runs/${id}`);
  }

  async function show(run: Run): Promise<HTMLElement> {
    (await open(run.id)).flush(run);
    await harness.fixture.whenStable();
    return page();
  }

  function page(): HTMLElement {
    return harness.routeNativeElement!;
  }

  // While fake timers are on, change detection waits for a timer, so tests render explicitly.
  function render(): void {
    harness.fixture.detectChanges();
  }

  function readAgain(): TestRequest {
    return http.expectOne(`/api/runs/${queuedRun.id}`);
  }

  function live(): string {
    return page().querySelector('[aria-live]')?.textContent ?? '';
  }

  function titles(): string[] {
    return Array.from(page().querySelectorAll('.finding .title')).map((t) => t.textContent!.trim());
  }

  function filterButton(label: string): HTMLButtonElement {
    return Array.from(page().querySelectorAll<HTMLButtonElement>('.filter button')).find((b) =>
      b.textContent!.trim().startsWith(label),
    )!;
  }

  function toggle(index: number): HTMLButtonElement {
    return page().querySelectorAll<HTMLButtonElement>('.finding .evidence-toggle')[index];
  }

  function lineRequests(): TestRequest[] {
    return http.match((r) => r.url === `/api/runs/${evidenceRun.id}/lines`);
  }

  function rowNumbers(): string[] {
    return Array.from(page().querySelectorAll('.evidence-lines .number')).map((n) =>
      n.textContent!.trim(),
    );
  }

  it('[analysis-ui/results-appear-when-the-run-completes] shows the summary and findings when a later read returns the completed run', async () => {
    const first = await open(queuedRun.id);
    vi.useFakeTimers();
    first.flush(queuedRun);
    render();
    expect(page().querySelector('.summary')).toBeNull();

    await vi.advanceTimersByTimeAsync(1_000);
    readAgain().flush({ ...warningCriticalErrorRun, id: queuedRun.id });
    render();

    expect(page().querySelector('.summary')?.textContent).toContain('The log shows problems.');
    expect(titles()).toEqual(['Connection pool exhausted', 'Payment failed', 'Slow response']);
  });

  it('[analysis-ui/failure-reason-is-explained] explains VALIDATION_FAILED in plain language', async () => {
    const element = await show(validationFailedRun);

    expect(element.textContent).toContain("The model's answers did not pass validation");
  });

  it('[analysis-ui/findings-are-ordered-by-severity] orders findings critical, error, warning', async () => {
    await show(warningCriticalErrorRun);

    expect(titles()).toEqual(['Connection pool exhausted', 'Payment failed', 'Slow response']);
  });

  it('[analysis-ui/filtering-by-severity] shows only critical findings after filtering to critical', async () => {
    await show(criticalWarningRun);

    filterButton('Critical').click();
    await harness.fixture.whenStable();

    expect(titles()).toEqual(['Connection pool exhausted']);
  });

  it('[analysis-ui/clean-log-message] says that no problems were found', async () => {
    const element = await show(cleanRun);

    expect(element.textContent).toContain('No problems were found');
  });

  it('[analysis-ui/evidence-is-highlighted-in-context] shows lines 117 to 123 with their numbers and marks the evidence on line 120', async () => {
    const element = await show(evidenceRun);

    toggle(0).click();
    const request = http.expectOne((r) => r.url === `/api/runs/${evidenceRun.id}/lines`);
    expect(request.request.params.get('from')).toBe('117');
    expect(request.request.params.get('to')).toBe('123');
    request.flush(lines117To123);
    await harness.fixture.whenStable();

    expect(rowNumbers()).toEqual(['117', '118', '119', '120', '121', '122', '123']);
    const marks = element.querySelectorAll('mark');
    expect(marks.length).toBe(1);
    expect(marks[0].textContent).toBe('connection pool exhausted');
    expect(marks[0].closest('li')?.querySelector('.number')?.textContent?.trim()).toBe('120');
  });

  it('polls every second for the first minute, then every 5 seconds', async () => {
    const first = await open(queuedRun.id);
    vi.useFakeTimers();
    first.flush(queuedRun);

    for (let second = 1; second <= 60; second++) {
      await vi.advanceTimersByTimeAsync(1_000);
      readAgain().flush(queuedRun);
    }
    await vi.advanceTimersByTimeAsync(4_999);
    http.expectNone(`/api/runs/${queuedRun.id}`);
    await vi.advanceTimersByTimeAsync(1);
    readAgain().flush(queuedRun);
  });

  it('stops polling when the run finishes', async () => {
    const first = await open(queuedRun.id);
    vi.useFakeTimers();
    first.flush(queuedRun);

    await vi.advanceTimersByTimeAsync(1_000);
    readAgain().flush({ ...cleanRun, id: queuedRun.id });
    await vi.advanceTimersByTimeAsync(10_000);

    http.expectNone(`/api/runs/${queuedRun.id}`);
  });

  it('stops polling when the page is destroyed', async () => {
    const first = await open(queuedRun.id);
    vi.useFakeTimers();
    first.flush(queuedRun);

    harness.fixture.destroy();
    await vi.advanceTimersByTimeAsync(10_000);

    http.expectNone(`/api/runs/${queuedRun.id}`);
  });

  it('shows the status as it changes from queued to running, in an aria-live region', async () => {
    const first = await open(queuedRun.id);
    vi.useFakeTimers();
    first.flush(queuedRun);
    render();
    expect(live()).toContain('Queued');

    await vi.advanceTimersByTimeAsync(1_000);
    readAgain().flush(runningRun);
    render();

    expect(live()).toContain('Running');
  });

  it('shows the retry notice for a 503 or a network error, in an aria-live region, and keeps polling', async () => {
    const first = await open(queuedRun.id);
    vi.useFakeTimers();
    flushError(first, problems.serviceUnavailable);
    render();
    expect(live()).toContain('Cannot reach the server; retrying');

    await vi.advanceTimersByTimeAsync(1_000);
    readAgain().error(new ProgressEvent('error'));
    render();
    expect(live()).toContain('Cannot reach the server; retrying');

    await vi.advanceTimersByTimeAsync(1_000);
    readAgain().flush({ ...cleanRun, id: queuedRun.id });
    render();
    expect(live()).not.toContain('Cannot reach the server');
  });

  it('says there is no run at this address for a 404, in an aria-live region, and stops', async () => {
    const first = await open(queuedRun.id);
    vi.useFakeTimers();
    flushError(first, problems.runNotFound);
    render();

    expect(live()).toContain('There is no run at this address');
    await vi.advanceTimersByTimeAsync(10_000);
    http.expectNone(`/api/runs/${queuedRun.id}`);
  });

  it("keeps the model's order within a severity, and puts info last", async () => {
    await show(mixedSeveritiesRun);

    expect(titles()).toEqual(['Disk full', 'First error', 'Second error', 'Cache warmed']);
  });

  it('has one filter button per severity present, with its count, plus All', async () => {
    const element = await show(mixedSeveritiesRun);
    const labels = Array.from(element.querySelectorAll('.filter button')).map((b) =>
      b.textContent!.trim(),
    );
    expect(labels).toEqual(['All', 'Critical (1)', 'Error (2)', 'Info (1)']);

    filterButton('Error').click();
    await harness.fixture.whenStable();
    expect(titles()).toEqual(['First error', 'Second error']);

    filterButton('All').click();
    await harness.fixture.whenStable();
    expect(titles()).toEqual(['Disk full', 'First error', 'Second error', 'Cache warmed']);
  });

  it("renders the model's texts with markup as text", async () => {
    const element = await show(markupRun);

    expect(element.querySelector('b')).toBeNull();
    expect(element.querySelector('.summary')?.textContent).toContain(
      'A summary with <b>bold</b> text.',
    );
    expect(titles()).toEqual(['A <b>bold</b> title']);
    expect(element.textContent).toContain('An explanation with <b>bold</b> text.');
    expect(element.textContent).toContain('An action with <b>bold</b> text.');
  });

  it('requests lines 37 to 43 and 57 to 63 at once for a finding that cites lines 40 and 60', async () => {
    await show(evidenceRun);

    toggle(1).click();
    const requests = lineRequests();

    expect(requests.map((r) => [r.request.params.get('from'), r.request.params.get('to')])).toEqual(
      [
        ['37', '43'],
        ['57', '63'],
      ],
    );
    requests[0].flush(lines37To43);
    requests[1].flush(lines57To63);
  });

  it('expands one finding at a time, and reads its lines only once', async () => {
    const element = await show(evidenceRun);

    toggle(0).click();
    http.expectOne((r) => r.url === `/api/runs/${evidenceRun.id}/lines`).flush(lines117To123);
    await harness.fixture.whenStable();
    toggle(1).click();
    const [first, second] = lineRequests();
    first.flush(lines37To43);
    second.flush(lines57To63);
    await harness.fixture.whenStable();

    expect(rowNumbers()).toContain('40');
    expect(rowNumbers()).not.toContain('120');
    expect(element.querySelectorAll('.evidence-toggle[aria-expanded="true"]').length).toBe(1);

    toggle(0).click();
    await harness.fixture.whenStable();
    expect(lineRequests()).toEqual([]);
    expect(rowNumbers()).toContain('120');
  });
});
