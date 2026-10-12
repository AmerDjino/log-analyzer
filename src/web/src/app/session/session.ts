import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Account, ApiClient } from '../api/api-client';
import { apiErrorOf } from '../api/api-error';
import { SubmissionDraft } from './submission-draft';

/**
 * Who is signed in. The session cookie is HttpOnly, so the app cannot see it: it asks GET /auth/me, and keeps the
 * answer until a sign-in, a sign-out or an ended session changes it.
 */
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly api = inject(ApiClient);
  private readonly draft = inject(SubmissionDraft);
  // undefined until GET /auth/me has answered; null when signed out.
  private readonly current = signal<Account | null | undefined>(undefined);
  private loading: Promise<Account | null> | undefined;

  readonly account = this.current.asReadonly();

  /** The signed-in account, or null. Asks GET /auth/me until it has an answer: an account, or a 401 for signed out. */
  load(): Promise<Account | null> {
    const known = this.current();
    if (known !== undefined) {
      return Promise.resolve(known);
    }

    this.loading ??= firstValueFrom(this.api.me()).then(
      (account) => this.settle(account),
      (error: unknown) => {
        if (apiErrorOf(error).status === 401) {
          return this.settle(null);
        }
        // Any other failure, such as a 502 while the API restarts, is not kept: this navigation goes to sign-in, and
        // the next one asks again.
        this.loading = undefined;
        return null;
      },
    );
    return this.loading;
  }

  signedIn(account: Account): void {
    this.settle(account);
  }

  /** The person signed out: the unsent draft goes too. */
  signedOut(): void {
    this.draft.clear();
    this.settle(null);
  }

  /** The API said the session has ended: the draft stays, for whoever signs in next with the same account. */
  ended(): void {
    this.settle(null);
  }

  private settle(account: Account | null): Account | null {
    this.loading = undefined;
    if (account !== null) {
      this.draft.ownedBy(account.id);
    }
    this.current.set(account);
    return account;
  }
}
