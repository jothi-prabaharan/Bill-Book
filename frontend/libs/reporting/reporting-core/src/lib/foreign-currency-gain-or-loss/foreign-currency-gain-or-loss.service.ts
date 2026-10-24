import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  ForeignCurrencyChoices,
  ForeignCurrencyGainLossRequest,
  ForeignCurrencyGainLossResult,
} from './foreign-currency-gain-or-loss.models';

/**
 * The Foreign Currency Gain or Loss report's own endpoint (TK-220), the lookup for its
 * filter (which currencies hold a balance at a date) and the rate master's rate for a
 * currency pair, which the page offers as each currency's revaluation rate. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class ForeignCurrencyGainLossService {
  private readonly http = inject(HttpClient);

  /** The gains and losses for a period and date, at the rates given. */
  async run(request: ForeignCurrencyGainLossRequest): Promise<ForeignCurrencyGainLossResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('asAtDate', request.asAtDate);
    if (request.currencyCode) {
      params = params.set('currencyCode', request.currencyCode);
    }

    for (const [code, rate] of Object.entries(request.rates ?? {})) {
      params = params.set(`rates[${code}]`, String(rate));
    }

    return new Promise((resolve, reject) =>
      this.http
        .get<ForeignCurrencyGainLossResult>('/api/statements/foreign-currency-gain-or-loss', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The foreign currencies the branch holds a balance in at a date, with its base currency. */
  async choices(asAtDate: string): Promise<ForeignCurrencyChoices> {
    const params = new HttpParams().set('asAtDate', asAtDate);

    return new Promise((resolve, reject) =>
      this.http
        .get<ForeignCurrencyChoices>('/api/statements/foreign-currency-gain-or-loss/currencies', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /**
   * The rate master's rate on or before a date, in `to` per unit of `from`, or null when
   * none is on file. Read only: the report never changes a rate, and the figure is only
   * the starting value of a field the person can edit.
   */
  async masterRate(from: string, to: string, on: string): Promise<number | null> {
    const params = new HttpParams().set('from', from).set('to', to).set('on', on);

    return new Promise((resolve, reject) =>
      this.http.get<{ rate: number }>('/api/rates/exchange', { params }).subscribe({
        next: (row) => resolve(row.rate),
        error: (error: unknown) => (error instanceof HttpErrorResponse && error.status === 404 ? resolve(null) : reject(error)),
      }),
    );
  }
}
