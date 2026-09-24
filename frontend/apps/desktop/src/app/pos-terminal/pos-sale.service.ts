import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { TenderMode } from './pos-tender';

/** A bank or cash account a tender can land in (`GET api/bank-accounts/tender-options`). */
export interface TenderAccount {
  bankAccountId: number;
  accountName: string;
  /** `Cash`, `Current`, `Savings`, `Wallet`, … as Accounting names it. */
  accountType: string;
  isDefault: boolean;
}

export interface PosTenderPayload {
  mode: TenderMode;
  /** Rupees. */
  amount: number;
  bankAccountId: number;
  reference?: string;
}

export interface PosSalePayload {
  tillId: number;
  documentDate: string;
  contactId: number;
  contactGstin?: string;
  lines: unknown[];
  tenders: PosTenderPayload[];
}

export interface PosSaleResult {
  invoiceId: number;
  documentNo: string;
  totalAmount: number;
  changeAmount: number;
}

/**
 * The till's two server calls (TK-40, over TK-39): the accounts money can go
 * into, and the sale itself — made, paid and posted in one request.
 */
@Injectable({ providedIn: 'root' })
export class PosSaleService {
  private readonly http = inject(HttpClient);

  tenderAccounts(): Promise<TenderAccount[]> {
    return firstValueFrom(this.http.get<TenderAccount[]>('/api/bank-accounts/tender-options'));
  }

  sell(sale: PosSalePayload): Promise<PosSaleResult> {
    return firstValueFrom(this.http.post<PosSaleResult>('/api/sales/pos/sales', sale));
  }

  /** The account a mode defaults to: a cash account for cash, the first non-cash one otherwise; the default flag first. */
  static defaultAccountFor(mode: TenderMode, accounts: readonly TenderAccount[]): TenderAccount | null {
    const fits = accounts.filter((a) => (mode === 'Cash') === (a.accountType === 'Cash'));
    return fits.find((a) => a.isDefault) ?? fits[0] ?? null;
  }
}
