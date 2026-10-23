import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { JournalSourceOption } from '../journal-report/journal-report.models';
import { AccountTransactionRequest, AccountTransactionResult, TaxRateOption, taxRateOptions } from './account-transaction.models';

interface AccountOption {
  accountId: number;
  accountCode: string;
  accountName: string;
}

interface ContactOption {
  contactId: number;
  contactCode: string;
  displayName: string;
}

/**
 * The Account Transaction report's own endpoint (TK-218), and the four lists its
 * filters offer: the chart of accounts and the tax rates from Accounting, the
 * contacts from Master, and the document types that post to the ledger. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class AccountTransactionService {
  private readonly http = inject(HttpClient);

  /** A page of the period's postings, grouped by account. */
  async run(request: AccountTransactionRequest): Promise<AccountTransactionResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    for (const id of request.accountIds ?? []) {
      params = params.append('accountIds', id);
    }
    if (request.contactId) {
      params = params.set('contactId', request.contactId);
    }
    if (request.taxGroupId) {
      params = params.set('taxGroupId', request.taxGroupId);
    }
    if (request.source) {
      params = params.set('source', request.source);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<AccountTransactionResult>('/api/statements/account-transaction', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The document types that post to the ledger, by name. */
  async sources(): Promise<JournalSourceOption[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<JournalSourceOption[]>('/api/master/transaction-types', { params: new HttpParams().set('postingOnly', 'true') })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's chart of accounts, for the account filter. */
  async accounts(): Promise<{ value: number; label: string }[]> {
    return new Promise((resolve, reject) =>
      this.http.get<AccountOption[]>('/api/accounts').subscribe({
        next: (accounts) => resolve(accounts.map((a) => ({ value: a.accountId, label: `${a.accountCode} — ${a.accountName}` }))),
        error: reject,
      }),
    );
  }

  /** The branch's contacts, for the contact filter. */
  async contacts(): Promise<{ value: number; label: string }[]> {
    return new Promise((resolve, reject) =>
      this.http.get<ContactOption[]>('/api/contacts').subscribe({
        next: (contacts) => resolve(contacts.map((c) => ({ value: c.contactId, label: `${c.contactCode} — ${c.displayName}` }))),
        error: reject,
      }),
    );
  }

  /** The branch's tax rates, one for each group. */
  async taxRates(): Promise<TaxRateOption[]> {
    return new Promise((resolve, reject) =>
      this.http.get<Parameters<typeof taxRateOptions>[0]>('/api/tax-masters').subscribe({
        next: (rates) => resolve(taxRateOptions(rates)),
        error: reject,
      }),
    );
  }
}
