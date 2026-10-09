import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { SaveSiteDto, SiteDto } from './site.models';

type Saved = { id: number };

@Injectable({ providedIn: 'root' })
export class SiteApiService {
  private readonly http = inject(HttpClient);

  list(): Promise<SiteDto[]> {
    return firstValueFrom(this.http.get<SiteDto[]>('/api/site/sites'));
  }

  get(id: number): Promise<SiteDto> {
    return firstValueFrom(this.http.get<SiteDto>(`/api/site/sites/${id}`));
  }

  save(id: number | null, body: SaveSiteDto): Promise<Saved> {
    return firstValueFrom(
      id === null ? this.http.post<Saved>('/api/site/sites', body) : this.http.put<Saved>(`/api/site/sites/${id}`, body)
    );
  }
}
