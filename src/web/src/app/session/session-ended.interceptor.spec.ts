import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Account, ApiClient } from '../api/api-client';
import { ana, problems, runListPage } from '../testing/answers';
import { answerSession, flushError, provideTestApp } from '../testing/test-app';
import { Session } from './session';

describe('sessionEndedInterceptor', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  async function openAt(url: string, account: Account | null): Promise<void> {
    const navigation = harness.navigateByUrl(url);
    await answerSession(http, account);
    await navigation;
  }

  it('sends SIGN_IN_REQUIRED to sign-in with the current URL and ended=1', async () => {
    await openAt('/runs', ana);

    // The run list's own request finds that the session has ended.
    flushError(http.expectOne('/api/runs'), problems.signInRequired);
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/sign-in?returnUrl=%2Fruns&ended=1');
    expect(TestBed.inject(Session).account()).toBeNull();
  });

  it('leaves INVALID_CREDENTIALS from sign-in alone', async () => {
    await openAt('/sign-in', null);

    TestBed.inject(ApiClient)
      .signIn('ana@example.com', 'wrong password!')
      .subscribe({ error: () => {} });
    flushError(http.expectOne('/api/auth/sign-in'), problems.invalidCredentials);
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/sign-in');
  });

  it('leaves a 401 from GET /auth/me alone', async () => {
    await openAt('/runs', ana);
    http.expectOne('/api/runs').flush(runListPage);

    TestBed.inject(ApiClient)
      .me()
      .subscribe({ error: () => {} });
    flushError(http.expectOne('/api/auth/me'), problems.signInRequired);
    await harness.fixture.whenStable();

    expect(TestBed.inject(Router).url).toBe('/runs');
  });
});
