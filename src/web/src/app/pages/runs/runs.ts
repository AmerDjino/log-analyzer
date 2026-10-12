import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { ApiClient, RunPage } from '../../api/api-client';
import { apiErrorOf } from '../../api/api-error';
import { messageFor } from '../../messages';

/** The person's runs, newest first as the API returns them. Only the first page: paging is not part of this change. */
@Component({
  selector: 'app-runs-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './runs.html',
  styleUrl: './runs.css',
})
export class RunsPage {
  protected readonly runs = signal<RunPage['items'] | null>(null);
  protected readonly error = signal('');

  constructor() {
    inject(ApiClient)
      .listRuns()
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: (page) => this.runs.set(page.items),
        error: (error: unknown) => this.error.set(messageFor(apiErrorOf(error)).text),
      });
  }

  protected capitalized(text: string): string {
    return text[0].toUpperCase() + text.slice(1);
  }
}
