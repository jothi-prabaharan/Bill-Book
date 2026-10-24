/**
 * The Foreign Currency Gain or Loss report's contract (TK-220), field for field with
 * `ForeignCurrencyGainLossReportModels.cs`, and the pure steps the page takes from an
 * answer: the exchange gains and losses on foreign-currency balances, realized when a
 * settlement posted a difference and unrealized on what is still open, at a rate the
 * person running it gives. Every figure but the foreign amount is in the branch's base
 * currency. Dates are ISO `yyyy-MM-dd`; a rate is base currency per unit of the foreign
 * one, the way the rate master states it.
 */

/** What kind of account holds a foreign-currency balance. */
export type ForeignAccountKind = 'Receivable' | 'Payable' | 'Bank';

/** What the report is asked for. Both dates are required; a currency narrows it; a rate is given for each currency. */
export interface ForeignCurrencyGainLossRequest {
  /** The first day the realized part covers. */
  fromDate: string;
  /** The last day the realized part covers, and the date the balances are taken at. */
  asAtDate: string;
  /** Only this currency; every foreign currency when blank. */
  currencyCode?: string | null;
  /** The revaluation rate for each currency, by its code. A currency with none has no unrealized figure. */
  rates?: Record<string, number>;
}

/** One account's foreign-currency balance, and what it has gained or lost. */
export interface ForeignCurrencyAccount {
  accountId: number;
  accountName: string;
  kind: ForeignAccountKind;
  /** What is owed to the branch in the currency: negative for a payable. */
  dueSource: number;
  /** The same balance in base currency at the rates the documents were booked at. */
  carryingBase: number;
  /** The balance at the revaluation rate; null when the currency has no rate. */
  revaluedBase: number | null;
  /** Revalued less carrying: positive is a gain. Null when the currency has no rate. */
  unrealized: number | null;
  /** What settlements in the period posted to Realized FX Gain/Loss for this account: positive is a gain. */
  realized: number;
}

/** One currency: its accounts, and what the currency came to in total. */
export interface ForeignCurrencyBlock {
  currencyCode: string;
  /** The revaluation rate used; null when none was given. */
  rate: number | null;
  /** The currency has a balance and no rate. */
  needsRate: boolean;
  accounts: ForeignCurrencyAccount[];
  dueSource: number;
  carryingBase: number;
  revaluedBase: number | null;
  unrealized: number | null;
  realized: number;
  net: number;
}

export interface ForeignCurrencyGainLossResult {
  fromDate: string;
  asAtDate: string;
  baseCurrencyCode: string;
  currencies: ForeignCurrencyBlock[];
  /** Posted to Realized FX Gain/Loss by a document with no foreign balance, such as a manual journal. */
  unassignedRealized: number;
  /** Every posting to Realized FX Gain/Loss in the period: the account's movement on the Profit and Loss. */
  totalRealized: number;
  totalUnrealized: number;
  totalNet: number;
  currenciesWithoutRate: string[];
}

/** A foreign currency the branch holds a balance in. */
export interface ForeignCurrencyChoice {
  currencyCode: string;
  accounts: number;
}

/** The branch's base currency and the foreign currencies it holds a balance in at a date. */
export interface ForeignCurrencyChoices {
  baseCurrencyCode: string;
  currencies: ForeignCurrencyChoice[];
}

const KIND_LABELS: Record<ForeignAccountKind, string> = {
  Receivable: 'Receivable',
  Payable: 'Payable',
  Bank: 'Bank',
};

/** How a kind of account reads on the page. */
export function foreignKindLabel(kind: ForeignAccountKind): string {
  return KIND_LABELS[kind] ?? kind;
}

export type ForeignCurrencyRowKind = 'heading' | 'account' | 'subtotal' | 'unassigned' | 'total';

/** One row of the grid: a currency's heading, an account, a currency's total, the part no currency claims, or the grand total. */
export interface ForeignCurrencyGridRow {
  kind: ForeignCurrencyRowKind;
  /** Unique across the grid, for tracking. */
  key: string;
  currencyCode: string;
  name: string;
  /** The kind of account, or the rate on a heading; empty elsewhere. */
  detail: string;
  /** Null on a heading and where the figure does not apply, so the cell reads blank rather than as a nought. */
  dueSource: number | null;
  carryingBase: number | null;
  revaluedBase: number | null;
  unrealized: number | null;
  realized: number | null;
  net: number | null;
  /** The currency has a balance and no rate, so its revalued and unrealized cells are blank for a reason. */
  needsRate: boolean;
}

/** A rate as it reads on the page: the figure with no more places than it was given. */
export function rateText(rate: number): string {
  return String(Number(rate.toFixed(8)));
}

/** What a currency's heading says: its rate, or that it has none. */
export function headingDetail(block: Pick<ForeignCurrencyBlock, 'rate' | 'needsRate'>, baseCurrency: string): string {
  if (block.rate !== null) {
    return `rate ${rateText(block.rate)} ${baseCurrency}`;
  }

  return block.needsRate ? 'no rate given' : '';
}

/** Realized plus unrealized; a figure that is not worked out counts as nothing. */
export function netOf(realized: number, unrealized: number | null): number {
  return realized + (unrealized ?? 0);
}

/**
 * The grid's rows, currency by currency: a heading, its accounts, then its total when it
 * has more than one account; then what no currency claims, and a grand total when there
 * is more than one currency or something unassigned. Nothing is worked out here beyond the
 * layout and each account's net; the figures are the server's.
 */
export function foreignCurrencyRows(result: ForeignCurrencyGainLossResult): ForeignCurrencyGridRow[] {
  const rows: ForeignCurrencyGridRow[] = [];

  for (const block of result.currencies) {
    rows.push({
      kind: 'heading',
      key: `h:${block.currencyCode}`,
      currencyCode: block.currencyCode,
      name: block.currencyCode,
      detail: headingDetail(block, result.baseCurrencyCode),
      dueSource: null,
      carryingBase: null,
      revaluedBase: null,
      unrealized: null,
      realized: null,
      net: null,
      needsRate: block.needsRate,
    });

    for (const account of block.accounts) {
      rows.push({
        kind: 'account',
        key: `a:${block.currencyCode}:${account.accountId}`,
        currencyCode: block.currencyCode,
        name: account.accountName,
        detail: foreignKindLabel(account.kind),
        dueSource: account.dueSource,
        carryingBase: account.carryingBase,
        revaluedBase: account.revaluedBase,
        unrealized: account.unrealized,
        realized: account.realized,
        net: netOf(account.realized, account.unrealized),
        needsRate: block.needsRate,
      });
    }

    if (block.accounts.length > 1) {
      rows.push({
        kind: 'subtotal',
        key: `s:${block.currencyCode}`,
        currencyCode: block.currencyCode,
        name: `Total ${block.currencyCode}`,
        detail: '',
        dueSource: block.dueSource,
        carryingBase: block.carryingBase,
        revaluedBase: block.revaluedBase,
        unrealized: block.unrealized,
        realized: block.realized,
        net: block.net,
        needsRate: block.needsRate,
      });
    }
  }

  if (result.unassignedRealized !== 0) {
    rows.push({
      kind: 'unassigned',
      key: 'u',
      currencyCode: '',
      name: 'Not tied to a currency',
      detail: 'Posted with no foreign balance, such as a manual journal',
      dueSource: null,
      carryingBase: null,
      revaluedBase: null,
      unrealized: null,
      realized: result.unassignedRealized,
      net: result.unassignedRealized,
      needsRate: false,
    });
  }

  if (result.currencies.length > 1 || (result.currencies.length > 0 && result.unassignedRealized !== 0)) {
    rows.push({
      kind: 'total',
      key: 't',
      currencyCode: result.baseCurrencyCode,
      name: 'Total',
      detail: '',
      dueSource: null,
      carryingBase: result.currencies.reduce((t, c) => t + c.carryingBase, 0),
      revaluedBase: null,
      unrealized: result.totalUnrealized,
      realized: result.totalRealized,
      net: result.totalNet,
      needsRate: false,
    });
  }

  return rows;
}

/**
 * What the report came to, in a sentence: the realized gain or loss, the unrealized one
 * and the net. Money is formatted by the caller so the sentence reads in the branch's own
 * currency. A gain reads as itself and a loss with a minus sign.
 */
export function foreignCurrencySentence(result: ForeignCurrencyGainLossResult, money: (value: number) => string): string {
  const signed = (value: number): string => (value < 0 ? `−${money(Math.abs(value))}` : money(value));

  return `Realized ${signed(result.totalRealized)} + unrealized ${signed(result.totalUnrealized)} = net ${signed(result.totalNet)}.`;
}

/** The sentence for currencies that have a balance and no rate; empty when every one has a rate. */
export function missingRatesSentence(result: Pick<ForeignCurrencyGainLossResult, 'currenciesWithoutRate'>): string {
  const codes = result.currenciesWithoutRate;
  if (codes.length === 0) {
    return '';
  }

  return `No rate for ${codes.join(', ')}, so ${codes.length === 1 ? 'its' : 'their'} unrealized gain or loss is not worked out. Enter a rate and run the report again.`;
}

/** The rates the form holds, as the request sends them: only a figure above nought, by upper-case code. */
export function ratesOf(values: Record<string, number | null | undefined>): Record<string, number> {
  const rates: Record<string, number> = {};

  for (const [code, value] of Object.entries(values)) {
    if (typeof value === 'number' && Number.isFinite(value) && value > 0) {
      rates[code.trim().toUpperCase()] = value;
    }
  }

  return rates;
}

/** The currency a refused rate belongs to, from the key the server gave it (`Rates[USD]`), or null for any other key. */
export function rateKeyCurrency(key: string): string | null {
  const match = /^rates\[(.+)\]$/i.exec(key);
  return match ? match[1].toUpperCase() : null;
}

/** A currency's label on the filter: the code, with how many accounts hold a balance in it. */
export function currencyChoiceLabel(choice: ForeignCurrencyChoice): string {
  return `${choice.currencyCode} · ${choice.accounts} ${choice.accounts === 1 ? 'account' : 'accounts'}`;
}
