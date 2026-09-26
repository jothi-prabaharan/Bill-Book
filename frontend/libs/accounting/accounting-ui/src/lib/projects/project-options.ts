import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { BbSelectOption } from '@bill-book/ui-components';

/** A project as `GET api/projects` lists it (TK-104). */
export interface ProjectListItem {
  projectId: number;
  projectCode: string;
  projectName: string;
  contactId: number | null;
  billingMethod: 'FixedFee' | 'TimeAndMaterials' | 'NonBillable';
  status: 'Active' | 'OnHold' | 'Completed' | 'Cancelled';
  startDate: string | null;
  endDate: string | null;
  budgetAmount: number | null;
  currencyCode: string;
}

/** Active and on-hold projects take postings; completed and cancelled ones do not. */
export function isPostableProject(status: ProjectListItem['status']): boolean {
  return status === 'Active' || status === 'OnHold';
}

/**
 * The projects a line may be tagged with, as a picker's options. A project
 * the line already names stays in the list even if its job has since closed,
 * so an old draft shows what it says rather than a blank.
 */
export function projectOptions(rows: readonly ProjectListItem[], keep: readonly (number | null)[] = []): BbSelectOption<number>[] {
  return rows
    .filter((p) => isPostableProject(p.status) || keep.includes(p.projectId))
    .map((p) => ({ value: p.projectId, label: `${p.projectCode} · ${p.projectName}` }));
}

/**
 * The branch's projects, or none when the user cannot read them. A line
 * editor offers the picker only when there is something to pick; a user
 * without `projects.view` simply does not see it.
 */
export async function loadProjects(http: HttpClient): Promise<ProjectListItem[]> {
  try {
    return await firstValueFrom(http.get<ProjectListItem[]>('/api/projects'));
  } catch {
    return [];
  }
}
