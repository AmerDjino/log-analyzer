import { TestBed } from '@angular/core/testing';
import {
  EvidenceLines,
  EvidenceRow,
  evidenceRows,
  segmentsOf,
  trimEvidence,
  windowsFor,
} from './evidence';

function marks(row: EvidenceRow): string[] {
  return row.segments.filter((segment) => segment.marked).map((segment) => segment.text);
}

describe('evidence', () => {
  it('starts windows no lower than line 1, and merges them where they overlap or touch', () => {
    expect(windowsFor([2], 500)).toEqual([{ from: 1, to: 5 }]);
    expect(windowsFor([10, 17], 500)).toEqual([{ from: 7, to: 20 }]);
    expect(windowsFor([30, 38], 500)).toEqual([
      { from: 27, to: 33 },
      { from: 35, to: 41 },
    ]);
    expect(windowsFor([50, 55], 500)).toEqual([{ from: 47, to: 58 }]);
  });

  it("trims the evidence by the API's white-space rule: U+0085 trimmed, U+FEFF kept", () => {
    const evidence = '\u0085pool exhausted﻿';
    expect(trimEvidence(evidence)).toBe('pool exhausted﻿');

    const [row] = evidenceRows(
      [{ number: 5, text: 'Database pool exhausted﻿ after 30 s' }],
      new Set([5]),
      evidence,
    );
    expect(marks(row)).toEqual(['pool exhausted﻿']);
  });

  it('marks every occurrence on a cited line, and none on a context line', () => {
    const rows = evidenceRows(
      [
        { number: 9, text: 'retry failed, then retry failed again' },
        { number: 10, text: 'a context line where retry failed too' },
      ],
      new Set([9]),
      'retry failed',
    );

    expect(marks(rows[0])).toEqual(['retry failed', 'retry failed']);
    expect(marks(rows[1])).toEqual([]);
    expect(rows[1].cited).toBe(false);
  });

  it('marks each cited line as cited, and gives one without the evidence no mark', () => {
    const rows = evidenceRows(
      [
        { number: 40, text: 'Payment for order 42 failed' },
        { number: 60, text: 'Order 42 cancelled' },
      ],
      new Set([40, 60]),
      'Payment for order 42 failed',
    );

    expect(rows.map((row) => row.cited)).toEqual([true, true]);
    expect(marks(rows[0])).toEqual(['Payment for order 42 failed']);
    expect(marks(rows[1])).toEqual([]);
  });

  it('gives an occurrence in other letter case no mark', () => {
    const [row] = evidenceRows(
      [{ number: 120, text: 'Connection pool exhausted' }],
      new Set([120]),
      'connection pool exhausted',
    );

    expect(marks(row)).toEqual([]);
  });

  it('renders a line with markup as text', async () => {
    const fixture = TestBed.createComponent(EvidenceLines);
    fixture.componentRef.setInput('rows', [
      {
        number: 1,
        text: '<b>bold</b> line',
        cited: true,
        segments: segmentsOf('<b>bold</b> line', 'line'),
      },
    ]);
    await fixture.whenStable();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('b')).toBeNull();
    expect(element.textContent).toContain('<b>bold</b>');
    expect(element.querySelector('mark')?.textContent).toBe('line');
    // White space is preserved in log lines, so the template must add none around the segments.
    expect(element.querySelector('code')?.textContent).toBe('<b>bold</b> line');
  });
});
