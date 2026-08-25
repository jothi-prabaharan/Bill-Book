import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DocumentLine,
  DocumentLineContext,
  DocumentLineTax,
  LineType,
  TaxGroupOption,
  TaxTreatment,
} from './document-line.model';
import { componentsFor, recalculate, totalsOf } from './line-math';

/** Rates carry four decimals in a line's tax rows: 18% is stored as 180000. */
const RATE_SCALE = 10_000;

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
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-document-line-grid',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './document-line-grid.component.html',
  styleUrl: './document-line-grid.component.scss',
})
export class DocumentLineGridComponent {
  readonly lines = input.required<readonly DocumentLine[]>();
  readonly context = input.required<DocumentLineContext>();

  /**
   * The tax groups a line may be put on.
   *
   * **Optional, and empty by default, so a caller that does not pass any keeps
   * the grid exactly as it was.** Until this existed `taxGroupId` was editable
   * nowhere, which meant every line carried no tax rows and therefore no tax —
   * on the sales screens as much as the purchase ones.
   */
  readonly taxGroups = input<readonly TaxGroupOption[]>([]);

  /**
   * Whether to show the Stock / Expense / Capital selector.
   *
   * Off by default because a sale has one kind of line in practice. **Purchase
   * is where all three are used** — an expense line posts to a named account and
   * a capital line creates a fixed asset register row — so the purchase screens
   * turn it on.
   */
  readonly showLineType = input(false);

  /** Emitted whenever a line changes, already recalculated. */
  readonly linesChange = output<readonly DocumentLine[]>();

  /** The host opens its own item picker — this grid does not know how to. */
  readonly pickItem = output<number>();

  protected readonly totals = computed(() => totalsOf(this.lines()));

  protected readonly components = computed(() =>
    componentsFor(this.context().isInterState, this.context().isUnionTerritory ?? false),
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

  /**
   * Putting a line on a tax group rebuilds its tax rows, then recalculates.
   *
   * **The rows are the fact, not their value.** A zero-rated line carries rows
   * at rate zero and an exempt line carries none, which is how GSTR-1 tells an
   * export from an exempt supply — so clearing the group clears the rows rather
   * than leaving them at zero.
   *
   * Which components appear follows the document's `isInterState`: CGST + SGST
   * intra-state, IGST across a state line. The server recomputes all of this on
   * save; what happens here is the preview the person keying is entitled to see.
   */
  protected onTaxGroupChange(index: number): void {
    const updated = this.lines().map((line, at) => {
      if (at !== index) {
        return line;
      }

      const group = this.taxGroups().find(
        (candidate) => candidate.taxGroupId === Number(line.taxGroupId),
      );

      if (group === undefined) {
        return recalculate({ ...line, taxGroupId: null, taxMasterId: null, taxes: [] },
          this.context());
      }

      const taxes: DocumentLineTax[] = this.components().map((component) => ({
        component,
        // Resolved server-side when the document is saved; the grid has no way
        // to know which GST sub-account a rate posts against.
        subAccountId: 0,
        rate: Math.round(this.rateOf(group, component) * RATE_SCALE),
        taxableAmount: 0,
        amount: 0,
      }));

      if (group.cessRate > 0) {
        taxes.push({
          component: 'Cess',
          subAccountId: 0,
          rate: Math.round(group.cessRate * RATE_SCALE),
          taxableAmount: 0,
          amount: 0,
        });
      }

      return recalculate(
        {
          ...line,
          taxGroupId: group.taxGroupId,
          taxMasterId: group.taxMasterId,
          taxes,
        },
        this.context(),
      );
    });

    this.emit(updated);
  }

  private rateOf(
    group: TaxGroupOption,
    component: 'Cgst' | 'Sgst' | 'Igst' | 'Utgst',
  ): number {
    switch (component) {
      case 'Cgst':
        return group.cgstRate;

      // UTGST draws SGST's rate: the same half of the same tax under a
      // different name, and never beside it.
      case 'Sgst':
      case 'Utgst':
        return group.sgstRate;

      default:
        return group.igstRate;
    }
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

  /**
   * Whether a rate can be put on this line at all.
   *
   * Exempt, nil-rated and outside-GST lines carry no tax rows, so offering a
   * rate for them would let somebody pick one that is then thrown away.
   * Zero-rated does carry rows, at rate zero, which is why it is not excluded.
   */
  protected chargesTaxRows(line: DocumentLine): boolean {
    return line.taxTreatment === 'Taxable' || line.taxTreatment === 'ZeroRated';
  }

  private emit(lines: readonly DocumentLine[]): void {
    this.linesChange.emit(lines);
  }
}

