import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ana } from '../testing/answers';
import { answerSession, provideTestApp } from '../testing/test-app';

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
    await harness.navigateByUrl('/');
    await harness.navigateByUrl('/runs');

    http.expectNone('/api/auth/me');
    expect(TestBed.inject(Router).url).toBe('/runs');
  });
});
