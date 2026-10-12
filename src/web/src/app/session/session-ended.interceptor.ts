import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { apiErrorOf } from '../api/api-error';
import { Session } from './session';

/**
 * When the API says the session has ended, sends the person to sign-in with the page they were on. GET /auth/me is left
 * alone: its 401 only means signed out, which Session handles. A wrong password answers INVALID_CREDENTIALS, not this.
 */
export const sessionEndedInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(Session);
  const router = inject(Router);
  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        apiErrorOf(error).code === 'SIGN_IN_REQUIRED' &&
        request.url !== '/api/auth/me'
      ) {
        session.ended();
        void router.navigate(['/sign-in'], { queryParams: { returnUrl: router.url, ended: 1 } });
      }
      return throwError(() => error);
    }),
  );
};
