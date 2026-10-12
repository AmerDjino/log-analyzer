import { TestBed } from '@angular/core/testing';
import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import {
  ana,
  limits,
  problems,
  queuedRun,
  runListPage,
  submissionAccepted,
} from '../../testing/answers';
import { answerSession, flushError, provideTestApp, typeInto } from '../../testing/test-app';

const log = '2026-10-10T09:00:01Z info: Orders.Api[0] Application started\n';

describe('SubmitPage', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  /** Opens the submit page signed in, and answers its limits request with 1 MB, or with a 503. */
  async function open(
    limitsAnswer: 'one megabyte' | 'failure' = 'one megabyte',
  ): Promise<HTMLElement> {
    const navigation = harness.navigateByUrl('/');
    await answerSession(http, ana);
    await navigation;
    const request = http.expectOne('/api/runs/limits');
    if (limitsAnswer === 'one megabyte') {
      request.flush(limits);
    } else {
      flushError(request, problems.serviceUnavailable);
    }
    await harness.fixture.whenStable();
    return page();
  }

  function page(): HTMLElement {
    return harness.routeNativeElement!;
  }

  function submitButton(): HTMLButtonElement {
    return page().querySelector<HTMLButtonElement>('button[type="submit"]')!;
  }

  function pasteArea(): HTMLTextAreaElement {
    return page().querySelector<HTMLTextAreaElement>('#log')!;
  }

  async function choose(file: File): Promise<void> {
    const input = page().querySelector<HTMLInputElement>('#file')!;
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    await harness.fixture.whenStable();
  }

  async function paste(text: string): Promise<void> {
    typeInto(page(), '#log', text);
    await harness.fixture.whenStable();
  }

  async function submit(): Promise<void> {
    page().querySelector('form')!.dispatchEvent(new Event('submit'));
    await harness.fixture.whenStable();
  }

  function submission(): TestRequest {
    return http.expectOne((r) => r.method === 'POST' && r.url === '/api/runs');
  }

  async function answer(
    request: TestRequest,
    error: Parameters<typeof flushError>[1],
  ): Promise<void> {
    flushError(request, error);
    await harness.fixture.whenStable();
  }

  it('[analysis-ui/submit-is-disabled-for-empty-input] disables submit while the paste area is empty and no file is chosen', async () => {
    await open();

    expect(pasteArea().value).toBe('');
    expect(submitButton().disabled).toBe(true);
  });

  it('[analysis-ui/oversized-file-is-refused-before-upload] refuses a 1.5 MB file against a 1 MB limit and sends nothing when submit is clicked', async () => {
    await open();

    await choose(new File([new Uint8Array(1_572_864)], 'big.log'));
    expect(page().textContent).toContain('larger than the 1 MB limit');
    submitButton().click();
    await submit();

    http.expectNone((r) => r.method === 'POST' && r.url.split('?')[0] === '/api/runs');
  });

  it("[analysis-ui/successful-submission-opens-the-run] opens the new run's page when the API accepts the paste", async () => {
    await open();

    await paste(log);
    await submit();
    submission().flush(submissionAccepted, { status: 202, statusText: 'Accepted' });
    const read = await vi.waitFor(() => http.expectOne(`/api/runs/${queuedRun.id}`));
    read.flush({ ...queuedRun, status: 'failed', failureReason: 'INTERRUPTED' });
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe(`/runs/${queuedRun.id}`);
  });

  it('[analysis-ui/server-side-size-rejection-keeps-the-input] explains INPUT_TOO_LARGE and keeps the pasted text', async () => {
    await open();

    await paste(log);
    await submit();
    await answer(submission(), problems.inputTooLarge);

    expect(page().textContent).toContain('larger than the 1 MB limit');
    expect(pasteArea().value).toBe(log);
  });

  it("disables the paste area while a file is chosen, shows the file's name and size, and enables it again once the file is removed", async () => {
    await open();

    await choose(new File(['line 1\n'], 'orders-api.log'));
    expect(pasteArea().disabled).toBe(true);
    expect(page().textContent).toContain('orders-api.log');
    expect(page().textContent).toContain('7 bytes');

    page().querySelector<HTMLButtonElement>('.remove-file')!.click();
    await harness.fixture.whenStable();
    expect(pasteArea().disabled).toBe(false);
  });

  it('asks for the limits only once, once their answer has arrived', async () => {
    await open();

    await harness.navigateByUrl('/runs');
    http.expectOne('/api/runs').flush(runListPage);
    await harness.navigateByUrl('/');

    http.expectNone('/api/runs/limits');
  });

  it('leaves files unchecked when the limits request fails', async () => {
    await open('failure');

    await choose(new File([new Uint8Array(1_572_864)], 'big.log'));
    expect(page().textContent).not.toContain('limit');
    await submit();

    const request = submission();
    expect(request.request.body).toBeInstanceOf(File);
    await answer(request, problems.inputTooLarge);
  });

  it('sends a paste of only white space unchanged, and explains EMPTY_INPUT, keeping the input', async () => {
    await open();

    await paste('  \n\t\n');
    expect(submitButton().disabled).toBe(false);
    await submit();
    const request = submission();
    expect(request.request.body).toBe('  \n\t\n');
    await answer(request, problems.emptyInput);

    expect(page().textContent).toContain('The log is empty');
    expect(pasteArea().value).toBe('  \n\t\n');
  });

  it('sends an empty file too, and explains EMPTY_INPUT', async () => {
    await open();
    const empty = new File([], 'empty.log');

    await choose(empty);
    expect(submitButton().disabled).toBe(false);
    await submit();
    const request = submission();
    expect(request.request.body).toBe(empty);
    await answer(request, problems.emptyInput);

    expect(page().textContent).toContain('The log is empty');
  });

  it("gives nginx's 413 without a code the size message", async () => {
    await open();

    await paste(log);
    await submit();
    await answer(submission(), problems.nginxTooLarge);

    expect(page().textContent).toContain('larger than the 1 MB limit');
  });

  it('disables submit while a submission is being sent, and shows a request error in an aria-live region', async () => {
    await open();

    await paste(log);
    await submit();
    expect(submitButton().disabled).toBe(true);
    await answer(submission(), problems.serviceUnavailable);

    expect(submitButton().disabled).toBe(false);
    expect(page().querySelector('.error[aria-live]')?.textContent).toContain('The request failed');
  });
});
