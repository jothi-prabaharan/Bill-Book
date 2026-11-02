/**
 * The transactions that have one view page between them, and what each is called, where it lives and which
 * template lays it out (TK-263).
 *
 * **One table, so the page, its routes and every link to it agree.** A document list, a form and the page
 * itself all need the same few facts about a transaction — its route, its API, the permission module it
 * answers to — and a copy of them in each is a copy that falls out of step the first time a route is renamed.
 * The server decides what a document looks like (the print template its type names); this table only says how
 * to ask for it.
 */
export type TransactionKind =
  | 'quotes'
  | 'sales-orders'
  | 'delivery-challans'
  | 'invoices'
  | 'credit-notes'
  | 'purchase-orders'
  | 'goods-receipts'
  | 'bills'
  | 'debit-notes'
  | 'receive-money'
  | 'spend-money'
  | 'journals';

/** The permission module a transaction answers to: `{module}.view`, `.print`, `.edit`. */
export type TransactionModule = 'sales' | 'purchase' | 'banking' | 'accounting';

export interface TransactionKindInfo {
  readonly kind: TransactionKind;

  /** The three-letter transaction type, which is what picks the print template. */
  readonly typeCode: string;

  /** What the document is called in a sentence: "quote", "goods receipt". */
  readonly label: string;

  readonly module: TransactionModule;

  /** The document's own screen without its id, as router segments: `['/sales', 'quotes']`. */
  readonly base: readonly string[];

  /** The API prefix the document is served under, without a trailing slash. */
  readonly api: string;
}

export const TRANSACTION_KINDS: readonly TransactionKindInfo[] = [
  { kind: 'quotes', typeCode: 'QTE', label: 'quote', module: 'sales', base: ['/sales', 'quotes'], api: '/api/sales/quotes' },
  { kind: 'sales-orders', typeCode: 'SOR', label: 'sales order', module: 'sales', base: ['/sales', 'sales-orders'], api: '/api/sales/sales-orders' },
  { kind: 'delivery-challans', typeCode: 'DLC', label: 'delivery challan', module: 'sales', base: ['/sales', 'delivery-challans'], api: '/api/sales/delivery-challans' },
  { kind: 'invoices', typeCode: 'INV', label: 'invoice', module: 'sales', base: ['/sales', 'invoices'], api: '/api/sales/invoices' },
  { kind: 'credit-notes', typeCode: 'CRN', label: 'credit note', module: 'sales', base: ['/sales', 'credit-notes'], api: '/api/sales/credit-notes' },

  { kind: 'purchase-orders', typeCode: 'POR', label: 'purchase order', module: 'purchase', base: ['/purchase', 'purchase-orders'], api: '/api/purchase/purchase-orders' },
  { kind: 'goods-receipts', typeCode: 'GRN', label: 'goods receipt', module: 'purchase', base: ['/purchase', 'goods-receipts'], api: '/api/purchase/goods-receipts' },
  { kind: 'bills', typeCode: 'BIL', label: 'bill', module: 'purchase', base: ['/purchase', 'bills'], api: '/api/purchase/bills' },
  { kind: 'debit-notes', typeCode: 'DBN', label: 'debit note', module: 'purchase', base: ['/purchase', 'debit-notes'], api: '/api/purchase/debit-notes' },

  { kind: 'receive-money', typeCode: 'RCM', label: 'receipt', module: 'banking', base: ['/banking', 'receive-money'], api: '/api/receive-money' },
  { kind: 'spend-money', typeCode: 'SPM', label: 'payment', module: 'banking', base: ['/banking', 'spend-money'], api: '/api/spend-money' },

  { kind: 'journals', typeCode: 'JRN', label: 'journal voucher', module: 'accounting', base: ['/accounting', 'journals'], api: '/api/journals' },
];

const BY_KIND: ReadonlyMap<string, TransactionKindInfo> = new Map(TRANSACTION_KINDS.map((info) => [info.kind, info]));

/**
 * The names a sales or purchase register gives a row's type, which are not the URL names: `SalesOrder` is
 * `sales-orders`. A list that holds more than one type reads the row's own.
 */
const BY_TYPE_NAME: Readonly<Record<string, TransactionKind>> = {
  Quote: 'quotes',
  SalesOrder: 'sales-orders',
  DeliveryChallan: 'delivery-challans',
  Invoice: 'invoices',
  CreditNote: 'credit-notes',
  PurchaseOrder: 'purchase-orders',
  GoodsReceipt: 'goods-receipts',
  Bill: 'bills',
  DebitNote: 'debit-notes',
};

/** What a transaction kind is, or null for a name this table does not know. */
export function transactionKindInfo(kind: string | null | undefined): TransactionKindInfo | null {
  return (kind && BY_KIND.get(kind)) || null;
}

/** The kind a register row's type name stands for (`SalesOrder` → `sales-orders`), or null for one with no view. */
export function transactionKindOfTypeName(name: string | null | undefined): TransactionKind | null {
  return (name && BY_TYPE_NAME[name]) || null;
}

/** The router link of a transaction's view page: `['/sales', 'quotes', '7', 'view']`. */
export function transactionViewRoute(kind: TransactionKind, id: number): string[] {
  const info = transactionKindInfo(kind);
  return info ? [...info.base, String(id), 'view'] : [];
}

/** The router link of the transaction's own screen, where it is edited, posted or voided. */
export function transactionDocumentRoute(kind: TransactionKind, id: number): string[] {
  const info = transactionKindInfo(kind);
  return info ? [...info.base, String(id)] : [];
}

/** The register the transaction lives in. */
export function transactionListRoute(kind: TransactionKind): string[] {
  const info = transactionKindInfo(kind);
  return info ? [...info.base] : ['/'];
}

/**
 * The view link for a register row by its type name, or null when the row is of no type the view page covers
 * (so a list can leave the link off rather than point it nowhere).
 */
export function transactionViewRouteOfRow(typeName: string | null | undefined, id: number | null | undefined): string[] | null {
  const kind = transactionKindOfTypeName(typeName);
  return kind && id ? transactionViewRoute(kind, id) : null;
}

/** The API path of the document laid out by its print template, at the module's view permission. */
export function transactionViewApiPath(kind: TransactionKind, id: number): string {
  const info = transactionKindInfo(kind);
  if (!info) {
    throw new Error(`Unknown transaction kind: ${kind}`);
  }

  return `${info.api}/${id}/view`;
}
