import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ApplyLeave,
  DailyAttendanceView,
  LeaveApplicationView,
  LeaveBalanceView,
  LeavePolicyView,
  LeaveTypeView,
  RegularisationRequestView,
  ShiftView,
  WeeklyOffPolicyView,
} from './time-leave.models';

@Injectable({ providedIn: 'root' })
export class TimeLeaveApiService {
  private readonly http = inject(HttpClient);

  // Leave
  leaveTypes(): Promise<LeaveTypeView[]> {
    return firstValueFrom(this.http.get<LeaveTypeView[]>('/api/tla/leave/types'));
  }

  leavePolicies(): Promise<LeavePolicyView[]> {
    return firstValueFrom(this.http.get<LeavePolicyView[]>('/api/tla/leave/policies'));
  }

  leaveBalances(employeeId: number, year: number): Promise<LeaveBalanceView[]> {
    return firstValueFrom(
      this.http.get<LeaveBalanceView[]>(`/api/tla/leave/balances?employeeId=${employeeId}&year=${year}`),
    );
  }

  applyLeave(body: ApplyLeave): Promise<LeaveApplicationView> {
    return firstValueFrom(this.http.post<LeaveApplicationView>('/api/tla/leave/applications', body));
  }

  approveLeave(applicationId: number, comments?: string): Promise<LeaveApplicationView> {
    return firstValueFrom(
      this.http.post<LeaveApplicationView>(`/api/tla/leave/applications/${applicationId}/approve`, {
        decision: 'Approved',
        comments,
      }),
    );
  }

  // Attendance
  shifts(): Promise<ShiftView[]> {
    return firstValueFrom(this.http.get<ShiftView[]>('/api/tla/attendance/shifts'));
  }

  weeklyOffs(): Promise<WeeklyOffPolicyView[]> {
    return firstValueFrom(this.http.get<WeeklyOffPolicyView[]>('/api/tla/attendance/weekly-offs'));
  }

  dailyAttendance(employeeId: number, from: string, to: string): Promise<DailyAttendanceView[]> {
    return firstValueFrom(
      this.http.get<DailyAttendanceView[]>(`/api/tla/attendance/daily?employeeId=${employeeId}&from=${from}&to=${to}`),
    );
  }

  regularise(body: {
    employeeId: number;
    attendanceDate: string;
    requestedIn?: string;
    requestedOut?: string;
    requestedStatus: string;
    reason: string;
  }): Promise<RegularisationRequestView> {
    return firstValueFrom(
      this.http.post<RegularisationRequestView>('/api/tla/attendance/regularisations', body),
    );
  }
}
