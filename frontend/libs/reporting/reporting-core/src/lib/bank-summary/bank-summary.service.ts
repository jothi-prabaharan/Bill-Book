import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BankReconciliationAccount } from '../bank-reconciliation/bank-reconciliation-report.models';
import { BankSummaryRequest, BankSummaryResult } from './bank-summary.models';

/**
 * The Bank Summary's own endpoint (TK-219), and the one list its filter offers: the
 * branch's bank accounts, which the Reconciliation Report already serves. Owns no
 * state and touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class BankSummaryService {
  private readonly http = inject(HttpClient);

  /** Every bank account's balances over a period. */
  async run(request: BankSummaryRequest): Promise<BankSummaryResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.bankAccountId) {
      params = params.set('bankAccountId', request.bankAccountId);
    }

    return new Promise((resolve, reject) =>
      this.http.get<BankSummaryResult>('/api/statements/bank-summary', { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** The branch's bank accounts, active ones first, for the bank account filter. */
  async accounts(): Promise<BankReconciliationAccount[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<BankReconciliationAccount[]>('/api/statements/bank-reconciliation/accounts')
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
