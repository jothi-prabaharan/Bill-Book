import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { PortalAttendance, PortalChild, PortalDemand, PortalExam, PortalReceipt } from './school-portal.models';

/** The parent portal's routes (S9, TK-69): children and marks from Student, fees from Fee, attendance from Attendance. */
@Injectable({ providedIn: 'root' })
export class SchoolPortalApi {
  private readonly http = inject(HttpClient);

  children(): Promise<PortalChild[]> {
    return firstValueFrom(this.http.get<PortalChild[]>('/api/portal/school/children'));
  }

  marks(studentId: number): Promise<PortalExam[]> {
    return firstValueFrom(this.http.get<PortalExam[]>(`/api/portal/school/children/${studentId}/marks`));
  }

  demands(): Promise<PortalDemand[]> {
    return firstValueFrom(this.http.get<PortalDemand[]>('/api/portal/school/fees/demands'));
  }

  receipts(): Promise<PortalReceipt[]> {
    return firstValueFrom(this.http.get<PortalReceipt[]>('/api/portal/school/fees/receipts'));
  }

  attendance(studentId: number, month: string): Promise<PortalAttendance> {
    return firstValueFrom(this.http.get<PortalAttendance>(`/api/portal/school/attendance/${studentId}`, { params: { month } }));
  }
}
