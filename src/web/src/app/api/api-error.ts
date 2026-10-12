import { HttpErrorResponse } from '@angular/common/http';
import { components } from './schema';

/** Every code the API reports, as its OpenAPI document lists them. */
export type ApiErrorCode = NonNullable<components['schemas']['ProblemDetails']['code']>;

/** A failed request: its status (0 when the API could not be reached) and its code, if the answer carried one. */
export interface ApiError {
  status: number;
  code: string | null;
}

// Answers the framework produces, and nginx's own error pages, carry no code.
export function apiErrorOf(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, code: null };
  }

  const body: unknown = error.error;
  const code =
    typeof body === 'object' && body !== null && 'code' in body && typeof body.code === 'string'
      ? body.code
      : null;
  return { status: error.status, code };
}
