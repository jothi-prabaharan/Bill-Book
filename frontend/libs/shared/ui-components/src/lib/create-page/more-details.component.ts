import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';

let nextId = 0;

/**
 * The card after a create page's own cards that holds what the app records and
 * `Shell.dc.html` does not show — TDS, project, HSN, terms and conditions,
 * custom fields (TK-192; owner, 12 October 2026: keep them, after the design's
 * cards). Closed by default, with a line saying what is inside.
 *
 * A field the document cannot be saved without belongs in the design's own
 * cards rather than here, where a closed card would hide it. The content stays
 * in the page while closed, so a form's controls inside it keep their values
 * and their validation; a host opens the card when one of them is refused.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-more-details',
  standalone: true,
  templateUrl: './more-details.component.html',
  styleUrl: './more-details.component.scss',
})
export class MoreDetailsComponent {
  readonly title = input<string>('More details');

  /** What is inside, shown while the card is closed: "Terms, project, custom fields". */
  readonly summary = input<string>('');

  readonly open = model<boolean>(false);

  protected readonly bodyId = `more-details-${++nextId}`;

  protected toggle(): void {
    this.open.update((open) => !open);
  }
}
