import { Directive, TemplateRef, inject } from '@angular/core';

/**
 * Footer rows for `bb-data-grid` — a total, a subtotal, a "these legs must
 * balance" line.
 *
 * Put it on an `<ng-template>` holding one or more `<tr>`; the grid renders it
 * inside the table's `<tfoot>`, so the totals line up under their columns.
 *
 * ```html
 * <bb-data-grid [columns]="columns" [data]="legs">
 *   <ng-template bbGridFooter>
 *     <tr><th scope="row">Total</th><td class="numeric">{{ debit }}</td></tr>
 *   </ng-template>
 * </bb-data-grid>
 * ```
 */
@Directive({
  selector: '[bbGridFooter]',
  standalone: true,
})
export class DataGridFooterDirective {
  readonly template = inject(TemplateRef);
}
