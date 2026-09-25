import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AssetStatus, Building, FacilityAsset, SaveAsset, SaveBuilding, SaveSpace, Space } from './facility.models';

type Saved = { id: number };

/** The Facility service's routes (S5, TK-65), behind the Gateway's `/api/facility`. */
@Injectable({ providedIn: 'root' })
export class FacilityApiService {
  private readonly http = inject(HttpClient);

  buildings(): Promise<Building[]> {
    return firstValueFrom(this.http.get<Building[]>('/api/facility/buildings'));
  }

  saveBuilding(id: number | null, body: SaveBuilding): Promise<Saved> {
    return this.save('/api/facility/buildings', id, body);
  }

  spaces(buildingId?: number | null): Promise<Space[]> {
    return firstValueFrom(this.http.get<Space[]>('/api/facility/spaces', { params: buildingId ? { buildingId: String(buildingId) } : {} }));
  }

  saveSpace(id: number | null, body: SaveSpace): Promise<Saved> {
    return this.save('/api/facility/spaces', id, body);
  }

  assets(query: { spaceId?: number | null; status?: AssetStatus | null; search?: string }): Promise<FacilityAsset[]> {
    const params: Record<string, string> = {};
    if (query.spaceId) params['spaceId'] = String(query.spaceId);
    if (query.status) params['status'] = query.status;
    if (query.search) params['search'] = query.search;
    return firstValueFrom(this.http.get<FacilityAsset[]>('/api/facility/assets', { params }));
  }

  saveAsset(id: number | null, body: SaveAsset): Promise<Saved> {
    return this.save('/api/facility/assets', id, body);
  }

  private save<T>(url: string, id: number | null, body: T): Promise<Saved> {
    return firstValueFrom(id === null ? this.http.post<Saved>(url, body) : this.http.put<Saved>(`${url}/${id}`, body));
  }
}
