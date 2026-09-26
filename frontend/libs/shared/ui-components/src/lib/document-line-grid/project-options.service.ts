import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ProjectOption } from './document-line-grid.component';

interface ProjectRow {
  projectId: number;
  projectCode: string;
  projectName: string;
  status: string;
}

/**
 * The branch's open projects, as the line grid's picker offers them (TK-105).
 * A user who cannot read projects gets none, and the grid then shows no
 * project row at all. Completed and cancelled jobs take no postings, so they
 * are not offered.
 */
@Injectable({ providedIn: 'root' })
export class ProjectOptionsService {
  private readonly http = inject(HttpClient);

  async load(): Promise<ProjectOption[]> {
    try {
      const rows = await firstValueFrom(this.http.get<ProjectRow[]>('/api/projects'));
      return toProjectOptions(rows);
    } catch {
      return [];
    }
  }
}

/** Open projects, labelled by code and name. */
export function toProjectOptions(rows: readonly ProjectRow[]): ProjectOption[] {
  return rows
    .filter((p) => p.status === 'Active' || p.status === 'OnHold')
    .map((p) => ({ value: p.projectId, label: `${p.projectCode} · ${p.projectName}` }));
}
