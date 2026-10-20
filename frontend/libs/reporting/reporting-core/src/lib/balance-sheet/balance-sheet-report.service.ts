import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BalanceSheetReportRequest, BalanceSheetReportResult } from './balance-sheet-report.models';

/**
 * The Balance Sheet's own endpoint (TK-199). Owns no state and touches no
 * browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class BalanceSheetReportService {
  private readonly http = inject(HttpClient);

  /** The balance sheet at a date, and at a second one when asked. */
  async run(request: BalanceSheetReportRequest): Promise<BalanceSheetReportResult> {
    let params = new HttpParams().set('asOfDate', request.asOfDate);
    if (request.compareAsOfDate) {
      params = params.set('compareAsOfDate', request.compareAsOfDate);
    }
    if (request.isZeroBalanceShown) {
      params = params.set('isZeroBalanceShown', 'true');
    }

    return new Promise((resolve, reject) =>
      this.http
        .get<BalanceSheetReportResult>('/api/statements/balance-sheet', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
