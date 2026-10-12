import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideTestApp } from '../testing/test-app';
import {
  ana,
  evidenceRun,
  limits,
  lines117To123,
  runListPage,
  submissionAccepted,
} from '../testing/answers';
import { ApiClient } from './api-client';

describe('ApiClient', () => {
  let api: ApiClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideTestApp() });
    api = TestBed.inject(ApiClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('registers with the email address and password as JSON', () => {
    let answer: unknown;
    api.register('ana@example.com', 'correct horse battery').subscribe((a) => (answer = a));

    const request = http.expectOne('/api/auth/register');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'ana@example.com',
      password: 'correct horse battery',
    });
    request.flush(ana, { status: 201, statusText: 'Created' });
    expect(answer).toEqual(ana);
  });

  it('signs in with the email address and password as JSON', () => {
    let answer: unknown;
    api.signIn('ana@example.com', 'correct horse battery').subscribe((a) => (answer = a));

    const request = http.expectOne('/api/auth/sign-in');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'ana@example.com',
      password: 'correct horse battery',
    });
    request.flush(ana);
    expect(answer).toEqual(ana);
  });

  it('signs out without a body', () => {
    let done = false;
    api.signOut().subscribe({ complete: () => (done = true) });

    const request = http.expectOne('/api/auth/sign-out');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('reads the signed-in account', () => {
    let answer: unknown;
    api.me().subscribe((a) => (answer = a));

    const request = http.expectOne('/api/auth/me');
    expect(request.request.method).toBe('GET');
    request.flush(ana);
    expect(answer).toEqual(ana);
  });

  it('reads the submission size limit', () => {
    let answer: unknown;
    api.limits().subscribe((a) => (answer = a));

    const request = http.expectOne('/api/runs/limits');
    expect(request.request.method).toBe('GET');
    request.flush(limits);
    expect(answer).toEqual(limits);
  });

  it('sends a paste as text/plain without a file name', () => {
    let answer: unknown;
    api.submitPaste('line 1\nline 2\n').subscribe((a) => (answer = a));

    const request = http.expectOne((r) => r.method === 'POST' && r.url === '/api/runs');
    expect(request.request.body).toBe('line 1\nline 2\n');
    expect(request.request.headers.get('Content-Type')).toBe('text/plain');
    expect(request.request.params.has('fileName')).toBe(false);
    request.flush(submissionAccepted, { status: 202, statusText: 'Accepted' });
    expect(answer).toEqual(submissionAccepted);
  });

  it('sends a file as the same File object, as text/plain, with its file name', () => {
    const file = new File(['line 1\n'], 'orders-api.log', { type: 'application/octet-stream' });
    let answer: unknown;
    api.submitFile(file).subscribe((a) => (answer = a));

    const request = http.expectOne((r) => r.method === 'POST' && r.url === '/api/runs');
    expect(request.request.body).toBe(file);
    expect(request.request.headers.get('Content-Type')).toBe('text/plain');
    expect(request.request.params.get('fileName')).toBe('orders-api.log');
    request.flush(submissionAccepted, { status: 202, statusText: 'Accepted' });
    expect(answer).toEqual(submissionAccepted);
  });

  it('lists the runs', () => {
    let answer: unknown;
    api.listRuns().subscribe((a) => (answer = a));

    const request = http.expectOne('/api/runs');
    expect(request.request.method).toBe('GET');
    request.flush(runListPage);
    expect(answer).toEqual(runListPage);
  });

  it('reads a run by its id', () => {
    let answer: unknown;
    api.getRun(evidenceRun.id).subscribe((a) => (answer = a));

    const request = http.expectOne(`/api/runs/${evidenceRun.id}`);
    expect(request.request.method).toBe('GET');
    request.flush(evidenceRun);
    expect(answer).toEqual(evidenceRun);
  });

  it("reads a range of a run's lines", () => {
    let answer: unknown;
    api.getLines(evidenceRun.id, 117, 123).subscribe((a) => (answer = a));

    const request = http.expectOne((r) => r.url === `/api/runs/${evidenceRun.id}/lines`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('from')).toBe('117');
    expect(request.request.params.get('to')).toBe('123');
    request.flush(lines117To123);
    expect(answer).toEqual(lines117To123);
  });
});
