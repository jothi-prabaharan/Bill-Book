import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  FixedAssetReconciliationReportRequest,
  FixedAssetReconciliationReportResponse,
} from './fixed-asset-reconciliation-report.models';

@Injectable({ providedIn: 'root' })
export class FixedAssetReconciliationReportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/statements/fixed-asset-reconciliation';

  getReport(request: FixedAssetReconciliationReportRequest): Observable<FixedAssetReconciliationReportResponse> {
    let params = new HttpParams()
      .set('from', request.from)
      .set('to', request.to);

    if (request.orgId) {
      params = params.set('orgId', request.orgId);
    }

    return this.http.get<FixedAssetReconciliationReportResponse>(this.baseUrl, { params });
  }
}
