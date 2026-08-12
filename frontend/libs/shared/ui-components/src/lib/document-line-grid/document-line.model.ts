/**
 * One editable line on a sales or purchase document.
 *
 * Mirrors `DocumentLineBase` — see SALES.md §4. All nine document types share
 * this shape, which is why one grid can edit any of them.
 *
 * **Amounts are held in integer paise, not rupees.** TypeScript has no decimal;
 * `number` is a double and `0.1 + 0.2` is not `0.3`. Money that has to tie to a
 * ledger cannot be computed in floating point, so every amount here is a whole
 * number of the currency's smallest unit and is converted for display only.
 */
export interface DocumentLine {
  /** Zero for a line not yet saved. */
  detailId: number;
  lineNumber: number;

  /** Null makes this a free-text line: description, quantity and price only. */
  itemId: number | null;
  /** Resolved from Inventory for display; never stored on the document. */
  itemLabel: string | null;
  hsnSacCode: string | null;
  /** Required when `itemId` is null — it is then the only thing naming what was sold. */
  description: string | null;
  warehouseId: number | null;

  quantity: number;
  uomId: number | null;
  uomLabel: string | null;
  conversionFactor: number;
  baseQuantity: number;

  /** Paise per entered unit. */
  unitPrice: number;
  isPriceInclusive: boolean;
  discountPercent: number | null;
  /** Paise. */
  discountAmount: number;

  /** Paise. Computed — never keyed. */
  grossAmount: number;
  /** Paise. Computed — never keyed. */
  taxableAmount: number;

  taxTreatment: TaxTreatment;
  taxMasterId: number | null;
  taxGroupId: number | null;
  /** Paise. Sum of `taxes` — computed. */
  taxAmount: number;
  taxes: DocumentLineTax[];

  lineType: LineType;
  accountId: number | null;
  fixedAssetCategoryId: number | null;

  /** Paise. `taxableAmount + taxAmount` — computed. */
  lineTotal: number;
  itemBatchId: number | null;
  lineNotes: string | null;
}

/**
 * One tax component on a line. Intra-state produces two of these, inter-state
 * one, and cess is a third on top — which is why tax is rows rather than a
 * fixed set of columns.
 */
export interface DocumentLineTax {
  component: TaxComponent;
  subAccountId: number;
  /** Percent, four decimals. Not paise — this is a rate, not money. */
  rate: number;
  /** Paise. */
  taxableAmount: number;
  /** Paise. */
  amount: number;
}

export type TaxComponent = 'Cgst' | 'Sgst' | 'Igst' | 'Cess';

export type TaxTreatment =
  | 'Taxable'
  | 'ZeroRated'
  | 'NilRated'
  | 'Exempt'
  | 'NonGst';

export type LineType = 'Stock' | 'Expense' | 'Capital';

/**
 * What the grid needs from the document around it. Passed in rather than
 * fetched, so the grid stays free of HTTP and can be tested without a server.
 */
export interface DocumentLineContext {
  /** Decides whether a line's tax rows carry CGST + SGST, or IGST. */
  isInterState: boolean;
  /** From `plt.Organizations`. Off means every line must name an item. */
  allowFreeTextLines: boolean;
  /** From `plt.Organizations`. Off computes tax on the gross. */
  discountBeforeTax: boolean;
  /** From `plt.Organizations`: Line, Header or Both. */
  discountLevel: 'Line' | 'Header' | 'Both';
  /** Posted documents are never edited. */
  readonly: boolean;
  /** Decimal places for display only; the values themselves stay in paise. */
  currencyDecimals: number;
}
