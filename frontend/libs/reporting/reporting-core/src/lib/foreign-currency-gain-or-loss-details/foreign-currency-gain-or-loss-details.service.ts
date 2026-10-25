import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ForeignCurrencyDetailChoices, ForeignCurrencyDetailsRequest, ForeignCurrencyDetailsResult } from './foreign-currency-gain-or-loss-details.models';

/**
 * The Foreign Currency Gain or Loss Details report's own endpoint (TK-221) and the one lookup
 * its filters use: the currencies, accounts and contacts with a foreign-currency posting up to
 * a date. The rate master's rate for each currency comes from the summary report's service,
 * which the page also uses. Owns no state and touches no browser global, so it runs in a
 * mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class ForeignCurrencyGainLossDetailsService {
  private readonly http = inject(HttpClient);

  /** The gains and losses document by document for a period and date, at the rates given. */
  async run(request: ForeignCurrencyDetailsRequest): Promise<ForeignCurrencyDetailsResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.currencyCode) {
      params = params.set('currencyCode', request.currencyCode);
    }

    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }

    if (request.accountId) {
      params = params.set('accountId', request.accountId);
    }

    for (const [code, rate] of Object.entries(request.rates ?? {})) {
      params = params.set(`rates[${code}]`, String(rate));
    }

    return new Promise((resolve, reject) =>
      this.http
        .get<ForeignCurrencyDetailsResult>('/api/statements/foreign-currency-gain-or-loss-details', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** What the filters offer at a date, with the branch's base currency. */
  async choices(toDate: string): Promise<ForeignCurrencyDetailChoices> {
    const params = new HttpParams().set('toDate', toDate);

    return new Promise((resolve, reject) =>
      this.http
        .get<ForeignCurrencyDetailChoices>('/api/statements/foreign-currency-gain-or-loss-details/choices', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
