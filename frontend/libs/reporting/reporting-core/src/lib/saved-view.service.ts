import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { ReportQuery, SavedView } from './models/report-contracts';

/**
 * Saved layouts for a report.
 *
 * The server decides whose view it is from the token — this never sends an owner,
 * because a client that could name one could write into somebody else's list.
 */
@Injectable({ providedIn: 'root' })
export class SavedViewService {
  private readonly http = inject(HttpClient);

  /** This user's views for a report, plus the branch-wide ones. */
  async list(reportKey: string): Promise<SavedView[]> {
    return this.req<SavedView[]>('GET', `/api/reports/${reportKey}/views`);
  }

  async create(
    reportKey: string,
    viewName: string,
    layout: ReportQuery,
    options: { isShared: boolean; isDefault: boolean },
  ): Promise<SavedView> {
    return this.req<SavedView>('POST', `/api/reports/${reportKey}/views`, {
      viewName,
      layout,
      ...options,
    });
  }

  async update(reportKey: string, view: SavedView): Promise<SavedView> {
    return this.req<SavedView>(
      'PUT',
      `/api/reports/${reportKey}/views/${view.reportViewId}`,
      view,
    );
  }

  async remove(reportKey: string, viewId: number): Promise<void> {
    await this.req<void>('DELETE', `/api/reports/${reportKey}/views/${viewId}`);
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
