import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { JournalReportRequest, JournalReportResult, JournalSourceOption } from './journal-report.models';

interface AccountOption {
  accountId: number;
  accountCode: string;
  accountName: string;
}

/**
 * The Journal Report's own endpoint (TK-217), and the two lists its filters offer:
 * the document types that post to the ledger, from Master, and the chart of
 * accounts, from Accounting. Owns no state and touches no browser global, so it
 * runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class JournalReportService {
  private readonly http = inject(HttpClient);

  /** A page of the journals posted in a period. */
  async run(request: JournalReportRequest): Promise<JournalReportResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.source) {
      params = params.set('source', request.source);
    }
    if (request.status) {
      params = params.set('status', request.status);
    }
    if (request.accountId) {
      params = params.set('accountId', request.accountId);
    }
    if (request.pageNumber) {
      params = params.set('pageNumber', request.pageNumber);
    }
    if (request.pageSize) {
      params = params.set('pageSize', request.pageSize);
    }

    return new Promise((resolve, reject) =>
      this.http.get<JournalReportResult>('/api/statements/journal-report', { params }).subscribe({ next: resolve, error: reject }),
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
}
