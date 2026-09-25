import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ApplyLeave,
  LeaveApplicationView,
  LeaveBalanceView,
  LeaveTypeView,
  TimeLeaveApiService,
} from '@bill-book/time-leave-core';

@Component({
  standalone: true,
  selector: 'bb-leave-applications',
  imports: [CommonModule, FormsModule],
  templateUrl: './leave-applications.page.html',
  styleUrl: '../time-leave-page.scss',
})
export class LeaveApplicationsPage implements OnInit {
  private readonly api = inject(TimeLeaveApiService);

  readonly loading = signal(false);
  readonly showForm = signal(false);
  readonly leaveTypes = signal<LeaveTypeView[]>([]);
  readonly balances = signal<LeaveBalanceView[]>([]);
  readonly applications = signal<LeaveApplicationView[]>([]);

  form: ApplyLeave = {
    employeeId: 1,
    leaveTypeId: 1,
    fromDate: new Date().toISOString().substring(0, 10),
    toDate: new Date().toISOString().substring(0, 10),
    fromHalf: 'Full',
    toHalf: 'Full',
    reason: '',
  };

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const types = await this.api.leaveTypes();
      this.leaveTypes.set(types);
      if (types.length > 0) {
        this.form.leaveTypeId = types[0].leaveTypeId;
      }
      const year = new Date().getFullYear();
      const bal = await this.api.leaveBalances(1, year);
      this.balances.set(bal);
    } catch {
      // Offline / not configured yet
    } finally {
      this.loading.set(false);
    }
  }

  openApplyModal(): void {
    this.showForm.set(true);
  }

  closeModal(): void {
    this.showForm.set(false);
  }

  async submitApplication(): Promise<void> {
    try {
      const app = await this.api.applyLeave(this.form);
      this.applications.update((list) => [app, ...list]);
      this.closeModal();
      const year = new Date().getFullYear();
      const bal = await this.api.leaveBalances(1, year);
      this.balances.set(bal);
    } catch (e: any) {
      alert(e?.message || 'Error submitting leave application');
    }
  }

  async approve(app: LeaveApplicationView): Promise<void> {
    try {
      const res = await this.api.approveLeave(app.leaveApplicationId);
      this.applications.update((list) =>
        list.map((item) => (item.leaveApplicationId === res.leaveApplicationId ? res : item)),
      );
      const year = new Date().getFullYear();
      const bal = await this.api.leaveBalances(1, year);
      this.balances.set(bal);
    } catch (e: any) {
      alert(e?.message || 'Error approving application');
    }
  }
}
