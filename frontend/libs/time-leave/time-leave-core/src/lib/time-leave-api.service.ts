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

  // Self-Service & Approvals (H8, TK-55)
  myLeaveBalances(): Promise<LeaveBalanceView[]> {
    return firstValueFrom(this.http.get<LeaveBalanceView[]>('/api/tla/me/leave/balances'));
  }

  myLeaveApplications(): Promise<LeaveApplicationView[]> {
    return firstValueFrom(this.http.get<LeaveApplicationView[]>('/api/tla/me/leave/applications'));
  }

  applyMyLeave(body: import('./time-leave.models').ApplyLeaveSelfRequest): Promise<LeaveApplicationView> {
    return firstValueFrom(this.http.post<LeaveApplicationView>('/api/tla/me/leave/apply', body));
  }

  cancelMyLeave(id: number): Promise<{ message: string }> {
    return firstValueFrom(this.http.post<{ message: string }>(`/api/tla/me/leave/${id}/cancel`, {}));
  }

  myAttendance(month?: number, year?: number): Promise<any> {
    const params: Record<string, string> = {};
    if (month) params['month'] = String(month);
    if (year) params['year'] = String(year);
    return firstValueFrom(this.http.get<any>('/api/tla/me/attendance', { params }));
  }

  myPunches(date?: string): Promise<any[]> {
    const params: Record<string, string> = {};
    if (date) params['date'] = date;
    return firstValueFrom(this.http.get<any[]>('/api/tla/me/punches', { params }));
  }

  mobilePunch(body: import('./time-leave.models').MobilePunchSelfRequest): Promise<any> {
    return firstValueFrom(this.http.post<any>('/api/tla/me/punches/mobile', body));
  }

  myRegularisations(): Promise<any[]> {
    return firstValueFrom(this.http.get<any[]>('/api/tla/me/regularisation'));
  }

  submitMyRegularisation(body: import('./time-leave.models').SubmitRegularisationSelfRequest): Promise<any> {
    return firstValueFrom(this.http.post<any>('/api/tla/me/regularisation', body));
  }

  myOvertime(): Promise<any[]> {
    return firstValueFrom(this.http.get<any[]>('/api/tla/me/overtime'));
  }

  submitMyOvertime(body: import('./time-leave.models').SubmitOvertimeSelfRequest): Promise<any> {
    return firstValueFrom(this.http.post<any>('/api/tla/me/overtime', body));
  }

  pendingApprovals(): Promise<import('./time-leave.models').PendingApprovalItem[]> {
    return firstValueFrom(this.http.get<import('./time-leave.models').PendingApprovalItem[]>('/api/tla/approvals/pending'));
  }

  approvalHistory(): Promise<import('./time-leave.models').PendingApprovalItem[]> {
    return firstValueFrom(this.http.get<import('./time-leave.models').PendingApprovalItem[]>('/api/tla/approvals/history'));
  }

  actOnApproval(stepId: number, body: import('./time-leave.models').ActApprovalRequest): Promise<{ message: string }> {
    return firstValueFrom(this.http.post<{ message: string }>(`/api/tla/approvals/${stepId}/act`, body));
  }
}
