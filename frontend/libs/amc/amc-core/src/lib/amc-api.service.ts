import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AmcContract, AmcVisit, ContractStatus, RecordVisit, SaveContract, VendorOption } from './amc.models';

type Saved = { id: number };

/** The Amc service's routes (S8, TK-68), behind the Gateway's `/api/amc`, and Master's vendor list. */
@Injectable({ providedIn: 'root' })
export class AmcApiService {
  private readonly http = inject(HttpClient);

  contracts(status?: ContractStatus | null): Promise<AmcContract[]> {
    return firstValueFrom(this.http.get<AmcContract[]>('/api/amc/contracts', { params: status ? { status } : {} }));
  }

  contract(id: number): Promise<AmcContract> {
    return firstValueFrom(this.http.get<AmcContract>(`/api/amc/contracts/${id}`));
  }

  save(id: number | null, body: SaveContract): Promise<Saved> {
    return firstValueFrom(id === null
      ? this.http.post<Saved>('/api/amc/contracts', body)
      : this.http.put<Saved>(`/api/amc/contracts/${id}`, body));
  }

  activate(id: number): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/amc/contracts/${id}/activate`, {}));
  }

  terminate(id: number, reason: string): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/amc/contracts/${id}/terminate`, { reason }));
  }

  visits(id: number): Promise<AmcVisit[]> {
    return firstValueFrom(this.http.get<AmcVisit[]>(`/api/amc/contracts/${id}/visits`));
  }

  recordVisit(id: number, body: RecordVisit): Promise<Saved> {
    return firstValueFrom(this.http.post<Saved>(`/api/amc/contracts/${id}/visits`, body));
  }

  vendors(): Promise<VendorOption[]> {
    return firstValueFrom(this.http.get<VendorOption[]>('/api/contacts', { params: { role: 'vendor', includeInactive: 'false' } }));
  }
}
