import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BusinessPerformanceReportRequest, BusinessPerformanceReportResult } from './business-performance-report.models';

/**
 * Business Performance's own endpoint (TK-203). Owns no state and touches no
 * browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class BusinessPerformanceReportService {
  private readonly http = inject(HttpClient);

  /** The ratios for a period, and for a comparison period when asked. */
  async run(request: BusinessPerformanceReportRequest): Promise<BusinessPerformanceReportResult> {
    let params = new HttpParams().set('fromDate', request.fromDate).set('toDate', request.toDate);
    if (request.compareFromDate) {
      params = params.set('compareFromDate', request.compareFromDate);
    }
    if (request.compareToDate) {
      params = params.set('compareToDate', request.compareToDate);
    }

    return new Promise((resolve, reject) =>
      this.http
        .get<BusinessPerformanceReportResult>('/api/statements/business-performance', { params })
        .subscribe({ next: resolve, error: reject }),
    );
  }
}
