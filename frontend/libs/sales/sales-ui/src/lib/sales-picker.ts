import { computed, signal } from '@angular/core';
import { CustomerOption, SalesItemOption, SalesLookupService } from '@bill-book/sales-core';
import { matchScannedItem } from '@bill-book/scan-core';
import {
  DocumentLine,
  DocumentLineContext,
  LookupRow,
  recalculate,
} from '@bill-book/ui-components';

export type SalesPickerKind = 'none' | 'customer' | 'item';

/** What a form does with a chosen row. */
export interface SalesPickerHandlers {
  customer(row: LookupRow): void;
  item(lineIndex: number, row: LookupRow): void;
  /**
   * A scanned item, or the one chosen after a scan matched nothing (TK-161):
   * the form adds it as a line, or one more on its existing line.
   */
  scanned?(row: LookupRow): void;
}

/**
 * The customer and item pickers every sales form shares (TK-15).
 *
 * One `bb-lookup-dialog` serves both, as on the purchase bill: the form binds
 * its inputs to these signals and forwards its events here. The HTTP is
 * `SalesLookupService`'s, and what a choice does is the form's own, through
 * {@link SalesPickerHandlers}, because each form stores a customer and a line
 * its own way.
 *
 * **A slow answer to an old search never overwrites a newer one.** Each search
 * takes a token and only the latest may write the rows, so typing "ra" then
 * "ravi" cannot end on the matches for "ra".
 */
export class SalesPicker {
  readonly kind = signal<SalesPickerKind>('none');
  readonly rows = signal<LookupRow[]>([]);
  readonly loading = signal(false);

  readonly open = computed(() => this.kind() !== 'none');

  /** The search the dialog opens on: a scanned code that matched nothing, else empty. */
  readonly term = signal('');

  readonly title = computed(() =>
    this.kind() === 'customer' ? 'Choose a customer' : 'Choose an item',
  );

  readonly placeholder = computed(() =>
    this.kind() === 'customer'
      ? 'Search customers by code, name or GSTIN'
      : 'Search items by code or name, or scan a barcode',
  );

  readonly emptyText = computed(() =>
    this.kind() === 'customer' ? 'No customers match.' : 'No items match.',
  );

  private line = -1;
  private fromScan = false;
  private token = 0;

  constructor(
    private readonly lookups: Pick<SalesLookupService, 'customers' | 'items'>,
    private readonly handlers: SalesPickerHandlers,
  ) {}

  openCustomer(): Promise<void> {
    this.fromScan = false;
    this.term.set('');
    this.kind.set('customer');
    return this.search('');
  }

  openItem(lineIndex: number): Promise<void> {
    this.line = lineIndex;
    this.fromScan = false;
    this.term.set('');
    this.kind.set('item');
    return this.search('');
  }

  /**
   * A scanned code (TK-161): the item it names goes straight to the form; a
   * code that names no single item opens the item picker on it, and the item
   * chosen there is added the same way a scan would have added it.
   */
  async scanItem(code: string): Promise<void> {
    const wanted = code.trim();
    if (!wanted || !this.handlers.scanned) {
      return;
    }

    let match: SalesItemOption | null = null;
    try {
      match = matchScannedItem(wanted, await this.lookups.items(wanted));
    } catch {
      match = null;
    }

    if (match) {
      this.handlers.scanned(SalesPicker.itemRow(match));
      return;
    }

    this.fromScan = true;
    this.line = -1;
    this.term.set(wanted);
    this.kind.set('item');
    await this.search(wanted);
  }

  async search(term: string): Promise<void> {
    const token = ++this.token;
    const kind = this.kind();
    this.loading.set(true);

    try {
      const rows =
        kind === 'customer'
          ? (await this.lookups.customers(term)).map(SalesPicker.customerRow)
          : (await this.lookups.items(term)).map(SalesPicker.itemRow);

      if (token === this.token) {
        this.rows.set(rows);
      }
    } catch {
      if (token === this.token) {
        this.rows.set([]);
      }
    } finally {
      if (token === this.token) {
        this.loading.set(false);
      }
    }
  }

  choose(row: LookupRow): void {
    if (this.kind() === 'customer') {
      this.handlers.customer(row);
    } else if (this.kind() === 'item' && this.fromScan) {
      this.handlers.scanned?.(row);
    } else if (this.kind() === 'item' && this.line >= 0) {
      this.handlers.item(this.line, row);
    }

    this.close();
  }

  close(): void {
    // Any answer still in flight belongs to a dialog that is gone.
    this.token++;
    this.kind.set('none');
    this.rows.set([]);
    this.loading.set(false);
    this.line = -1;
    this.fromScan = false;
    this.term.set('');
  }

  /** How a chosen row reads on the form: code, then name. */
  static label(row: LookupRow): string {
    return `${row.code} ${row.name}`.trim();
  }

  /**
   * How a saved document's customer reads on the form when it loads — the code
   * and name the document came back with, or the bare id when it named neither,
   * so a loaded document never shows "Choose a customer" over a customer it has.
   */
  static savedLabel(
    code: string | null | undefined,
    name: string | null | undefined,
    contactId: number | null | undefined,
  ): string {
    const label = `${code ?? ''} ${name ?? ''}`.trim();

    if (label) {
      return label;
    }

    return contactId && contactId > 0 ? `Customer ${contactId}` : '';
  }

  static customerRow(customer: CustomerOption): LookupRow {
    return {
      id: customer.contactId,
      code: customer.contactCode,
      name: customer.displayName,
      meta: customer.gstin,
    };
  }

  static itemRow(item: SalesItemOption): LookupRow {
    return {
      id: item.itemId,
      code: item.itemCode,
      name: item.itemName,
      meta: item.inventoryUomCode,
    };
  }

  /**
   * The lines with one line's item replaced and the line recalculated — the
   * same shape the purchase bill writes, so the grid shows the label at once.
   */
  static withItem<T extends DocumentLine>(
    lines: readonly T[],
    lineIndex: number,
    row: LookupRow,
    context: DocumentLineContext,
  ): T[] {
    return lines.map((line, at) =>
      at === lineIndex
        ? ({
            ...line,
            ...recalculate(
              { ...line, itemId: row.id, itemLabel: `${row.code} - ${row.name}` },
              context,
            ),
          } as T)
        : line,
    );
  }
}
