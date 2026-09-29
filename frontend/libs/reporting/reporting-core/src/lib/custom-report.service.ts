import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { CustomReport, ReportQuery } from './models/report-contracts';

/** What saving a custom report sends; the server sets the owner from the token. */
export interface CustomReportDraft {
  datasetKey: string;
  title: string;
  description: string | null;
  isShared: boolean;
  layout: ReportQuery;
}

/**
 * Custom reports (TK-113): a dataset and a saved layout. They run through the
 * dataset's own query route; this only keeps the layouts.
 */
@Injectable({ providedIn: 'root' })
export class CustomReportService {
  private readonly http = inject(HttpClient);

  async get(id: number): Promise<CustomReport> {
    return this.req<CustomReport>('GET', `/api/reports/custom/${id}`);
  }

  async create(draft: CustomReportDraft): Promise<CustomReport> {
    return this.req<CustomReport>('POST', '/api/reports/custom', draft);
  }

  async update(id: number, draft: CustomReportDraft): Promise<CustomReport> {
    return this.req<CustomReport>('PUT', `/api/reports/custom/${id}`, draft);
  }

  async remove(id: number): Promise<void> {
    await this.req<void>('DELETE', `/api/reports/custom/${id}`);
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}

/** The link a catalog entry opens: a custom report is its dataset with `?custom=`. */
export function catalogLink(item: { reportKey: string; customReportId?: number | null }): {
  path: string[];
  queryParams: Record<string, number> | null;
} {
  return item.customReportId
    ? { path: ['/reports', item.reportKey], queryParams: { custom: item.customReportId } }
    : { path: ['/reports', item.reportKey], queryParams: null };
}
