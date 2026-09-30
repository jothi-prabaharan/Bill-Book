import { ChangeDetectionStrategy, Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FormatSettingsService } from '@bill-book/currency-format';
import { DataGridComponent } from '../data-grid/data-grid.component';
import { DataGridCellTemplateDirective } from '../data-grid/data-grid-cell-template.directive';
import { ColumnDef } from '../data-grid/data-grid.models';
import { DateInputComponent } from '../date-input/date-input.component';
import { SelectComponent } from '../forms/select/select.component';
import { BbSelectOption } from '../forms/field.model';
import { MessageBoxComponent } from '../message-box/message-box.component';
import {
  EditChangeRow,
  EditLogArea,
  EditLogEntry,
  EditLogPage,
  EditLogUser,
  changeRows,
  changeSummary,
  editLogBase,
  editLogQuery,
  humanize,
  operationLabel,
  pageCount,
} from './edit-history.model';

/**
 * The audit-log report (TK-118): every change one service's edit log holds,
 * newest first, filtered by date, user and table. Each app mounts it with the
 * areas it sells — RetailErp's sales, purchase and ledger; HRMS's employees and
 * leave — because each service keeps its own log in its own schema.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-edit-log-report',
  standalone: true,
  imports: [FormsModule, DataGridComponent, DataGridCellTemplateDirective, DateInputComponent, SelectComponent, MessageBoxComponent],
  templateUrl: './edit-log-report.component.html',
  styleUrl: './edit-log-report.component.scss',
})
export class EditLogReportComponent implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  /** The services whose logs this app may read, first one shown first. */
  @Input({ required: true }) areas: EditLogArea[] = [];

  protected area = '';
  protected from: string | null = null;
  protected to: string | null = null;
  protected userId: string | null = null;
  protected entity: string | null = null;
  protected page = 1;
  protected readonly pageSize = 50;

  protected readonly rows = signal<EditLogEntry[]>([]);
  protected readonly total = signal(0);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly selected = signal<EditLogEntry | null>(null);
  protected readonly userOptions = signal<BbSelectOption<string>[]>([]);
  protected readonly entityOptions = signal<BbSelectOption<string>[]>([]);

  protected readonly columns: ColumnDef[] = [
    { field: 'changedAt', header: 'When', isTemplate: true, width: '10rem' },
    { field: 'userName', header: 'User', width: '10rem' },
    { field: 'entityName', header: 'Table', isTemplate: true, width: '10rem' },
    { field: 'entityKey', header: 'Id', width: '6rem' },
    { field: 'operation', header: 'Action', isTemplate: true, width: '6rem' },
    { field: 'summary', header: 'Changes', isTemplate: true, sortable: false, filterable: false },
  ];

  protected readonly humanize = humanize;
  protected readonly operationLabel = operationLabel;
  protected readonly changeSummary = changeSummary;

  protected get areaOptions(): BbSelectOption<string>[] {
    return this.areas.map((a) => ({ value: a.service, label: a.label }));
  }

  protected get pages(): number {
    return pageCount(this.total(), this.pageSize);
  }

  ngOnInit(): void {
    this.area = this.areas[0]?.service ?? '';
    void this.changeArea();
  }

  protected async changeArea(): Promise<void> {
    this.userId = null;
    this.entity = null;
    this.userOptions.set([]);
    this.entityOptions.set([]);
    if (!this.area) return;

    const base = editLogBase(this.area);
    try {
      const [users, entities] = await Promise.all([
        firstValueFrom(this.http.get<EditLogUser[]>(`${base}/users`)),
        firstValueFrom(this.http.get<string[]>(`${base}/entities`)),
      ]);
      this.userOptions.set(users.map((u) => ({ value: u.userId, label: u.userName || u.userId })));
      this.entityOptions.set(entities.map((e) => ({ value: e, label: humanize(e) })));
    } catch {
      // The filters stay empty; the search below reports the failure.
    }

    await this.search(1);
  }

  protected async search(page = 1): Promise<void> {
    if (!this.area) return;
    this.page = page;
    this.loading.set(true);
    this.error.set(null);
    this.selected.set(null);
    try {
      const result = await firstValueFrom(
        this.http.get<EditLogPage>(editLogBase(this.area), {
          params: editLogQuery({
            from: this.from,
            to: this.to,
            userId: this.userId,
            entity: this.entity,
            page: this.page,
            pageSize: this.pageSize,
          }),
        }),
      );
      this.rows.set(result.rows);
      this.total.set(result.total);
    } catch {
      this.rows.set([]);
      this.total.set(0);
      this.error.set('The audit log could not be loaded. Try again in a moment.');
    } finally {
      this.loading.set(false);
    }
  }

  protected select(row: EditLogEntry): void {
    this.selected.set(this.selected()?.editLogId === row.editLogId ? null : row);
  }

  protected detail(entry: EditLogEntry): EditChangeRow[] {
    return changeRows(entry);
  }

  protected when(value: string): string {
    const time = new Date(value);
    return `${this.formats.formatDate(value)} ${String(time.getHours()).padStart(2, '0')}:${String(time.getMinutes()).padStart(2, '0')}`;
  }
}
