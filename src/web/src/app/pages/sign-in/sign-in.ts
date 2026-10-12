import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiClient } from '../../api/api-client';
import { apiErrorOf } from '../../api/api-error';
import { errorMessages, messageFor } from '../../messages';
import { Session } from '../../session/session';

@Component({
  selector: 'app-sign-in-page',
  imports: [RouterLink],
  templateUrl: './sign-in.html',
})
export class SignInPage {
  private readonly api = inject(ApiClient);
  private readonly session = inject(Session);
  private readonly router = inject(Router);
  private readonly query = inject(ActivatedRoute).snapshot.queryParamMap;

  protected readonly endedNote =
    this.query.get('ended') === '1' ? errorMessages.SIGN_IN_REQUIRED.text : '';
  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly error = signal('');
  protected readonly sending = signal(false);

  protected signIn(event: Event): void {
    event.preventDefault();
    this.error.set('');
    this.sending.set(true);
    this.api.signIn(this.email(), this.password()).subscribe({
      next: (account) => {
        this.session.signedIn(account);
        void this.router.navigateByUrl(appPath(this.query.get('returnUrl')));
      },
      error: (error: unknown) => {
        this.error.set(messageFor(apiErrorOf(error)).text);
        this.sending.set(false);
      },
    });
  }

  protected valueOf(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}

/** The return address when it is a path inside the app, so sign-in never sends anyone to another site; / otherwise. */
export function appPath(url: string | null): string {
  return url !== null && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\')
    ? url
    : '/';
}
