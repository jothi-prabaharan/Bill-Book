import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import {
  ExportFormat,
  ReportCatalogItem,
  ReportMetadata,
  ReportQuery,
  ReportResult,
} from './models/report-contracts';

/**
 * The reports API.
 *
 * **Owns no state.** It fetches and returns; the host page holds the query and
 * the result, so the grid stays a component that renders what it is given and
 * two pages showing the same report cannot disagree about which page they are on.
 *
 * No `window`, no `document` — this lib is Ionic-safe, and a browser global here
 * is what stops the same code running in the desktop and mobile shells.
 */
@Injectable({ providedIn: 'root' })
export class ReportQueryService {
  private readonly http = inject(HttpClient);

  /** The reports this user may run. Ones they may not are absent, not refused. */
  async catalog(): Promise<ReportCatalogItem[]> {
    return this.req<ReportCatalogItem[]>('GET', '/api/reports');
  }

  /** One report's parameters and columns, for building its screen before it runs. */
  async metadata(reportKey: string): Promise<ReportMetadata> {
    return this.req<ReportMetadata>('GET', `/api/reports/${reportKey}`);
  }

  /**
   * Runs the report.
   *
   * POST for a read, because a filter with two hundred item ids does not fit a
   * query string and a URL that long is refused by proxies before it arrives.
   */
  async run(reportKey: string, query: ReportQuery): Promise<ReportResult> {
    return this.req<ReportResult>('POST', `/api/reports/${reportKey}/query`, query);
  }

  /**
   * Exports the whole result — the same query without paging.
   *
   * Returns the blob rather than saving it: what to do with a file is the shell's
   * business, and the desktop app does not do it the way a browser does.
   */
  async export(
    reportKey: string,
    query: ReportQuery,
    format: ExportFormat = 'Xlsx',
  ): Promise<Blob> {
    return new Promise((resolve, reject) =>
      this.http
        .post(`/api/reports/${reportKey}/export?format=${format}`, query, {
          responseType: 'blob',
        })
        .subscribe({ next: resolve, error: reject }),
    );
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
