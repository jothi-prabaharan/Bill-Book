import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  LaborerDto,
  LaborerPortalAssignmentDto,
  MarginRuleDto,
  ResolveMarginRequest,
  ResolveMarginResponse,
  SaveAssignmentDto,
  SaveLaborerDto,
  SaveMarginRuleDto,
  SaveSiteDto,
  SaveTradeDto,
  SiteAssignmentDto,
  CheckInPortalRequest,
  SiteAttendanceDetailDto,
  SiteAttendanceSummaryDto,
  SiteDto,
  TradeDto,
  DispatchStockRequest,
  ReturnStockRequest,
  SiteStockBalanceDto,
  SiteStockDocumentDto,
  SiteStockDocumentType,
  SubmitMaterialUsageRequest,
} from './site.models';

@Injectable({ providedIn: 'root' })
export class SiteApiService {
  private readonly http = inject(HttpClient);

  // --- Sites ---
  listSites(): Promise<SiteDto[]> {
    return firstValueFrom(this.http.get<SiteDto[]>('/api/site/sites'));
  }

  getSite(id: number): Promise<SiteDto> {
    return firstValueFrom(this.http.get<SiteDto>(`/api/site/sites/${id}`));
  }

  saveSite(id: number | null, body: SaveSiteDto): Promise<SiteDto> {
    return firstValueFrom(
      id === null ? this.http.post<SiteDto>('/api/site/sites', body) : this.http.put<SiteDto>(`/api/site/sites/${id}`, body)
    );
  }

  // --- Trades ---
  listTrades(): Promise<TradeDto[]> {
    return firstValueFrom(this.http.get<TradeDto[]>('/api/site/trades'));
  }

  getTrade(id: number): Promise<TradeDto> {
    return firstValueFrom(this.http.get<TradeDto>(`/api/site/trades/${id}`));
  }

  saveTrade(id: number | null, body: SaveTradeDto): Promise<TradeDto> {
    return firstValueFrom(
      id === null ? this.http.post<TradeDto>('/api/site/trades', body) : this.http.put<TradeDto>(`/api/site/trades/${id}`, body)
    );
  }

  // --- Laborers ---
  listLaborers(tradeId?: number | null): Promise<LaborerDto[]> {
    return firstValueFrom(
      this.http.get<LaborerDto[]>('/api/site/laborers', { params: tradeId ? { tradeId } : {} })
    );
  }

  getLaborer(id: number): Promise<LaborerDto> {
    return firstValueFrom(this.http.get<LaborerDto>(`/api/site/laborers/${id}`));
  }

  saveLaborer(id: number | null, body: SaveLaborerDto): Promise<LaborerDto> {
    return firstValueFrom(
      id === null ? this.http.post<LaborerDto>('/api/site/laborers', body) : this.http.put<LaborerDto>(`/api/site/laborers/${id}`, body)
    );
  }

  // --- Assignments ---
  listAssignments(siteId?: number | null, laborerId?: number | null): Promise<SiteAssignmentDto[]> {
    const params: Record<string, number> = {};
    if (siteId) params['siteId'] = siteId;
    if (laborerId) params['laborerId'] = laborerId;
    return firstValueFrom(this.http.get<SiteAssignmentDto[]>('/api/site/assignments', { params }));
  }

  getAssignment(id: number): Promise<SiteAssignmentDto> {
    return firstValueFrom(this.http.get<SiteAssignmentDto>(`/api/site/assignments/${id}`));
  }

  saveAssignment(id: number | null, body: SaveAssignmentDto): Promise<SiteAssignmentDto> {
    return firstValueFrom(
      id === null
        ? this.http.post<SiteAssignmentDto>('/api/site/assignments', body)
        : this.http.put<SiteAssignmentDto>(`/api/site/assignments/${id}`, body)
    );
  }

  // --- Margin Rules ---
  listMarginRules(): Promise<MarginRuleDto[]> {
    return firstValueFrom(this.http.get<MarginRuleDto[]>('/api/site/margin-rules'));
  }

  getMarginRule(id: number): Promise<MarginRuleDto> {
    return firstValueFrom(this.http.get<MarginRuleDto>(`/api/site/margin-rules/${id}`));
  }

  saveMarginRule(id: number | null, body: SaveMarginRuleDto): Promise<MarginRuleDto> {
    return firstValueFrom(
      id === null
        ? this.http.post<MarginRuleDto>('/api/site/margin-rules', body)
        : this.http.put<MarginRuleDto>(`/api/site/margin-rules/${id}`, body)
    );
  }

  resolveMargin(body: ResolveMarginRequest): Promise<ResolveMarginResponse> {
    return firstValueFrom(this.http.post<ResolveMarginResponse>('/api/site/margin-rules/resolve', body));
  }

  // --- Laborer Portal ---
  getPortalAssignments(laborerId?: number | null): Promise<LaborerPortalAssignmentDto[]> {
    const params: Record<string, number> = {};
    if (laborerId) params['laborerId'] = laborerId;
    return firstValueFrom(this.http.get<LaborerPortalAssignmentDto[]>('/api/site/portal/assignments', { params }));
  }

  checkIn(body: CheckInPortalRequest): Promise<SiteAttendanceSummaryDto> {
    return firstValueFrom(this.http.post<SiteAttendanceSummaryDto>('/api/site/portal/check-in', body));
  }

  clockOut(formData: FormData): Promise<SiteAttendanceSummaryDto> {
    return firstValueFrom(this.http.post<SiteAttendanceSummaryDto>('/api/site/portal/clock-out', formData));
  }

  getAttendance(id: number): Promise<SiteAttendanceDetailDto> {
    return firstValueFrom(this.http.get<SiteAttendanceDetailDto>(`/api/site/portal/attendance/${id}`));
  }

  getPhotoUrl(photoId: number): string {
    return `/api/site/portal/photos/${photoId}`;
  }

  // --- Admin Approvals (TK-172) ---
  listPendingAttendance(siteId?: number | null, fromDate?: string | null, toDate?: string | null): Promise<SiteAttendanceDetailDto[]> {
    const params: Record<string, string | number> = {};
    if (siteId) params['siteId'] = siteId;
    if (fromDate) params['fromDate'] = fromDate;
    if (toDate) params['toDate'] = toDate;
    return firstValueFrom(this.http.get<SiteAttendanceDetailDto[]>('/api/site/attendance/pending', { params }));
  }

  approveAttendance(id: number, otHoursApproved?: number | null): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/site/attendance/${id}/approve`, { otHoursApproved }));
  }

  rejectAttendance(id: number, reason: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/site/attendance/${id}/reject`, { reason }));
  }

  // --- Site Stock (TK-173) ---
  listSiteStockBalances(siteId: number): Promise<SiteStockBalanceDto[]> {
    return firstValueFrom(this.http.get<SiteStockBalanceDto[]>('/api/site/stock/balances', { params: { siteId } }));
  }

  listSiteStockDocuments(
    siteId?: number | null,
    type?: SiteStockDocumentType | null,
    fromDate?: string | null,
    toDate?: string | null
  ): Promise<SiteStockDocumentDto[]> {
    const params: Record<string, string | number> = {};
    if (siteId) params['siteId'] = siteId;
    if (type) params['type'] = type;
    if (fromDate) params['fromDate'] = fromDate;
    if (toDate) params['toDate'] = toDate;
    return firstValueFrom(this.http.get<SiteStockDocumentDto[]>('/api/site/stock/documents', { params }));
  }

  getSiteStockDocument(id: number): Promise<SiteStockDocumentDto> {
    return firstValueFrom(this.http.get<SiteStockDocumentDto>(`/api/site/stock/documents/${id}`));
  }

  dispatchStock(req: DispatchStockRequest): Promise<SiteStockDocumentDto> {
    return firstValueFrom(this.http.post<SiteStockDocumentDto>('/api/site/stock/dispatch', req));
  }

  returnStock(req: ReturnStockRequest): Promise<SiteStockDocumentDto> {
    return firstValueFrom(this.http.post<SiteStockDocumentDto>('/api/site/stock/return', req));
  }

  // --- Material Usage (TK-174) ---
  submitMaterialUsage(req: SubmitMaterialUsageRequest): Promise<SiteStockDocumentDto> {
    return firstValueFrom(this.http.post<SiteStockDocumentDto>('/api/site/stock/usage', req));
  }

  listPendingMaterialUsage(siteId?: number | null): Promise<SiteStockDocumentDto[]> {
    const params: Record<string, number> = {};
    if (siteId) params['siteId'] = siteId;
    return firstValueFrom(this.http.get<SiteStockDocumentDto[]>('/api/site/stock/usage/pending', { params }));
  }

  approveMaterialUsage(id: number): Promise<SiteStockDocumentDto> {
    return firstValueFrom(this.http.post<SiteStockDocumentDto>(`/api/site/stock/usage/${id}/approve`, {}));
  }

  rejectMaterialUsage(id: number, reason: string): Promise<SiteStockDocumentDto> {
    return firstValueFrom(this.http.post<SiteStockDocumentDto>(`/api/site/stock/usage/${id}/reject`, { reason }));
  }

  submitPortalUsage(req: SubmitMaterialUsageRequest): Promise<SiteStockDocumentDto> {
    return firstValueFrom(this.http.post<SiteStockDocumentDto>('/api/site/portal/usage', req));
  }
}


