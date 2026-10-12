import { Component, ElementRef, computed, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';
import { ApiClient } from '../../api/api-client';
import { apiErrorOf } from '../../api/api-error';
import { formatSize, messageFor } from '../../messages';
import { SubmissionDraft } from '../../session/submission-draft';
import { SubmissionLimits } from './submission-limits';

@Component({
  selector: 'app-submit-page',
  templateUrl: './submit.html',
})
export class SubmitPage {
  private readonly api = inject(ApiClient);
  private readonly router = inject(Router);
  private readonly limits = inject(SubmissionLimits);
  private readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  // The input lives in the draft, not here, so it survives the trip to sign-in when a session ends.
  protected readonly draft = inject(SubmissionDraft);
  protected readonly sending = signal(false);
  protected readonly error = signal('');

  protected readonly tooLarge = computed(() => {
    const file = this.draft.file();
    const limit = this.limits.maxSubmissionBytes();
    return file !== null && limit !== null && file.size > limit
      ? `This file is ${formatSize(file.size)}, larger than the ${formatSize(limit)} limit.`
      : '';
  });

  // Empty means an empty paste area and no file. White space and an empty file are sent anyway: the API decides
  // what counts as empty, and answers EMPTY_INPUT.
  protected readonly canSubmit = computed(
    () =>
      !this.sending() &&
      this.tooLarge() === '' &&
      (this.draft.text() !== '' || this.draft.file() !== null),
  );

  constructor() {
    this.limits.load();
  }

  protected choose(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file !== undefined) {
      this.draft.file.set(file);
      this.error.set('');
    }
  }

  protected removeFile(): void {
    this.draft.file.set(null);
    this.fileInput().nativeElement.value = '';
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (!this.canSubmit()) {
      return;
    }

    const file = this.draft.file();
    this.error.set('');
    this.sending.set(true);
    const request =
      file !== null ? this.api.submitFile(file) : this.api.submitPaste(this.draft.text());
    request.subscribe({
      next: (submission) => {
        this.draft.clear();
        void this.router.navigate(['/runs', submission.id]);
      },
      error: (error: unknown) => {
        const limit = this.limits.maxSubmissionBytes();
        this.error.set(messageFor(apiErrorOf(error), limit ?? undefined).text);
        this.sending.set(false);
      },
    });
  }

  protected valueOf(event: Event): string {
    return (event.target as HTMLTextAreaElement).value;
  }

  protected sizeOf(file: File): string {
    return formatSize(file.size);
  }
}
