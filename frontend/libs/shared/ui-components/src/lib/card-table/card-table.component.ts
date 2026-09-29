import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * A table the page composes itself, in the product's table style.
 *
 * **Reach for `bb-data-grid` first.** A list of records someone browses — sort,
 * page, choose columns — is a grid, and the grid gives all of that for a column
 * list. This is for the tables a grid cannot draw: a posting preview with a
 * totals footer, a statement section with its subtotal, rows carrying a state
 * of their own (short of stock, over-allocated), or rows of editable controls.
 *
 * The page projects `<thead>`, `<tbody>` and `<tfoot>`; the component owns the
 * `<table>` itself and its scrolling frame, which is what keeps every such table
 * looking and scrolling like every other.
 *
 * ```html
 * <bb-card-table label="Posting preview" flush>
 *   <thead><tr><th scope="col">Account</th>…</tr></thead>
 *   <tbody>@for (leg of legs; track $index) { <tr>…</tr> }</tbody>
 *   <tfoot>…</tfoot>
 * </bb-card-table>
 * ```
 *
 * Styles for the projected rows stay on the page: the cells are the page's own
 * template, so its scoped selectors reach them — put the class on the
 * `bb-card-table` element, not on a `<table>` that is no longer there.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-card-table',
  standalone: true,
  templateUrl: './card-table.component.html',
  // A block, so a page's width and margin rules on the element land on it.
  host: { style: 'display: block' },
})
export class CardTableComponent {
  /**
   * Drop the card surface and border.
   *
   * For a table that already sits inside a card, a panel or a drawer, where a
   * second border inside the first reads as a box in a box.
   */
  readonly flush = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /** The table's accessible name, when no heading beside it gives one. */
  readonly label = input<string>('');
}
