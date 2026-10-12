import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { ana, bob, limits, queuedRun, submissionAccepted } from '../testing/answers';
import { answerSession, provideTestApp, typeInto } from '../testing/test-app';
import { Session } from './session';
import { SubmissionDraft } from './submission-draft';

describe('SubmissionDraft', () => {
  let session: Session;
  let draft: SubmissionDraft;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    session = TestBed.inject(Session);
    draft = TestBed.inject(SubmissionDraft);
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('is cleared on sign-out', () => {
    session.signedIn(ana);
    draft.text.set('2026-10-10T09:00:04Z fail: Payment failed');

    session.signedOut();

    expect(draft.text()).toBe('');
    expect(draft.file()).toBeNull();
  });

  it('is cleared when another account signs in', () => {
    session.signedIn(ana);
    draft.text.set('2026-10-10T09:00:04Z fail: Payment failed');
    session.ended();

    session.signedIn(bob);

    expect(draft.text()).toBe('');
  });

  it('is kept when the same account signs in again', () => {
    session.signedIn(ana);
    draft.text.set('2026-10-10T09:00:04Z fail: Payment failed');
    session.ended();

    session.signedIn(ana);

    expect(draft.text()).toBe('2026-10-10T09:00:04Z fail: Payment failed');
  });

  it('keeps a chosen file like pasted text, as the same File object', () => {
    const file = new File(['line 1\n'], 'orders-api.log');
    session.signedIn(ana);
    draft.file.set(file);
    session.ended();

    session.signedIn(ana);

    expect(draft.file()).toBe(file);
  });

  it('writes nothing to localStorage or sessionStorage', () => {
    session.signedIn(ana);
    draft.text.set('2026-10-10T09:00:04Z fail: Payment failed');
    draft.file.set(new File(['line 1\n'], 'orders-api.log'));
    session.ended();
    session.signedIn(ana);

    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('is cleared after a successful submission', async () => {
    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();
    const navigation = harness.navigateByUrl('/');
    await answerSession(http, ana);
    await navigation;
    http.expectOne('/api/runs/limits').flush(limits);
    await harness.fixture.whenStable();

    typeInto(harness.routeNativeElement!, '#log', '2026-10-10T09:00:04Z fail: Payment failed');
    harness.routeNativeElement!.querySelector('form')!.dispatchEvent(new Event('submit'));
    http
      .expectOne((r) => r.method === 'POST' && r.url === '/api/runs')
      .flush(submissionAccepted, {
        status: 202,
        statusText: 'Accepted',
      });
    await vi
      .waitFor(() => http.expectOne(`/api/runs/${queuedRun.id}`))
      .then((read) => read.flush({ ...queuedRun, status: 'failed', failureReason: 'INTERRUPTED' }));
    await harness.fixture.whenStable();

    expect(draft.text()).toBe('');
  });
});
