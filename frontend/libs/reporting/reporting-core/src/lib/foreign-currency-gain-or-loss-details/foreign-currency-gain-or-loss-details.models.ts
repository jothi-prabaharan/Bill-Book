import { ForeignAccountKind, foreignKindLabel, headingDetail, netOf } from '../foreign-currency-gain-or-loss/foreign-currency-gain-or-loss.models';

/**
 * The Foreign Currency Gain or Loss Details report's contract (TK-221), field for field with
 * `ForeignCurrencyGainLossDetailsReportModels.cs`, and the pure steps the page takes from an
 * answer: the gains and losses of the Foreign Currency Gain or Loss report document by
 * document. An open document carries its unrealized amount; a settlement carries the realized
 * amount it posted. Every figure but the amount in the currency is in the branch's base
 * currency. Dates are ISO `yyyy-MM-dd`; a rate is base currency per unit of the foreign one.
 */

/** What a document row is: a document still open at the To date, or a settlement of the period. */
export type ForeignDocumentKind = 'Open' | 'Settlement';

/** What the report is asked for. Both dates are required; the rest narrow it; a rate is given for each currency. */
export interface ForeignCurrencyDetailsRequest {
  /** The first day the realized part covers. */
  fromDate: string;
  /** The last day the realized part covers, and the date the balances are taken at. */
  toDate: string;
  currencyCode?: string | null;
  contactId?: number | null;
  accountId?: number | null;
  /** The revaluation rate for each currency, by its code. A currency with none has no unrealized figure. */
  rates?: Record<string, number>;
}

/** One document, or one settlement, under an account. */
export interface ForeignCurrencyDocument {
  kind: ForeignDocumentKind;
  date: string;
  /** The kind of document: INV, BIL, RCM, SPM and so on. */
  typeCode: string;
  documentNo: string | null;
  contactId: number | null;
  contactName: string | null;
  /** For a settlement, the document it settled, such as "Settles INV-0042". */
  reference: string | null;
  /** Open: what is owed in the currency, negative for a payable. Settlement: what was settled. */
  amount: number | null;
  /** Base currency per unit at the rate the document was booked at. */
  bookedRate: number | null;
  /** Open: the revaluation rate. Settlement: the rate the money moved at. */
  currentRate: number | null;
  /** The amount in base currency at the booked rate. */
  carryingBase: number | null;
  /** Open: at the revaluation rate. Settlement: at the rate the money moved at. */
  currentBase: number | null;
  /** A settlement's exchange difference: positive is a gain. */
  realized: number | null;
  /** An open document's difference from its booked rate: positive is a gain; null without a rate. */
  unrealized: number | null;
}

/** One account's balance in a currency and the documents behind it. */
export interface ForeignCurrencyDetailAccount {
  accountId: number;
  accountName: string;
  kind: ForeignAccountKind;
  dueSource: number;
  carryingBase: number;
  revaluedBase: number | null;
  unrealized: number | null;
  realized: number;
  documents: ForeignCurrencyDocument[];
}

export interface ForeignCurrencyDetailBlock {
  currencyCode: string;
  rate: number | null;
  needsRate: boolean;
  accounts: ForeignCurrencyDetailAccount[];
  dueSource: number;
  carryingBase: number;
  revaluedBase: number | null;
  unrealized: number | null;
  realized: number;
  net: number;
}

export interface ForeignCurrencyDetailsResult {
  fromDate: string;
  toDate: string;
  baseCurrencyCode: string;
  currencies: ForeignCurrencyDetailBlock[];
  /** Postings to Realized FX Gain/Loss that no currency can claim, such as a manual journal's. */
  unassigned: ForeignCurrencyDocument[];
  unassignedRealized: number;
  totalRealized: number;
  totalUnrealized: number;
  totalNet: number;
  currenciesWithoutRate: string[];
}

/** A foreign currency with a posting; `hasBalance` says whether a rate is asked for it. */
export interface ForeignCurrencyDetailChoice {
  currencyCode: string;
  accounts: number;
  hasBalance: boolean;
}

export interface ForeignCurrencyAccountChoice {
  accountId: number;
  accountName: string;
  kind: ForeignAccountKind;
}

export interface ForeignCurrencyContactChoice {
  contactId: number;
  contactName: string;
}

/** The base currency, and the currencies, accounts and contacts the filters offer. */
export interface ForeignCurrencyDetailChoices {
  baseCurrencyCode: string;
  currencies: ForeignCurrencyDetailChoice[];
  accounts: ForeignCurrencyAccountChoice[];
  contacts: ForeignCurrencyContactChoice[];
}

export type ForeignDetailRowKind = 'heading' | 'account' | 'open' | 'settlement' | 'subtotal' | 'currencyTotal' | 'unassigned' | 'total';

/** One row of the grid. */
export interface ForeignDetailGridRow {
  kind: ForeignDetailRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  currencyCode: string;
  /** The document's or settlement's date; null on headings and totals. */
  date: string | null;
  /** The document number, or the heading or total's name. */
  name: string;
  /** What sits under the name: the contact, "Settles INV-0042", the kind of account, or a currency's rate. */
  detail: string;
  /** The type code beside a document's number. */
  typeCode: string;
  /** Null where the figure does not apply, so the cell reads blank rather than as a nought. */
  bookedRate: number | null;
  currentRate: number | null;
  amount: number | null;
  carryingBase: number | null;
  currentBase: number | null;
  realized: number | null;
  unrealized: number | null;
  net: number | null;
  /** The currency has a balance and no rate, so its unrealized cells are blank for a reason. */
  needsRate: boolean;
}

const blankRow = (kind: ForeignDetailRowKind, key: string, currencyCode: string, name: string): ForeignDetailGridRow => ({
  kind,
  key,
  currencyCode,
  date: null,
  name,
  detail: '',
  typeCode: '',
  bookedRate: null,
  currentRate: null,
  amount: null,
  carryingBase: null,
  currentBase: null,
  realized: null,
  unrealized: null,
  net: null,
  needsRate: false,
});

/** What sits under a document's number: who it is with, and for a settlement what it settled. */
export function documentDetail(document: Pick<ForeignCurrencyDocument, 'contactName' | 'reference'>): string {
  return [document.contactName, document.reference].filter((part): part is string => !!part).join(' · ');
}

function documentRow(document: ForeignCurrencyDocument, key: string, currencyCode: string, needsRate: boolean): ForeignDetailGridRow {
  return {
    kind: document.kind === 'Open' ? 'open' : 'settlement',
    key,
    currencyCode,
    date: document.date,
    name: document.documentNo ?? '',
    detail: documentDetail(document),
    typeCode: document.typeCode,
    bookedRate: document.bookedRate,
    currentRate: document.currentRate,
    amount: document.amount,
    carryingBase: document.carryingBase,
    currentBase: document.currentBase,
    realized: document.realized,
    unrealized: document.unrealized,
    // An open document with no rate has neither figure: a blank cell, not a nought that reads as a result.
    net: document.realized === null && document.unrealized === null ? null : netOf(document.realized ?? 0, document.unrealized),
    needsRate,
  };
}

/**
 * The grid's rows, currency by currency: a heading, then each account with its documents and
 * its total when it has more than one document, then the currency's total when it has more than
 * one account; then what no currency claims, and a grand total when there is more than one
 * currency or something unassigned. Nothing is worked out here beyond the layout and each
 * document's net; the figures are the server's.
 */
export function foreignDetailRows(result: ForeignCurrencyDetailsResult): ForeignDetailGridRow[] {
  const rows: ForeignDetailGridRow[] = [];

  for (const block of result.currencies) {
    rows.push({
      ...blankRow('heading', `h:${block.currencyCode}`, block.currencyCode, block.currencyCode),
      detail: headingDetail(block, result.baseCurrencyCode),
      needsRate: block.needsRate,
    });

    for (const account of block.accounts) {
      rows.push({
        ...blankRow('account', `a:${block.currencyCode}:${account.accountId}`, block.currencyCode, account.accountName),
        detail: foreignKindLabel(account.kind),
      });

      account.documents.forEach((document, index) => {
        rows.push(documentRow(document, `d:${block.currencyCode}:${account.accountId}:${index}`, block.currencyCode, block.needsRate));
      });

      if (account.documents.length > 1) {
        rows.push({
          ...blankRow('subtotal', `s:${block.currencyCode}:${account.accountId}`, block.currencyCode, `Total ${account.accountName}`),
          amount: account.dueSource,
          carryingBase: account.carryingBase,
          currentBase: account.revaluedBase,
          realized: account.realized,
          unrealized: account.unrealized,
          net: netOf(account.realized, account.unrealized),
          needsRate: block.needsRate,
        });
      }
    }

    if (block.accounts.length > 1) {
      rows.push({
        ...blankRow('currencyTotal', `c:${block.currencyCode}`, block.currencyCode, `Total ${block.currencyCode}`),
        amount: block.dueSource,
        carryingBase: block.carryingBase,
        currentBase: block.revaluedBase,
        realized: block.realized,
        unrealized: block.unrealized,
        net: block.net,
        needsRate: block.needsRate,
      });
    }
  }

  if (result.unassigned.length > 0) {
    rows.push({ ...blankRow('heading', 'h:unassigned', '', 'Not tied to a currency'), detail: 'Posted with no foreign balance, such as a manual journal' });
    result.unassigned.forEach((document, index) => {
      rows.push({ ...documentRow(document, `u:${index}`, '', false), kind: 'unassigned' });
    });
  }

  if (result.currencies.length > 1 || (result.currencies.length > 0 && result.unassigned.length > 0)) {
    rows.push({
      ...blankRow('total', 't', result.baseCurrencyCode, 'Total'),
      carryingBase: result.currencies.reduce((t, c) => t + c.carryingBase, 0),
      realized: result.totalRealized,
      unrealized: result.totalUnrealized,
      net: result.totalNet,
    });
  }

  return rows;
}

/** The rates the phone card shows for a document: the booked rate, then the one it is now at. */
export function rateRange(row: Pick<ForeignDetailGridRow, 'bookedRate' | 'currentRate'>, format: (rate: number) => string): string {
  if (row.bookedRate === null) {
    return row.currentRate === null ? '' : format(row.currentRate);
  }

  return row.currentRate === null ? format(row.bookedRate) : `${format(row.bookedRate)} → ${format(row.currentRate)}`;
}

/** A currency on the filter says how many accounts have a posting in it. */
export function detailCurrencyLabel(choice: Pick<ForeignCurrencyDetailChoice, 'currencyCode' | 'accounts'>): string {
  return `${choice.currencyCode} · ${choice.accounts} ${choice.accounts === 1 ? 'account' : 'accounts'}`;
}

/** An account on the filter: its name and what kind it is. */
export function detailAccountLabel(choice: Pick<ForeignCurrencyAccountChoice, 'accountName' | 'kind'>): string {
  return `${choice.accountName} · ${foreignKindLabel(choice.kind)}`;
}
