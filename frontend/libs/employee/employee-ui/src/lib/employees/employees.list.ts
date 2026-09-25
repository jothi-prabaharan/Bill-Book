import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { EmployeeListItem, EmployeeStatus, EmployeeApiService } from '@bill-book/employee-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * People › Employees (H1, TK-48): the shared employee master, listed. PAN and
 * Aadhaar arrive masked from the server whoever asks; the full numbers are on
 * the employee's own page, to those allowed to see them.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-employee-employees-list',
  standalone: true,
  imports: [FormsModule, DataGridComponent, DataGridCellTemplateDirective, TextInputComponent, SelectComponent, MessageBoxComponent, IfCanDirective],
  templateUrl: './employees.list.html',
  styleUrl: '../employee-page.scss',
})
export class EmployeesList implements OnInit {
  private readonly api = inject(EmployeeApiService);
  private readonly router = inject(Router);

  protected readonly rows = signal<EmployeeListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  protected search = '';
  protected status: EmployeeStatus | null = 'Active';
  protected page = 1;
  protected readonly pageSize = 50;

  protected readonly statusOptions: BbSelectOption<EmployeeStatus>[] = [
    { value: 'Onboarding', label: 'Onboarding' },
    { value: 'Active', label: 'Active' },
    { value: 'OnNotice', label: 'On notice' },
    { value: 'Exited', label: 'Exited' },
  ];

  protected readonly columns: ColumnDef[] = [
    { field: 'employeeCode', header: 'Code' },
    { field: 'fullName', header: 'Name' },
    { field: 'departmentName', header: 'Department' },
    { field: 'designationName', header: 'Designation' },
    { field: 'workLocationName', header: 'Location' },
    { field: 'maskedPan', header: 'PAN' },
    { field: 'employeeStatus', header: 'Status' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.busy.set(true);
    try {
      const result = await this.api.employees({ search: this.search.trim(), status: this.status, page: this.page, pageSize: this.pageSize });
      this.rows.set(result.items);
      this.total.set(result.total);
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.busy.set(false);
    }
  }

  protected open(row: EmployeeListItem): void {
    void this.router.navigate(['/hrm/employees', row.employeeId]);
  }

  protected add(): void {
    void this.router.navigate(['/hrm/employees/new']);
  }
}
