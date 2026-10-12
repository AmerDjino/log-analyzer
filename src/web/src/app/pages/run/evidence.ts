import { Component, input } from '@angular/core';

/** A range of lines, both ends included. */
export interface LineWindow {
  from: number;
  to: number;
}

/** A piece of a line, marked when it is an occurrence of the evidence. */
export interface Segment {
  text: string;
  marked: boolean;
}

export interface EvidenceRow {
  number: number;
  text: string;
  cited: boolean;
  segments: Segment[];
}

/**
 * Three lines either side of each cited line, starting no lower than line 1 and ending no later than the last line,
 * merged where they overlap or touch.
 */
export function windowsFor(lineRefs: readonly number[], lineCount: number): LineWindow[] {
  const windows: LineWindow[] = [];
  for (const line of [...new Set(lineRefs)].sort((a, b) => a - b)) {
    const from = Math.max(1, line - 3);
    const to = Math.min(lineCount, line + 3);
    const last = windows.at(-1);
    if (last !== undefined && from <= last.to + 1) {
      last.to = Math.max(last.to, to);
    } else {
      windows.push({ from, to });
    }
  }
  return windows;
}

// The characters .NET's char.IsWhiteSpace accepts, which the validator trims from the evidence. JavaScript's trim()
// differs: it also trims U+FEFF, and keeps U+0085.
const apiWhiteSpace =
  '[\\t\\n\\v\\f\\r \\u0085\\u00a0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000]';
const edges = new RegExp(`^${apiWhiteSpace}+|${apiWhiteSpace}+$`, 'g');

export function trimEvidence(evidence: string): string {
  return evidence.replace(edges, '');
}

/** A line split at every exact occurrence of the evidence, letter case included, as the validator matches it. */
export function segmentsOf(text: string, evidence: string): Segment[] {
  if (evidence === '') {
    return [{ text, marked: false }];
  }

  const segments: Segment[] = [];
  let start = 0;
  for (let at = text.indexOf(evidence); at !== -1; at = text.indexOf(evidence, start)) {
    if (at > start) {
      segments.push({ text: text.slice(start, at), marked: false });
    }
    segments.push({ text: evidence, marked: true });
    start = at + evidence.length;
  }
  if (start < text.length || segments.length === 0) {
    segments.push({ text: text.slice(start), marked: false });
  }
  return segments;
}

/** The rows of a window. Only a cited line can carry a mark; context lines never do. */
export function evidenceRows(
  lines: readonly { number: number; text: string }[],
  cited: ReadonlySet<number>,
  evidence: string,
): EvidenceRow[] {
  const excerpt = trimEvidence(evidence);
  return lines.map(({ number, text }) => ({
    number,
    text,
    cited: cited.has(number),
    segments: cited.has(number) ? segmentsOf(text, excerpt) : [{ text, marked: false }],
  }));
}

/** A window of numbered log lines. Every text is interpolated, never inserted as HTML, because a log can hold markup. */
@Component({
  selector: 'app-evidence-lines',
  template: `
    <ol class="evidence-lines">
      @for (row of rows(); track row.number) {
        <li [class.cited]="row.cited">
          <span class="number">{{ row.number }}</span>
          <!-- On one line: the line's white space is shown as is, so the template must add none. -->
          <!-- prettier-ignore -->
          <code>@for (segment of row.segments; track $index) {@if (segment.marked) {<mark>{{ segment.text }}</mark>} @else {{{ segment.text }}}}</code>
        </li>
      }
    </ol>
  `,
  styles: `
    .evidence-lines {
      margin: 0.5rem 0;
      padding: 0;
      list-style: none;
      border: 1px solid var(--border);
      border-radius: 6px;
      overflow-x: auto;
    }

    li {
      display: flex;
      gap: 1rem;
      padding: 0 0.5rem;
    }

    li.cited {
      background: var(--cited);
    }

    .number {
      min-width: 3rem;
      text-align: right;
      color: var(--muted);
      user-select: none;
    }

    .number,
    code {
      font-family: ui-monospace, 'Cascadia Mono', Consolas, monospace;
      white-space: pre-wrap;
    }
  `,
})
export class EvidenceLines {
  readonly rows = input.required<EvidenceRow[]>();
}
