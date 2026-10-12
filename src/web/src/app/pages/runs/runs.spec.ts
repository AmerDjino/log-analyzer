import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ana, runListPage, warningCriticalErrorRun } from '../../testing/answers';
import { answerSession, provideTestApp } from '../../testing/test-app';

describe('RunsPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  async function openList(): Promise<HTMLElement> {
    const navigation = harness.navigateByUrl('/runs');
    await answerSession(http, ana);
    await navigation;
    http.expectOne('/api/runs').flush(runListPage);
    await harness.fixture.whenStable();
    return harness.routeNativeElement!;
  }

  it("[analysis-ui/past-run-opens-from-the-list] opens a run's page with its findings when it is selected", async () => {
    const page = await openList();

    page.querySelector<HTMLAnchorElement>('tbody tr a')!.click();
    const read = await vi.waitFor(() => http.expectOne(`/api/runs/${warningCriticalErrorRun.id}`));
    read.flush(warningCriticalErrorRun);
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe(`/runs/${warningCriticalErrorRun.id}`);
    const titles = Array.from(harness.routeNativeElement!.querySelectorAll('.finding .title')).map(
      (t) => t.textContent!.trim(),
    );
    expect(titles).toEqual(['Connection pool exhausted', 'Payment failed', 'Slow response']);
  });

  it("shows each run's file name, submission time, status and highest severity, newest first", async () => {
    const page = await openList();
    const rows = Array.from(page.querySelectorAll('tbody tr'));
    const cells = rows.map((row) =>
      Array.from(row.querySelectorAll('td')).map((cell) => cell.textContent!.trim()),
    );

    expect(cells.map((row) => row[0])).toEqual(['orders-api.log', 'Pasted log']);
    expect(rows.map((row) => row.querySelector('time')?.getAttribute('datetime'))).toEqual([
      '2026-10-10T09:45:00+00:00',
      '2026-10-10T08:30:00+00:00',
    ]);
    expect(cells.map((row) => row[2])).toEqual(['Completed', 'Queued']);
    expect(cells.map((row) => row[3])).toEqual(['Critical', '—']);
  });
});
