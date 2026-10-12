import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Subscription, forkJoin } from 'rxjs';
import { ApiClient, Run } from '../../api/api-client';
import { apiErrorOf } from '../../api/api-error';
import { failureReasonMessage, messageFor } from '../../messages';
import { EvidenceLines, EvidenceRow, LineWindow, evidenceRows, windowsFor } from './evidence';

type Finding = NonNullable<Run['findings']>[number];
type Severity = 'critical' | 'error' | 'warning' | 'info';

const severities: Severity[] = ['critical', 'error', 'warning', 'info'];

const statusLabels: Record<Run['status'], string> = {
  queued: 'Queued: waiting to be analyzed.',
  running: 'Running: the log is being analyzed.',
  completed: 'Completed.',
  failed: 'Failed.',
};

const retryNotice = 'Cannot reach the server; retrying.';

/** A finding with its place in the model's order, which stays its identity while the list is sorted and filtered. */
interface ListedFinding {
  finding: Finding;
  index: number;
  severity: Severity;
}

@Component({
  selector: 'app-run-page',
  imports: [EvidenceLines],
  templateUrl: './run.html',
  styleUrl: './run.css',
})
export class RunPage {
  private readonly api = inject(ApiClient);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  private readonly openedAt = Date.now();
  private timer: ReturnType<typeof setTimeout> | undefined;
  private reading: Subscription | undefined;
  private loadingLines: Subscription | undefined;
  // Lines already read, by number, so selecting a finding again sends no request.
  private readonly lines = new Map<number, string>();

  protected readonly run = signal<Run | null>(null);
  protected readonly message = signal('');
  protected readonly notice = signal('');
  protected readonly filter = signal<Severity | null>(null);
  protected readonly expanded = signal<number | null>(null);
  protected readonly evidence = signal<EvidenceRow[][] | null>(null);
  protected readonly evidenceError = signal('');

  protected readonly status = computed(() => {
    const run = this.run();
    return run === null ? '' : statusLabels[run.status];
  });

  protected readonly failure = computed(() =>
    failureReasonMessage(this.run()?.failureReason ?? null),
  );

  // Sorted by severity rank. The sort is stable, so the model's order holds within a severity.
  protected readonly findings = computed<ListedFinding[]>(() =>
    (this.run()?.findings ?? [])
      .map((finding, index) => ({ finding, index, severity: finding.severity ?? 'info' }))
      .sort((a, b) => severities.indexOf(a.severity) - severities.indexOf(b.severity)),
  );

  protected readonly filters = computed(() =>
    severities
      .map((severity) => ({
        severity,
        label: severity[0].toUpperCase() + severity.slice(1),
        count: this.findings().filter((item) => item.severity === severity).length,
      }))
      .filter((option) => option.count > 0),
  );

  protected readonly visible = computed(() => {
    const filter = this.filter();
    return filter === null
      ? this.findings()
      : this.findings().filter((item) => item.severity === filter);
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      clearTimeout(this.timer);
      this.reading?.unsubscribe();
      this.loadingLines?.unsubscribe();
    });
    this.read();
  }

  protected select(item: ListedFinding): void {
    if (this.expanded() === item.index) {
      this.expanded.set(null);
      return;
    }

    const run = this.run();
    if (run === null) {
      return;
    }
    this.expanded.set(item.index);
    this.evidence.set(null);
    this.evidenceError.set('');
    this.loadingLines?.unsubscribe();

    const lineRefs = item.finding.lineRefs.map(Number);
    const windows = windowsFor(lineRefs, Number(run.statistics.lineCount));
    const show = () =>
      this.evidence.set(
        windows.map((window) =>
          evidenceRows(this.linesOf(window), new Set(lineRefs), item.finding.evidence),
        ),
      );
    const missing = windows.filter(
      (window) => this.linesOf(window).length < window.to - window.from + 1,
    );
    if (missing.length === 0) {
      show();
      return;
    }

    // All merged windows are requested at once.
    this.loadingLines = forkJoin(
      missing.map((window) => this.api.getLines(run.id, window.from, window.to)),
    ).subscribe({
      next: (answers) => {
        for (const answer of answers) {
          for (const line of answer.lines) {
            this.lines.set(Number(line.number), line.text);
          }
        }
        show();
      },
      error: (error: unknown) => this.evidenceError.set(messageFor(apiErrorOf(error)).text),
    });
  }

  private read(): void {
    this.reading = this.api.getRun(this.id).subscribe({
      next: (run) => {
        this.run.set(run);
        this.notice.set('');
        if (run.status === 'queued' || run.status === 'running') {
          this.schedule();
        }
      },
      error: (error: unknown) => {
        const failure = apiErrorOf(error);
        if (failure.status === 0 || failure.status >= 500) {
          this.notice.set(retryNotice);
          this.schedule();
        } else {
          // 404 for a run that is not the person's; a 401 also sends them to sign-in.
          this.notice.set('');
          this.message.set(messageFor(failure).text);
        }
      },
    });
  }

  // Every second for the first minute, then every 5 seconds, so a long analysis does not cost a request a second.
  private schedule(): void {
    const delay = Date.now() - this.openedAt < 60_000 ? 1_000 : 5_000;
    this.timer = setTimeout(() => this.read(), delay);
  }

  private linesOf(window: LineWindow): { number: number; text: string }[] {
    const lines: { number: number; text: string }[] = [];
    for (let number = window.from; number <= window.to; number++) {
      const text = this.lines.get(number);
      if (text !== undefined) {
        lines.push({ number, text });
      }
    }
    return lines;
  }
}
