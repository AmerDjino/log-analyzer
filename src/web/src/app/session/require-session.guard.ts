import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Session } from './session';

/** Sends a signed-out person to sign-in, which brings them back to the page they opened. */
export const requireSession: CanActivateFn = async (_route, state) => {
  const router = inject(Router);
  return (await inject(Session).load()) !== null
    ? true
    : router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
};

/** Sends a signed-in person who opens sign-in or account creation to the submit page. */
export const redirectSignedIn: CanActivateFn = async () => {
  const router = inject(Router);
  return (await inject(Session).load()) === null ? true : router.parseUrl('/');
};
