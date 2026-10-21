import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { CashFlowReportRequest, CashFlowReportResult } from './cash-flow-report.models';

/**
 * The Cash Flow Statement – Direct's own endpoint (TK-201). Owns no state and
 * touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class CashFlowReportService {
  private readonly http = inject(HttpClient);

  /** The statement for a period, and for a comparison period when asked. */
  async run(request: CashFlowReportRequest): Promise<CashFlowReportResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.compareFromDate) {
      params = params.set('compareFromDate', request.compareFromDate);
    }
    if (request.compareToDate) {
      params = params.set('compareToDate', request.compareToDate);
    }

    return new Promise((resolve, reject) =>
      this.http
        .get<CashFlowReportResult>('/api/statements/cash-flow', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
