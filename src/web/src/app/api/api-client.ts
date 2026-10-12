import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { components, paths } from './schema';

type Schemas = components['schemas'];

export type Account = Schemas['AccountResponse'];
export type Run = Schemas['RunResponse'];
export type RunListPage = Schemas['RunPageResponse'];
export type RunLines = Schemas['RunLinesResponse'];
export type Submission = Schemas['SubmissionResponse'];
export type SubmissionLimitsAnswer = Schemas['SubmissionLimitsResponse'];

// The API takes the log itself as the body. A file goes with this type too, never with its own: routing answers any
// other type with a 415 that carries no code.
const textPlain = new HttpHeaders({ 'Content-Type': 'text/plain' });

/** The API's endpoints, typed with the types generated from its OpenAPI document. */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);

  register(email: string, password: string): Observable<Account> {
    return this.http.post<Account>(url('/auth/register'), { email, password });
  }

  signIn(email: string, password: string): Observable<Account> {
    return this.http.post<Account>(url('/auth/sign-in'), { email, password });
  }

  signOut(): Observable<void> {
    return this.http.post<void>(url('/auth/sign-out'), null);
  }

  me(): Observable<Account> {
    return this.http.get<Account>(url('/auth/me'));
  }

  limits(): Observable<SubmissionLimitsAnswer> {
    return this.http.get<SubmissionLimitsAnswer>(url('/runs/limits'));
  }

  submitPaste(text: string): Observable<Submission> {
    return this.http.post<Submission>(url('/runs'), text, { headers: textPlain });
  }

  // The File itself is the body, so the browser sends its bytes unchanged. Reading it with file.text() would replace
  // invalid UTF-8 with U+FFFD, and the API could no longer refuse it.
  submitFile(file: File): Observable<Submission> {
    return this.http.post<Submission>(url('/runs'), file, {
      headers: textPlain,
      params: { fileName: file.name },
    });
  }

  listRuns(): Observable<RunListPage> {
    return this.http.get<RunListPage>(url('/runs'));
  }

  getRun(id: string): Observable<Run> {
    return this.http.get<Run>(url('/runs/{id}', id));
  }

  getLines(id: string, from: number, to: number): Observable<RunLines> {
    return this.http.get<RunLines>(url('/runs/{id}/lines', id), { params: { from, to } });
  }
}

// A path from the OpenAPI document, so a misspelled path does not compile, behind the /api prefix that nginx and the
// dev server's proxy strip.
function url(path: keyof paths, id?: string): string {
  return `/api${id === undefined ? path : path.replace('{id}', encodeURIComponent(id))}`;
}
