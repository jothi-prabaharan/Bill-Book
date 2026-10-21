import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  BankReconciliationAccount,
  BankReconciliationReportRequest,
  BankReconciliationReportResult,
} from './bank-reconciliation-report.models';

/**
 * The Reconciliation Report's own endpoints (TK-202). Owns no state and
 * touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class BankReconciliationReportService {
  private readonly http = inject(HttpClient);

  /** The branch's bank accounts, active ones first. */
  async accounts(): Promise<BankReconciliationAccount[]> {
    return new Promise((resolve, reject) =>
      this.http
        .get<BankReconciliationAccount[]>('/api/statements/bank-reconciliation/accounts')
        .subscribe({ next: resolve, error: reject }),
    );
  }

  /** The reconciliation of one bank account at the end of one day. */
  async run(request: BankReconciliationReportRequest): Promise<BankReconciliationReportResult> {
    const params = new HttpParams()
      .set('bankAccountId', String(request.bankAccountId))
      .set('asOfDate', request.asOfDate);

    return new Promise((resolve, reject) =>
      this.http
        .get<BankReconciliationReportResult>('/api/statements/bank-reconciliation', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
