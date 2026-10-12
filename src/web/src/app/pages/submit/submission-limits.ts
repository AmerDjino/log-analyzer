import { Injectable, inject, signal } from '@angular/core';
import { ApiClient } from '../../api/api-client';

/**
 * The size limit the API enforces, asked for the first time the submit page opens and kept for the app's lifetime: it
 * changes only when the API restarts with new settings. While it is unknown, or if asking failed, files are not
 * checked, and the API's own 413 answers instead.
 */
@Injectable({ providedIn: 'root' })
export class SubmissionLimits {
  private readonly api = inject(ApiClient);
  private asking = false;

  readonly maxSubmissionBytes = signal<number | null>(null);

  load(): void {
    if (this.asking || this.maxSubmissionBytes() !== null) {
      return;
    }

    this.asking = true;
    this.api.limits().subscribe({
      next: (limits) => this.maxSubmissionBytes.set(Number(limits.maxSubmissionBytes)),
      // A later visit asks again.
      error: () => (this.asking = false),
    });
  }
}
