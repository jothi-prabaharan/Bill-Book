import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  Announcement,
  EmployeeDetail,
  EmployeeListPage,
  EmployeeStatus,
  OrgMasterKind,
  OrgMasterRow,
  PolicyDocument,
  SaveEmployee,
} from './employee.models';

/** The Employee service's routes (TK-48), behind the Gateway's `/api/hrm`. */
@Injectable({ providedIn: 'root' })
export class EmployeeApiService {
  private readonly http = inject(HttpClient);

  organisation(kind: OrgMasterKind): Promise<OrgMasterRow[]> {
    return firstValueFrom(this.http.get<OrgMasterRow[]>(`/api/hrm/organisation/${kind}`));
  }

  saveOrganisation(kind: OrgMasterKind, id: number | null, body: Partial<OrgMasterRow>): Promise<{ id: number }> {
    return firstValueFrom(
      id === null
        ? this.http.post<{ id: number }>(`/api/hrm/organisation/${kind}`, body)
        : this.http.put<{ id: number }>(`/api/hrm/organisation/${kind}/${id}`, body),
    );
  }

  employees(query: { search?: string; departmentId?: number | null; status?: EmployeeStatus | null; page: number; pageSize: number }): Promise<EmployeeListPage> {
    const params: Record<string, string> = { page: String(query.page), pageSize: String(query.pageSize) };
    if (query.search) params['search'] = query.search;
    if (query.departmentId) params['departmentId'] = String(query.departmentId);
    if (query.status) params['status'] = query.status;
    return firstValueFrom(this.http.get<EmployeeListPage>('/api/hrm/employees', { params }));
  }

  employee(id: number): Promise<EmployeeDetail> {
    return firstValueFrom(this.http.get<EmployeeDetail>(`/api/hrm/employees/${id}`));
  }

  createEmployee(body: SaveEmployee): Promise<{ id: number; code: string }> {
    return firstValueFrom(this.http.post<{ id: number; code: string }>('/api/hrm/employees', body));
  }

  updateEmployee(id: number, body: SaveEmployee): Promise<void> {
    return firstValueFrom(this.http.put<void>(`/api/hrm/employees/${id}`, body));
  }

  announcements(): Promise<Announcement[]> {
    return firstValueFrom(this.http.get<Announcement[]>('/api/hrm/announcements'));
  }

  saveAnnouncement(id: number | null, body: Announcement): Promise<{ id: number }> {
    return firstValueFrom(
      id === null
        ? this.http.post<{ id: number }>('/api/hrm/announcements', body)
        : this.http.put<{ id: number }>(`/api/hrm/announcements/${id}`, body),
    );
  }

  policies(): Promise<PolicyDocument[]> {
    return firstValueFrom(this.http.get<PolicyDocument[]>('/api/hrm/policy-documents'));
  }

  savePolicy(id: number | null, body: PolicyDocument): Promise<{ id: number }> {
    return firstValueFrom(
      id === null
        ? this.http.post<{ id: number }>('/api/hrm/policy-documents', body)
        : this.http.put<{ id: number }>(`/api/hrm/policy-documents/${id}`, body),
    );
  }

  // Lifecycle
  checklistTemplates(kind?: string): Promise<any[]> {
    const params: Record<string, string> = {};
    if (kind) params['kind'] = kind;
    return firstValueFrom(this.http.get<any[]>('/api/hrm/lifecycle/templates', { params }));
  }

  saveChecklistTemplate(id: number | null, body: any): Promise<{ id: number }> {
    return firstValueFrom(
      id === null
        ? this.http.post<{ id: number }>('/api/hrm/lifecycle/templates', body)
        : this.http.put<{ id: number }>(`/api/hrm/lifecycle/templates/${id}`, body),
    );
  }

  employeeChecklist(employeeId: number, kind = 'Onboarding'): Promise<any> {
    return firstValueFrom(
      this.http.get<any>(`/api/hrm/lifecycle/checklists/${employeeId}`, {
        params: { kind },
      }),
    );
  }

  createEmployeeChecklist(body: any): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/hrm/lifecycle/checklists', body));
  }

  updateChecklistItem(itemId: number, body: any): Promise<void> {
    return firstValueFrom(this.http.put<void>(`/api/hrm/lifecycle/checklists/items/${itemId}`, body));
  }

  separation(employeeId: number): Promise<any> {
    return firstValueFrom(this.http.get<any>(`/api/hrm/lifecycle/separations/${employeeId}`));
  }

  submitSeparation(body: any): Promise<{ id: number }> {
    return firstValueFrom(this.http.post<{ id: number }>('/api/hrm/lifecycle/separations', body));
  }

  approveSeparation(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/hrm/lifecycle/separations/${id}/approve`, {}));
  }

  clearSeparation(id: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/hrm/lifecycle/separations/${id}/clear`, {}));
  }

  settleSeparation(employeeId: number, lastWorkingDate?: string): Promise<void> {
    const params: Record<string, string> = {};
    if (lastWorkingDate) params['lastWorkingDate'] = lastWorkingDate;
    return firstValueFrom(
      this.http.post<void>(`/api/hrm/lifecycle/separations/${employeeId}/settle`, null, { params }),
    );
  }

  // Self-Service & Team (H8, TK-55)
  myProfile(): Promise<import('./employee.models').MyProfile> {
    return firstValueFrom(this.http.get<import('./employee.models').MyProfile>('/api/hrm/me/profile'));
  }

  updateMyProfile(body: import('./employee.models').UpdateMyProfile): Promise<{ message: string }> {
    return firstValueFrom(this.http.put<{ message: string }>('/api/hrm/me/profile', body));
  }

  myDocuments(): Promise<import('./employee.models').EmployeeDocument[]> {
    return firstValueFrom(this.http.get<import('./employee.models').EmployeeDocument[]>('/api/hrm/me/documents'));
  }

  myAnnouncements(): Promise<import('./employee.models').Announcement[]> {
    return firstValueFrom(this.http.get<import('./employee.models').Announcement[]>('/api/hrm/me/announcements'));
  }

  teamMembers(): Promise<import('./employee.models').TeamMember[]> {
    return firstValueFrom(this.http.get<import('./employee.models').TeamMember[]>('/api/hrm/team/members'));
  }

  teamSummary(): Promise<import('./employee.models').TeamSummary> {
    return firstValueFrom(this.http.get<import('./employee.models').TeamSummary>('/api/hrm/team/summary'));
  }
}
