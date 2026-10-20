import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  SalaryIncrementReportOptions,
  SalaryIncrementReportRequest,
  SalaryIncrementReportResult,
} from './salary-increment-report.models';

/**
 * The Salary Increment Report's own endpoint (TK-198), not the shared
 * `api/reports/{key}/query`. Owns no state, like `ReportQueryService`, and
 * touches no browser global, so it runs in a mobile shell too.
 */
@Injectable({ providedIn: 'root' })
export class SalaryIncrementReportService {
  private readonly http = inject(HttpClient);

  /** The employees and departments the filters offer. */
  async options(): Promise<SalaryIncrementReportOptions> {
    return this.req<SalaryIncrementReportOptions>('GET', '/api/reports/salary-increment/options');
  }

  /** Runs the report. POST for a read, like every report run. */
  async run(request: SalaryIncrementReportRequest): Promise<SalaryIncrementReportResult> {
    return this.req<SalaryIncrementReportResult>('POST', '/api/reports/salary-increment', request);
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
