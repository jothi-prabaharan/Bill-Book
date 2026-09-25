import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DailyAttendanceView,
  ShiftView,
  TimeLeaveApiService,
  WeeklyOffPolicyView,
} from '@bill-book/time-leave-core';

@Component({
  standalone: true,
  selector: 'bb-attendance-dashboard',
  imports: [CommonModule, FormsModule],
  templateUrl: './attendance-dashboard.page.html',
  styleUrl: '../time-leave-page.scss',
})
export class AttendanceDashboardPage implements OnInit {
  private readonly api = inject(TimeLeaveApiService);

  readonly loading = signal(false);
  readonly showRegularise = signal(false);
  readonly shifts = signal<ShiftView[]>([]);
  readonly weeklyOffs = signal<WeeklyOffPolicyView[]>([]);
  readonly attendances = signal<DailyAttendanceView[]>([]);

  selectedAttendance = signal<DailyAttendanceView | null>(null);

  regForm = {
    employeeId: 1,
    attendanceDate: new Date().toISOString().substring(0, 10),
    requestedIn: '',
    requestedOut: '',
    requestedStatus: 'Present',
    reason: '',
  };

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const s = await this.api.shifts();
      this.shifts.set(s);
      const w = await this.api.weeklyOffs();
      this.weeklyOffs.set(w);

      const today = new Date();
      const firstDay = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().substring(0, 10);
      const lastDay = today.toISOString().substring(0, 10);

      const records = await this.api.dailyAttendance(1, firstDay, lastDay);
      this.attendances.set(records);
    } catch {
      // offline / not configured
    } finally {
      this.loading.set(false);
    }
  }

  openRegularise(att: DailyAttendanceView): void {
    this.selectedAttendance.set(att);
    this.regForm.attendanceDate = att.attendanceDate;
    this.regForm.requestedIn = att.firstIn || '';
    this.regForm.requestedOut = att.lastOut || '';
    this.showRegularise.set(true);
  }

  closeRegularise(): void {
    this.showRegularise.set(false);
    this.selectedAttendance.set(null);
  }

  async submitRegularisation(): Promise<void> {
    try {
      await this.api.regularise(this.regForm);
      this.closeRegularise();
      await this.load();
    } catch (e: any) {
      alert(e?.message || 'Error submitting regularisation request');
    }
  }
}
