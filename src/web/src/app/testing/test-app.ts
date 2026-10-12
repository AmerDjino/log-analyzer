import { EnvironmentProviders, Provider } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Account } from '../api/api-client';
import { routes } from '../app.routes';
import { sessionEndedInterceptor } from '../session/session-ended.interceptor';
import { ErrorAnswer, problems } from './answers';

/**
 * The providers of app.config.ts for unit tests: the app's routes and its HTTP setup, with every request answered
 * through HttpTestingController instead of the network.
 */
export function provideTestApp(): (Provider | EnvironmentProviders)[] {
  return [
    provideRouter(routes),
    provideHttpClient(withInterceptors([sessionEndedInterceptor])),
    provideHttpClientTesting(),
  ];
}

/**
 * Answers the guard's GET /api/auth/me with the account, or with 401 SIGN_IN_REQUIRED for null. Start the navigation
 * without awaiting it, call this, then await the navigation: the router holds it until the guard has its answer.
 */
export async function answerSession(
  http: HttpTestingController,
  account: Account | null,
): Promise<void> {
  const request = await vi.waitFor(() => http.expectOne('/api/auth/me'));
  if (account === null) {
    flushError(request, problems.signInRequired);
  } else {
    request.flush(account);
  }
}

export function flushError(request: TestRequest, answer: ErrorAnswer): void {
  request.flush(answer.body, { status: answer.status, statusText: 'Error' });
}

/** Types a value into the input or textarea that the selector finds, as a person would. */
export function typeInto(root: HTMLElement, selector: string, value: string): void {
  const field = root.querySelector<HTMLInputElement | HTMLTextAreaElement>(selector);
  if (field === null) {
    throw new Error(`No field matches ${selector}.`);
  }
  field.value = value;
  field.dispatchEvent(new Event('input'));
}
