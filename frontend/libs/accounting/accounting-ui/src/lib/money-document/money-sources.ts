/**
 * What a money line can mean, and where it lands in the ledger.
 *
 * <b>This mirrors `Banking.Api.Services.MoneyPostingMap`, and it has to.</b> The
 * server decides the account; this table exists so the screen can show what that
 * decision will be before the document is posted, rather than after. Nothing here
 * is sent — only `ledgerSourceId` travels — so a drift between the two is a
 * wrong label, not a wrong posting. Still worth keeping in step: a label that
 * promises the wrong account is how someone books a supplier deposit as a bill
 * payment.
 */
export interface MoneySource {
  /** `mst.LedgerSources`. The only field the server is told. */
  ledgerSourceId: number;

  label: string;

  /** Which control account the line posts to, as Accounting names it. */
  accountSystemName: 'Accounts Receivable' | 'Accounts Payable';

  /** `Accounting.Entity.Enums.SubAccountPurpose`, by name. */
  purpose: 'Primary' | 'PrepaymentAdvance' | 'OverpaymentAdvance';

  /**
   * The document kind a line under this source settles, or null when it settles
   * nothing — an advance placed before any document exists, for instance.
   */
  settles: string | null;

  hint: string;
}

/**
 * Money leaving. Six meanings: two are payments against a document, two are
 * advances we are placing, two are money we are giving back.
 */
export const SPEND_SOURCES: readonly MoneySource[] = [
  {
    ledgerSourceId: 2,
    label: 'Bill payment',
    accountSystemName: 'Accounts Payable',
    purpose: 'Primary',
    settles: 'BIL',
    hint: "Settles what we owe on a supplier's bill.",
  },
  {
    ledgerSourceId: 8,
    label: 'Advance to supplier',
    accountSystemName: 'Accounts Receivable',
    purpose: 'PrepaymentAdvance',
    settles: null,
    hint: 'A deposit put down before the bill exists. An asset — they owe us goods.',
  },
  {
    ledgerSourceId: 16,
    label: 'Overpayment to supplier',
    accountSystemName: 'Accounts Receivable',
    purpose: 'OverpaymentAdvance',
    settles: 'BIL',
    hint: 'The excess when the payment ran past the bill. Held apart from a deliberate advance.',
  },
  {
    ledgerSourceId: 6,
    label: 'Credit note refund to customer',
    accountSystemName: 'Accounts Receivable',
    purpose: 'Primary',
    settles: 'CRN',
    hint: 'Paying a customer back for goods they returned.',
  },
  {
    ledgerSourceId: 18,
    label: "Refund of customer's overpayment",
    accountSystemName: 'Accounts Payable',
    purpose: 'OverpaymentAdvance',
    settles: null,
    hint: 'Giving back money a customer paid over what they owed.',
  },
  {
    ledgerSourceId: 19,
    label: "Refund of customer's advance",
    accountSystemName: 'Accounts Payable',
    purpose: 'PrepaymentAdvance',
    settles: null,
    hint: 'Giving back an advance a customer placed with us.',
  },
];

/** Money arriving. The mirror of the above, one short — there is no invoice-refund line. */
export const RECEIVE_SOURCES: readonly MoneySource[] = [
  {
    ledgerSourceId: 3,
    label: 'Invoice payment',
    accountSystemName: 'Accounts Receivable',
    purpose: 'Primary',
    settles: 'INV',
    hint: 'Settles what a customer owes on an invoice.',
  },
  {
    ledgerSourceId: 9,
    label: 'Advance from customer',
    accountSystemName: 'Accounts Payable',
    purpose: 'PrepaymentAdvance',
    settles: null,
    hint: 'Taken before the invoice exists. A liability — we owe them goods.',
  },
  {
    ledgerSourceId: 17,
    label: 'Overpayment from customer',
    accountSystemName: 'Accounts Payable',
    purpose: 'OverpaymentAdvance',
    settles: 'INV',
    hint: 'The excess when the receipt ran past the invoice.',
  },
  {
    ledgerSourceId: 7,
    label: 'Debit note refund from supplier',
    accountSystemName: 'Accounts Payable',
    purpose: 'Primary',
    settles: 'DBN',
    hint: 'A supplier paying us back for goods we returned.',
  },
  {
    ledgerSourceId: 4,
    label: 'Refund of our overpayment',
    accountSystemName: 'Accounts Receivable',
    purpose: 'OverpaymentAdvance',
    settles: 'BIL',
    hint: 'A supplier returning what we overpaid them.',
  },
];

/** `Banking.Entity.Enums.PaymentMethod`, by value — the request sends the number. */
export const PAYMENT_METHODS: readonly { value: number; label: string }[] = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Cheque' },
  { value: 2, label: 'Bank transfer' },
  { value: 3, label: 'UPI' },
  { value: 4, label: 'Card' },
  { value: 5, label: 'Demand draft' },
  { value: 6, label: 'Wallet' },
  { value: 7, label: 'Other' },
];
