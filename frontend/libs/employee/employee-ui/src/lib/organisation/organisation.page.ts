import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { EmployeeApiService, OrgMasterKind, OrgMasterRow } from '@bill-book/employee-core';
import {
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  NumberInputComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

const KINDS: readonly { kind: OrgMasterKind; label: string; singular: string }[] = [
  { kind: 'departments', label: 'Departments', singular: 'department' },
  { kind: 'designations', label: 'Designations', singular: 'designation' },
  { kind: 'grades', label: 'Grades', singular: 'grade' },
  { kind: 'cost-centres', label: 'Cost centres', singular: 'cost centre' },
  { kind: 'work-locations', label: 'Work locations', singular: 'work location' },
];

/**
 * People › Organisation setup (H1, TK-48): the five masters every employee
 * names. Shared by HRMS, Payroll and School. Rows are deactivated, never
 * deleted, because employees and their history name them.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-employee-organisation-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    NumberInputComponent,
    CheckboxComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './organisation.page.html',
  styleUrl: '../employee-page.scss',
})
export class OrganisationPage implements OnInit {
  private readonly api = inject(EmployeeApiService);

  protected readonly kinds = KINDS;
  protected readonly kind = signal<OrgMasterKind>('departments');
  protected readonly current = computed(() => KINDS.find((k) => k.kind === this.kind()) ?? KINDS[0]);
  protected readonly rows = signal<OrgMasterRow[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected form: Partial<OrgMasterRow> = {};

  protected readonly columns: ColumnDef[] = [
    { field: 'code', header: 'Code' },
    { field: 'name', header: 'Name' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  protected choose(kind: OrgMasterKind): void {
    this.kind.set(kind);
    this.editingId.set(undefined);
    void this.load();
  }

  protected startAdd(): void {
    this.form = { code: '', name: '', isActive: true, noticePeriodDays: 30, sortOrder: 0 };
    this.editingId.set(null);
  }

  protected startEdit(row: OrgMasterRow): void {
    this.form = { ...row };
    this.editingId.set(row.id);
  }

  protected cancel(): void {
    this.editingId.set(undefined);
  }

  protected async save(): Promise<void> {
    if (!this.form.code?.trim() || !this.form.name?.trim()) {
      this.messages.set([{ tone: 'error', text: 'Give a code and a name.' }]);
      return;
    }

    this.busy.set(true);
    try {
      await this.api.saveOrganisation(this.kind(), this.editingId() ?? null, this.form);
      this.messages.set([{ tone: 'success', text: `The ${this.current().singular} is saved.` }]);
      this.editingId.set(undefined);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.api.organisation(this.kind()));
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }
}
