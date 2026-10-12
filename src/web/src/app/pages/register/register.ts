import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Observable, of, switchMap, tap } from 'rxjs';
import { ApiClient } from '../../api/api-client';
import { apiErrorOf } from '../../api/api-error';
import { messageFor } from '../../messages';
import { Session } from '../../session/session';

@Component({
  selector: 'app-register-page',
  imports: [RouterLink],
  templateUrl: './register.html',
})
export class RegisterPage {
  private readonly api = inject(ApiClient);
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly email = signal('');
  protected readonly password = signal('');
  protected readonly emailError = signal('');
  protected readonly passwordError = signal('');
  protected readonly error = signal('');
  protected readonly sending = signal(false);
  // The address registered by an earlier try whose sign-in failed. Trying again only signs in: registering again
  // would answer that the address is taken, by the person's own new account.
  private registered: string | null = null;

  // Registration does not sign anyone in, so the page signs in with the same credentials right after.
  protected createAccount(event: Event): void {
    event.preventDefault();
    this.emailError.set('');
    this.passwordError.set('');
    this.error.set('');
    this.sending.set(true);
    const email = this.email();
    const password = this.password();
    const registration: Observable<unknown> =
      this.registered === email
        ? of(null)
        : this.api.register(email, password).pipe(tap(() => (this.registered = email)));
    registration.pipe(switchMap(() => this.api.signIn(email, password))).subscribe({
      next: (account) => {
        this.session.signedIn(account);
        void this.router.navigateByUrl('/');
      },
      error: (error: unknown) => {
        const message = messageFor(apiErrorOf(error));
        const target =
          message.field === 'email'
            ? this.emailError
            : message.field === 'password'
              ? this.passwordError
              : this.error;
        target.set(message.text);
        this.sending.set(false);
      },
    });
  }

  protected valueOf(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }
}
