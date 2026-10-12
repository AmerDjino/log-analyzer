import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ana, limits, problems, runListPage } from '../testing/answers';
import { answerSession, flushError, provideTestApp } from '../testing/test-app';
import { Session } from './session';

describe('requireSession', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('asks GET /auth/me once for several navigations', async () => {
    const harness = await RouterTestingHarness.create();

    const first = harness.navigateByUrl('/runs');
    await answerSession(http, ana);
    await first;
    http.expectOne('/api/runs').flush(runListPage);
    await harness.navigateByUrl('/');
    http.expectOne('/api/runs/limits').flush(limits);
    await harness.navigateByUrl('/runs');
    http.expectOne('/api/runs').flush(runListPage);

    http.expectNone('/api/auth/me');
    expect(TestBed.inject(Router).url).toBe('/runs');
  });

  it('asks GET /auth/me again after a failure other than 401', async () => {
    const session = TestBed.inject(Session);

    const first = session.load();
    flushError(http.expectOne('/api/auth/me'), problems.serviceUnavailable);
    expect(await first).toBeNull();
    expect(session.account()).toBeUndefined();

    const second = session.load();
    http.expectOne('/api/auth/me').flush(ana);
    expect(await second).toEqual(ana);
  });
});
