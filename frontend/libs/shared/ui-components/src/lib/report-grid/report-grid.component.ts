import { ChangeDetectionStrategy } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Component, computed, input, output } from '@angular/core';
import {
  ReportColumn,
  ReportCurrency,
  ReportGroupFooter,
  ReportQuery,
  ReportResult,
} from '@bill-book/reporting-core';

/**
 * The grid every report renders in.
 *
 * **It owns no data, fetches nothing, and computes no total.** A query state goes
 * in, a page of rows goes in, a new query state comes out — the host re-runs the
 * report. That is the same contract `bb-document-line-grid` works to, and it is
 * what keeps forty-five reports on one component.
 *
 * The totals in particular are the server's. A subtotal computed here would be
 * over the fifty rows on screen, which is wrong for every group spanning a page
 * boundary — and wrong in a way that looks plausible.
 *
 * At ~360px the table becomes a card per row, per the house rule.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-report-grid',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './report-grid.component.html',
  styleUrl: './report-grid.component.scss',
})
export class ReportGridComponent {
  readonly result = input.required<ReportResult | null>();
  readonly state = input.required<ReportQuery>();
  readonly busy = input(false);

  /** Any interaction. The host re-queries and passes a new result back in. */
  readonly stateChange = output<ReportQuery>();

  /** Double-click or Enter on a row. The host decides whether that opens a document. */
  readonly rowActivate = output<Record<string, unknown>>();

  protected readonly columns = computed(() => this.result()?.columns ?? []);

  protected readonly rows = computed(() => this.result()?.rows ?? []);

  protected readonly currency = computed<ReportCurrency>(
    () => this.result()?.currency ?? { code: '', decimals: 2 },
  );

  /**
   * How far from the left each frozen column sits.
   *
   * **Computed cumulatively rather than guessed**, because `position: sticky`
   * needs a real pixel offset and columns are not all the same width. Getting it
   * wrong stacks the frozen columns on top of each other, which reads as data
   * loss rather than as a layout bug.
   */
  protected readonly frozenOffsets = computed(() => {
    const frozen = this.state().freeze.columns;
    const offsets: number[] = [];
    let running = 0;

    for (let index = 0; index < frozen && index < this.columns().length; index++) {
      offsets.push(running);
      running += this.columns()[index].width ?? this.defaultWidth(this.columns()[index]);
    }

    return offsets;
  });

  protected isFrozen(index: number): boolean {
    return index < this.frozenOffsets().length;
  }

  protected frozenLeft(index: number): string {
    return `${this.frozenOffsets()[index]}px`;
  }

  /** The last frozen column carries the seam shadow, so the join is visible while scrolling. */
  protected isSeam(index: number): boolean {
    return index === this.frozenOffsets().length - 1;
  }

  /**
   * Which sort position a column holds, 1-based, or 0 when it is not sorted by.
   *
   * Shown in the header because a three-key sort is otherwise indistinguishable
   * from a one-key sort with surprising results.
   */
  protected sortPosition(key: string): number {
    return this.state().sorts.findIndex((sort) => sort.column === key) + 1;
  }

  protected sortDirection(key: string): 'Asc' | 'Desc' | null {
    return this.state().sorts.find((sort) => sort.column === key)?.direction ?? null;
  }

  /**
   * Click cycles ascending → descending → off. Shift-click adds a key instead of
   * replacing the set.
   *
   * **A report that forces its own order refuses all of it.** A running balance is
   * only true of one ordering, so re-sorting would leave the figures arithmetically
   * correct for an order the reader cannot see.
   */
  protected toggleSort(column: ReportColumn, event: MouseEvent): void {
    if (!column.isSortable || this.result()?.reportKey === undefined) {
      return;
    }

    const additive = event.shiftKey;
    const existing = this.state().sorts.find((sort) => sort.column === column.key);

    let sorts = additive
      ? this.state().sorts.filter((sort) => sort.column !== column.key)
      : [];

    if (existing?.direction === 'Asc') {
      sorts = [...sorts, { column: column.key, direction: 'Desc' }];
    } else if (existing?.direction !== 'Desc') {
      sorts = [...sorts, { column: column.key, direction: 'Asc' }];
    }
    // A third click on a descending column drops it, leaving `sorts` as-is.

    this.emit({ ...this.state(), sorts, page: { ...this.state().page, number: 1 } });
  }

  // ---- paging ----

  protected readonly pageSizes = [25, 50, 100, 200];

  protected readonly totalPages = computed(() => this.result()?.page.totalPages ?? 1);

  protected readonly firstRow = computed(() => {
    const page = this.state().page;
    return this.rows().length === 0 ? 0 : (page.number - 1) * page.size + 1;
  });

  protected readonly lastRow = computed(
    () => (this.state().page.number - 1) * this.state().page.size + this.rows().length,
  );

  protected goToPage(number: number): void {
    const clamped = Math.max(1, Math.min(number, this.totalPages()));

    this.emit({
      ...this.state(),
      // The count is a second scan over the whole result, so it is asked for once
      // and carried until a filter changes it.
      page: { ...this.state().page, number: clamped, includeCount: false },
    });
  }

  protected changePageSize(size: number): void {
    this.emit({
      ...this.state(),
      page: { number: 1, size, includeCount: true },
    });
  }

  // ---- rendering ----

  /**
   * A value formatted for its column's type.
   *
   * Money and quantity are handed to the template's `number` pipe instead, so the
   * digits stay tabular and the decimals follow the branch's currency.
   */
  protected display(row: Record<string, unknown>, column: ReportColumn): string {
    const value = row[column.key];

    if (value === null || value === undefined) {
      return '';
    }

    switch (column.dataType) {
      case 'Date':
        return String(value).slice(0, 10);
      case 'DateTime':
        return String(value).replace('T', ' ').slice(0, 16);
      case 'Boolean':
        return value ? 'Yes' : 'No';
      default:
        return String(value);
    }
  }

  protected isNumeric(column: ReportColumn): boolean {
    return (
      column.dataType === 'Money' ||
      column.dataType === 'Quantity' ||
      column.dataType === 'Number' ||
      column.dataType === 'Percent' ||
      column.dataType === 'Rate'
    );
  }

  protected numeric(row: Record<string, unknown>, column: ReportColumn): number | null {
    const value = row[column.key];

    return typeof value === 'number' ? value : value === null ? null : Number(value);
  }

  protected aggregate(footer: ReportGroupFooter, key: string): number | null {
    return footer.aggregates[key] ?? null;
  }

  /** The digits format for a column — money follows the branch, quantity carries more. */
  protected format(column: ReportColumn): string {
    const decimals = this.currency().decimals;

    return column.dataType === 'Quantity'
      ? '1.0-6'
      : column.dataType === 'Money'
        ? `1.${decimals}-${decimals}`
        : '1.0-4';
  }



  protected activate(row: Record<string, unknown>): void {
    this.rowActivate.emit(row);
  }

  private defaultWidth(column: ReportColumn): number {
    return this.isNumeric(column) ? 120 : 160;
  }

  private emit(state: ReportQuery): void {
    this.stateChange.emit(state);
  }
}

