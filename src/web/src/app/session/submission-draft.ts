import { Injectable, signal } from '@angular/core';

/**
 * What the person has pasted or chosen on the submit page but not sent yet. It lives in memory only, so it survives
 * the trip to sign-in when a session ends, but not a reload: logs may hold secrets, and can be too large for browser
 * storage.
 */
@Injectable({ providedIn: 'root' })
export class SubmissionDraft {
  readonly text = signal('');
  readonly file = signal<File | null>(null);
  private owner: string | null = null;

  /** Keeps the draft for the account it belongs to, and clears it when another account signs in on this tab. */
  ownedBy(accountId: string): void {
    if (this.owner !== null && this.owner !== accountId) {
      this.clear();
    }
    this.owner = accountId;
  }

  clear(): void {
    this.text.set('');
    this.file.set(null);
  }
}
