import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { PayrollApiService, PayrollRunView } from '@bill-book/payroll-core';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  MonthInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-payroll-runs-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MessageBoxComponent,
    MonthInputComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
  ],
  templateUrl: './payroll-runs.list.html',
  styleUrl: '../payroll-page.scss',
})
export class PayrollRunsList implements OnInit {
  protected readonly runsColumns: ColumnDef[] = [
    { field: 'month', header: 'Month' },
    { field: 'status', header: 'Status' },
    { field: 'employeeCount', header: 'Employees' },
    { field: 'totalGrossEarnings', header: 'Gross Earnings', numeric: true },
    { field: 'totalGrossDeductions', header: 'Deductions', numeric: true },
    { field: 'totalNetPay', header: 'Net Pay', numeric: true },
    { field: 'daysSource', header: 'Source' },
    { field: 'payrollRunId', header: 'Actions', sortable: false, filterable: false },
  ];

  private readonly api = inject(PayrollApiService);

  protected readonly runs = signal<PayrollRunView[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected selectedMonth = new Date().toISOString().slice(0, 7);

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.busy.set(true);
      const res = await this.api.runs();
      this.runs.set(res);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Could not load payroll runs.' }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected async processRun(): Promise<void> {
    if (!this.selectedMonth) return;

    try {
      this.busy.set(true);
      const dateStr = `${this.selectedMonth}-01`;
      await this.api.processRun(dateStr);
      await this.load();
      this.messages.set([{ tone: 'info', text: `Pay run for ${this.selectedMonth} processed.` }]);
    } catch {
      this.messages.set([{ tone: 'error', text: 'Failed to process pay run.' }]);
    } finally {
      this.busy.set(false);
    }
  }
}
