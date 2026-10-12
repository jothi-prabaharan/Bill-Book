import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** A tax rate a document line may take: active and in force on the document's date. */
export interface TaxRateChoice {
  taxMasterId: number;
  taxGroupId: number;
  taxName: string;
  totalRate: number;
  cgstRate: number;
  sgstRate: number;
  igstRate: number;
  cessRate: number;
}

export interface PaymentTermChoice {
  paymentTermId: number;
  termName: string;
  termType: string;
  dueDays: number;
  dueDayOfMonth: number | null;
  /** The branch's preselected term. */
  isDefault: boolean;
}

/** An account a document line may post to: active, and not posting-locked. */
export interface AccountChoice {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  currencyCode: string | null;
}

/**
 * The number the next document will probably take — only probably, because it
 * is allocated when the document is saved. Null when the branch has no active
 * series for the code.
 */
export interface NextNumberChoice {
  seriesCode: string;
  number: string | null;
}

export interface PrintTemplateChoice {
  printTemplateId: number;
  templateName: string;
  isDefault: boolean;
}

/** The master rate for a currency pair on a date, as `GET /api/rates/exchange` returns it. */
export interface ExchangeRateChoice {
  exchangeRateId: number;
  fromCurrencyCode: string;
  toCurrencyCode: string;
  rateDate: string;
  rate: number;
  source: string;
}

/** The modules that answer each list — a module asks only for what its documents use. */
export type TaxRateModule = 'sales' | 'purchase' | 'inventory';
export type PaymentTermModule = 'sales' | 'purchase' | 'contacts';
export type AccountModule = 'sales' | 'purchase' | 'banking' | 'accounting' | 'contacts';
export type NumberedModule = 'sales' | 'purchase' | 'banking' | 'accounting' | 'inventory';
export type PrintModule = 'sales' | 'purchase' | 'banking' | 'accounting';

/**
 * The option lists a create page reads (TK-192).
 *
 * Each answers behind the asking module's own view permission rather than the
 * accounting or settings one that guards the masters, so a clerk who may raise a
 * bill can pick its tax rate, terms, account and layout without being able to
 * read the tax master or the chart of accounts. A module asks only for its own:
 * the server refuses a sales page asking for a bill's number.
 */
@Injectable({ providedIn: 'root' })
export class DocumentChoicesService {
  private readonly http = inject(HttpClient);

  /** Rates in force on `on` (an ISO date; today when omitted). */
  taxRates(module: TaxRateModule, on?: string | null): Promise<TaxRateChoice[]> {
    return firstValueFrom(this.http.get<TaxRateChoice[]>(`/api/choices/${module}/tax-rates`, { params: dated(on) }));
  }

  paymentTerms(module: PaymentTermModule): Promise<PaymentTermChoice[]> {
    return firstValueFrom(this.http.get<PaymentTermChoice[]>(`/api/choices/${module}/payment-terms`));
  }

  accounts(module: AccountModule): Promise<AccountChoice[]> {
    return firstValueFrom(this.http.get<AccountChoice[]>(`/api/choices/${module}/accounts`));
  }

  nextNumber(module: NumberedModule, seriesCode: string, on?: string | null): Promise<NextNumberChoice> {
    return firstValueFrom(
      this.http.get<NextNumberChoice>(`/api/choices/${module}/next-number/${encodeURIComponent(seriesCode)}`, {
        params: dated(on),
      }),
    );
  }

  /** The active layouts for one document type, the branch default first. */
  printTemplates(module: PrintModule, documentType: string): Promise<PrintTemplateChoice[]> {
    return firstValueFrom(
      this.http.get<PrintTemplateChoice[]>(`/api/print-templates/choices/${module}/${encodeURIComponent(documentType)}`),
    );
  }

  /**
   * The master's rate from one currency to another on `on`, or null when none
   * is on file. Read only: the create pages show the master's rate and never
   * change it (owner, 12 October 2026).
   */
  async exchangeRate(from: string, to: string, on?: string | null): Promise<ExchangeRateChoice | null> {
    let params = new HttpParams().set('from', from).set('to', to);
    if (on) {
      params = params.set('on', on);
    }

    try {
      return await firstValueFrom(this.http.get<ExchangeRateChoice>('/api/rates/exchange', { params }));
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return null;
      }
      throw error;
    }
  }
}

function dated(on: string | null | undefined): HttpParams {
  return on ? new HttpParams().set('on', on) : new HttpParams();
}
