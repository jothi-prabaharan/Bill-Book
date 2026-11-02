import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * A register row's document number with its code chip in front — `[POR] POR-000615` (TK-187).
 *
 * Given a `viewRoute` the number is a link to the document's view page (TK-263), where it reads as its print
 * template lays it out. The click stops here: the row it sits in opens the document's own screen, and the
 * number is the one place in the row that goes somewhere else.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-doc-number',
  standalone: true,
  imports: [RouterLink],
  template: `@if (code()) {<span class="doc-chip">{{ code() }}</span>}@if (viewRoute(); as link) {<a class="doc-no doc-no--link" [routerLink]="link" title="View as printed" (click)="$event.stopPropagation()">{{ number() || '—' }}</a>} @else {<span class="doc-no">{{ number() || '—' }}</span>}`,
  styles: [
    `
      :host { display: inline-flex; align-items: center; gap: 6px; white-space: nowrap; }
      .doc-chip {
        display: inline-flex; align-items: center; padding: 0 5px; border: 1px solid var(--color-accent-400);
        border-radius: var(--radius-tag); font-size: 10px; letter-spacing: 0.04em; color: var(--color-accent-600); line-height: 16px;
      }
      .doc-no { font-variant-numeric: tabular-nums; }
      .doc-no--link { color: var(--color-accent-700); text-decoration: none; }
      .doc-no--link:hover, .doc-no--link:focus-visible { text-decoration: underline; }
    `,
  ],
})
export class DocNumberComponent {
  readonly code = input<string>('');
  readonly number = input<string | null | undefined>('');

  /** Where the number links to, as router segments; null leaves it as plain text. */
  readonly viewRoute = input<readonly string[] | null>(null);
}
