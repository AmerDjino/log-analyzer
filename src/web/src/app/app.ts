import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { ApiClient } from './api/api-client';
import { apiErrorOf } from './api/api-error';
import { messageFor } from './messages';
import { Session } from './session/session';

@Component({
  imports: [RouterLink, RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);

  protected readonly title = signal('Log Analyzer');
  protected readonly session = inject(Session);
  protected readonly signOutError = signal('');

  protected signOut(): void {
    this.signOutError.set('');
    this.api.signOut().subscribe({
      complete: () => this.signedOut(),
      error: (error: unknown) => {
        const failure = apiErrorOf(error);
        // A session that has already ended is signed out too. Any other failure leaves the session alive on the
        // server, so the page must not pretend otherwise.
        if (failure.status === 401) {
          this.signedOut();
        } else {
          this.signOutError.set(messageFor(failure).text);
        }
      },
    });
  }

  private signedOut(): void {
    this.session.signedOut();
    void this.router.navigateByUrl('/sign-in');
  }
}
