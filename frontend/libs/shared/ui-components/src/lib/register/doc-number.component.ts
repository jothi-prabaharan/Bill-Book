import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** A register row's document number with its code chip in front — `[POR] POR-000615` (TK-187). */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-doc-number',
  standalone: true,
  template: `@if (code()) {<span class="doc-chip">{{ code() }}</span>}<span class="doc-no">{{ number() || '—' }}</span>`,
  styles: [
    `
      :host { display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; }
      .doc-chip {
        display: inline-flex; align-items: center; padding: 0 5px; border: 1px solid var(--color-accent-400);
        border-radius: var(--radius-tag); font-size: 10px; letter-spacing: 0.04em; color: var(--color-accent-600); line-height: 16px;
      }
      .doc-no { font-variant-numeric: tabular-nums; }
    `,
  ],
})
export class DocNumberComponent {
  readonly code = input<string>('');
  readonly number = input<string | null | undefined>('');
}
