import { Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DocumentLine,
  DocumentLineContext,
  LineType,
  TaxTreatment,
} from './document-line.model';
import { componentsFor, recalculate, totalsOf } from './line-math';

/**
 * The line grid every sales and purchase document uses.
 *
 * All nine document types share one line shape (`DocumentLineBase`), so they
 * share one grid. What differs between an invoice and a goods receipt is the
 * header around it and the columns each *adds* — delivered quantity, accepted
 * quantity — which the host page renders beside this, not instead of it.
 *
 * **It owns no data and fetches nothing.** Lines come in, changed lines go out,
 * and lookups are the host's job. That keeps the grid testable without a server
 * and keeps one component from needing to know whether it is on a sales page or
 * a purchase one.
 *
 * At ~360px the grid becomes a card per line, per the house rule.
 */
@Component({
  selector: 'bb-document-line-grid',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './document-line-grid.component.html',
  styleUrl: './document-line-grid.component.scss',
})
export class DocumentLineGridComponent {
  readonly lines = input.required<readonly DocumentLine[]>();
  readonly context = input.required<DocumentLineContext>();

  /** Emitted whenever a line changes, already recalculated. */
  readonly linesChange = output<readonly DocumentLine[]>();

  /** The host opens its own item picker — this grid does not know how to. */
  readonly pickItem = output<number>();

  protected readonly totals = computed(() => totalsOf(this.lines()));

  protected readonly components = computed(() =>
    componentsFor(this.context().isInterState),
  );

  protected readonly treatments: readonly TaxTreatment[] = [
    'Taxable',
    'ZeroRated',
    'NilRated',
    'Exempt',
    'NonGst',
  ];

  protected readonly lineTypes: readonly LineType[] = [
    'Stock',
    'Expense',
    'Capital',
  ];

  /** Paise to a display string. Values stay integer everywhere else. */
  protected money(paise: number): string {
    const decimals = this.context().currencyDecimals;
    const unit = 10 ** decimals;
    return (paise / unit).toFixed(decimals);
  }

  protected quantity(scaled: number): string {
    return (scaled / 1_000_000).toString();
  }

  protected addLine(): void {
    const next: DocumentLine = {
      detailId: 0,
      lineNumber: this.lines().length + 1,
      itemId: null,
      itemLabel: null,
      hsnSacCode: null,
      description: null,
      warehouseId: null,
      quantity: 1_000_000,
      uomId: null,
      uomLabel: null,
      conversionFactor: 1_000_000,
      baseQuantity: 1_000_000,
      unitPrice: 0,
      isPriceInclusive: false,
      discountPercent: null,
      discountAmount: 0,
      grossAmount: 0,
      taxableAmount: 0,
      taxTreatment: 'Taxable',
      taxMasterId: null,
      taxGroupId: null,
      taxAmount: 0,
      taxes: [],
      lineType: 'Stock',
      accountId: null,
      fixedAssetCategoryId: null,
      lineTotal: 0,
      itemBatchId: null,
      lineNotes: null,
    };

    this.emit([...this.lines(), next]);
  }

  /**
   * Removing a line renumbers the rest. `LineNumber` is unique within a
   * document and is what the ledger posting keys its `ITEM` leg on, so a gap
   * would leave a posting pointing at a line that no longer exists.
   */
  protected removeLine(index: number): void {
    const remaining = this.lines()
      .filter((_, at) => at !== index)
      .map((line, at) => ({ ...line, lineNumber: at + 1 }));

    this.emit(remaining);
  }

  /** Any edit recalculates that line and republishes the whole set. */
  protected touch(index: number): void {
    const updated = this.lines().map((line, at) =>
      at === index ? recalculate(line, this.context()) : line,
    );

    this.emit(updated);
  }

  /**
   * A line with no item cannot be stock — there is nothing to move — so
   * clearing the item also moves it off `Stock`, matching the check constraint
   * rather than letting the server refuse the save.
   */
  protected onItemCleared(index: number): void {
    const updated = this.lines().map((line, at) =>
      at === index
        ? recalculate(
            {
              ...line,
              itemId: null,
              itemLabel: null,
              lineType: line.lineType === 'Stock' ? 'Expense' : line.lineType,
            },
            this.context(),
          )
        : line,
    );

    this.emit(updated);
  }

  protected isFreeText(line: DocumentLine): boolean {
    return line.itemId === null;
  }

  protected needsAccount(line: DocumentLine): boolean {
    return line.itemId === null || line.lineType === 'Expense';
  }

  private emit(lines: readonly DocumentLine[]): void {
    this.linesChange.emit(lines);
  }
}
