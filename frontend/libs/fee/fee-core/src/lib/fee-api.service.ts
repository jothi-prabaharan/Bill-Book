import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  BankAccountOption,
  Concession,
  FeeDemand,
  FeeHead,
  FeeReceipt,
  FeeStructure,
  GenerateResult,
  PostableAccount,
  SaveConcession,
  SaveFeeStructure,
  SaveReceipt,
} from './fee.models';

type Saved = { id: number };

/** The Fee service's routes (S4, TK-64), behind the Gateway's `/api/fee`. */
@Injectable({ providedIn: 'root' })
export class FeeApiService {
  private readonly http = inject(HttpClient);

  heads(): Promise<FeeHead[]> {
    return firstValueFrom(this.http.get<FeeHead[]>('/api/fee/heads'));
  }

  saveHead(id: number | null, body: Omit<FeeHead, 'feeHeadId'>): Promise<Saved> {
    return this.save('/api/fee/heads', id, body);
  }

  structures(academicYearId?: number | null): Promise<FeeStructure[]> {
    return firstValueFrom(
      this.http.get<FeeStructure[]>('/api/fee/structures', { params: academicYearId ? { academicYearId: String(academicYearId) } : {} }),
    );
  }

  saveStructure(id: number | null, body: SaveFeeStructure): Promise<Saved> {
    return this.save('/api/fee/structures', id, body);
  }

  concessions(): Promise<Concession[]> {
    return firstValueFrom(this.http.get<Concession[]>('/api/fee/concessions'));
  }

  saveConcession(id: number | null, body: SaveConcession): Promise<Saved> {
    return this.save('/api/fee/concessions', id, body);
  }

  postableAccounts(): Promise<PostableAccount[]> {
    return firstValueFrom(this.http.get<PostableAccount[]>('/api/fee/postable-accounts'));
  }

  bankAccounts(): Promise<BankAccountOption[]> {
    return firstValueFrom(this.http.get<BankAccountOption[]>('/api/fee/bank-accounts'));
  }

  demands(query: { contactId?: number | null; feeStructureId?: number | null; periodKey?: string | null; open?: boolean }): Promise<FeeDemand[]> {
    const params: Record<string, string> = {};
    if (query.contactId) params['contactId'] = String(query.contactId);
    if (query.feeStructureId) params['feeStructureId'] = String(query.feeStructureId);
    if (query.periodKey) params['periodKey'] = query.periodKey;
    if (query.open) params['open'] = 'true';
    return firstValueFrom(this.http.get<FeeDemand[]>('/api/fee/demands', { params }));
  }

  generate(feeStructureId: number, periodKey: string, demandDate: string): Promise<GenerateResult> {
    return firstValueFrom(this.http.post<GenerateResult>('/api/fee/demands/generate', { feeStructureId, periodKey, demandDate }));
  }

  post(body: { feeDemandIds?: number[]; feeStructureId?: number; periodKey?: string }): Promise<{ posted: number }> {
    return firstValueFrom(this.http.post<{ posted: number }>('/api/fee/demands/post', body));
  }

  voidDemand(id: number, reason: string): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/fee/demands/${id}/void`, { reason }));
  }

  receipts(contactId?: number | null): Promise<FeeReceipt[]> {
    return firstValueFrom(this.http.get<FeeReceipt[]>('/api/fee/receipts', { params: contactId ? { contactId: String(contactId) } : {} }));
  }

  createReceipt(body: SaveReceipt): Promise<FeeReceipt> {
    return firstValueFrom(this.http.post<FeeReceipt>('/api/fee/receipts', body));
  }

  voidReceipt(id: number, reason: string): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/fee/receipts/${id}/void`, { reason }));
  }

  private save<T>(url: string, id: number | null, body: T): Promise<Saved> {
    return firstValueFrom(id === null ? this.http.post<Saved>(url, body) : this.http.put<Saved>(`${url}/${id}`, body));
  }
}
