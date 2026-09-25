import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { GenerationResult, Occurrence, OccurrenceStatus, PreventivePlan, SavePlan } from './preventive.models';

type Saved = { id: number };

/** The Preventive service's routes (S7, TK-67), behind the Gateway's `/api/preventive`. */
@Injectable({ providedIn: 'root' })
export class PreventiveApiService {
  private readonly http = inject(HttpClient);

  plans(): Promise<PreventivePlan[]> {
    return firstValueFrom(this.http.get<PreventivePlan[]>('/api/preventive/plans'));
  }

  savePlan(id: number | null, body: SavePlan): Promise<Saved> {
    return firstValueFrom(id === null
      ? this.http.post<Saved>('/api/preventive/plans', body)
      : this.http.put<Saved>(`/api/preventive/plans/${id}`, body));
  }

  occurrences(query: { planId?: number | null; status?: OccurrenceStatus | null }): Promise<Occurrence[]> {
    const params: Record<string, string> = {};
    if (query.planId) params['planId'] = String(query.planId);
    if (query.status) params['status'] = query.status;
    return firstValueFrom(this.http.get<Occurrence[]>('/api/preventive/occurrences', { params }));
  }

  setOccurrence(id: number, occurrenceStatus: OccurrenceStatus): Promise<Saved> {
    return firstValueFrom(this.http.put<Saved>(`/api/preventive/occurrences/${id}`, { occurrenceStatus }));
  }

  generate(): Promise<GenerationResult> {
    return firstValueFrom(this.http.post<GenerationResult>('/api/preventive/generate', {}));
  }
}
