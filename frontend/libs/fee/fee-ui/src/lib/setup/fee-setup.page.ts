import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import {
  Concession,
  ConcessionKind,
  FREQUENCY_LABELS,
  FeeApiService,
  FeeFrequency,
  FeeHead,
  FeeStructure,
  SaveConcession,
  SaveFeeStructure,
} from '@bill-book/fee-core';
import { SisApiService } from '@bill-book/sis-core';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  MessageBoxComponent,
  MoneyInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

type Tab = 'heads' | 'structures' | 'concessions';

/**
 * Fees › Fee setup (S4, TK-64): fee heads and the accounts they post to, the
 * structure each class pays in a year, and concessions for particular students.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-fee-setup-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    NumberInputComponent,
    MoneyInputComponent,
    DateInputComponent,
    SelectComponent,
    CheckboxComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './fee-setup.page.html',
  styleUrl: '../fee-page.scss',
})
export class FeeSetupPage implements OnInit {
  private readonly api = inject(FeeApiService);
  private readonly sis = inject(SisApiService);

  protected readonly tabs: readonly { tab: Tab; label: string }[] = [
    { tab: 'heads', label: 'Fee heads' },
    { tab: 'structures', label: 'Structures' },
    { tab: 'concessions', label: 'Concessions' },
  ];

  protected readonly tab = signal<Tab>('heads');
  protected readonly heads = signal<FeeHead[]>([]);
  protected readonly structures = signal<FeeStructure[]>([]);
  protected readonly concessions = signal<Concession[]>([]);
  protected readonly accountOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly yearOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly classOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly studentOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected headForm: Omit<FeeHead, 'feeHeadId'> = FeeSetupPage.blankHead();
  protected structureForm: SaveFeeStructure = FeeSetupPage.blankStructure();
  protected concessionForm: SaveConcession = FeeSetupPage.blankConcession();

  protected readonly frequencies: BbSelectOption<FeeFrequency>[] = (Object.keys(FREQUENCY_LABELS) as FeeFrequency[]).map((f) => ({
    value: f,
    label: FREQUENCY_LABELS[f],
  }));

  protected readonly kinds: BbSelectOption<ConcessionKind>[] = [
    { value: 'Percent', label: 'Percentage' },
    { value: 'Amount', label: 'Fixed amount' },
  ];

  protected readonly months: BbSelectOption<number>[] = [
    'January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December',
  ].map((label, i) => ({ value: i + 1, label }));

  protected readonly headColumns: ColumnDef[] = [
    { field: 'code', header: 'Code' },
    { field: 'name', header: 'Name' },
    { field: 'incomeAccountId', header: 'Posts to' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' },
  ];
  protected readonly structureColumns: ColumnDef[] = [
    { field: 'schoolClassId', header: 'Class' },
    { field: 'name', header: 'Structure' },
    { field: 'lines', header: 'Fees' },
    { field: 'actions', header: '' },
  ];
  protected readonly concessionColumns: ColumnDef[] = [
    { field: 'studentId', header: 'Student' },
    { field: 'feeHeadId', header: 'Fee' },
    { field: 'value', header: 'Concession' },
    { field: 'validTo', header: 'Until' },
    { field: 'isApproved', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.load();
  }

  protected headOptions(): BbSelectOption<number>[] {
    return this.heads().filter((h) => h.isActive).map((h) => ({ value: h.feeHeadId, label: h.name }));
  }

  protected headName(id: number): string {
    return this.heads().find((h) => h.feeHeadId === id)?.name ?? '—';
  }

  protected className(id: number): string {
    return this.classOptions().find((c) => c.value === id)?.label ?? '—';
  }

  protected studentName(id: number): string {
    return this.studentOptions().find((s) => s.value === id)?.label ?? String(id);
  }

  protected postsTo(head: FeeHead): string {
    if (head.incomeAccountId) {
      return this.accountOptions().find((a) => a.value === head.incomeAccountId)?.label ?? 'Chosen account';
    }

    return head.isRefundable ? 'Refundable Deposits' : 'Fee Income';
  }

  protected feesOf(structure: FeeStructure): string {
    return structure.lines.map((l) => `${this.headName(l.feeHeadId)} ${l.amount} ${FREQUENCY_LABELS[l.frequency].toLowerCase()}`).join(', ');
  }

  protected choose(tab: Tab): void {
    this.tab.set(tab);
    this.editingId.set(undefined);
  }

  protected startAdd(): void {
    this.headForm = FeeSetupPage.blankHead();
    this.structureForm = FeeSetupPage.blankStructure();
    this.structureForm.academicYearId = this.yearOptions()[0]?.value ?? 0;
    this.concessionForm = FeeSetupPage.blankConcession();
    this.editingId.set(null);
  }

  protected editHead(row: FeeHead): void {
    this.headForm = { ...row };
    this.editingId.set(row.feeHeadId);
  }

  protected editStructure(row: FeeStructure): void {
    this.structureForm = { ...row, lines: row.lines.map((l) => ({ ...l })) };
    this.editingId.set(row.feeStructureId);
  }

  protected editConcession(row: Concession): void {
    this.concessionForm = { ...row };
    this.editingId.set(row.feeConcessionId);
  }

  protected addLine(): void {
    this.structureForm.lines = [...this.structureForm.lines, { feeHeadId: 0, amount: 0, frequency: 'Monthly', dueDay: 10 }];
  }

  protected removeLine(index: number): void {
    this.structureForm.lines = this.structureForm.lines.filter((_, i) => i !== index);
  }

  protected cancel(): void {
    this.editingId.set(undefined);
  }

  protected async save(): Promise<void> {
    const id = this.editingId() ?? null;
    this.busy.set(true);
    try {
      switch (this.tab()) {
        case 'heads':
          await this.api.saveHead(id, this.headForm);
          break;
        case 'structures':
          await this.api.saveStructure(id, this.structureForm);
          break;
        default:
          await this.api.saveConcession(id, this.concessionForm);
      }

      this.messages.set([{ tone: 'success', text: 'Saved.' }]);
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
      const [heads, structures, concessions, accounts, years, classes, students] = await Promise.all([
        this.api.heads(),
        this.api.structures(),
        this.api.concessions(),
        this.api.postableAccounts(),
        this.sis.years(),
        this.sis.classes(),
        this.sis.students({ status: 'Active' }),
      ]);
      this.heads.set(heads);
      this.structures.set(structures);
      this.concessions.set(concessions);
      this.accountOptions.set(accounts.map((a) => ({ value: a.accountId, label: `${a.accountCode} ${a.accountName}` })));
      this.yearOptions.set([...years].sort((a, b) => Number(b.isCurrent) - Number(a.isCurrent)).map((y) => ({ value: y.academicYearId, label: y.code })));
      this.classOptions.set(classes.map((c) => ({ value: c.schoolClassId, label: c.name })));
      this.studentOptions.set(students.map((s) => ({ value: s.studentId, label: `${s.fullName} (${s.admissionNo})` })));
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

  private static blankHead(): Omit<FeeHead, 'feeHeadId'> {
    return { code: '', name: '', incomeAccountId: null, isRefundable: false, hsnSacCode: null, isActive: true };
  }

  private static blankStructure(): SaveFeeStructure {
    return { academicYearId: 0, schoolClassId: 0, name: 'Day scholar', firstMonth: 6, isActive: true, lines: [] };
  }

  private static blankConcession(): SaveConcession {
    const today = new Date().toISOString().slice(0, 10);
    return { studentId: 0, feeHeadId: 0, concessionKind: 'Percent', value: 0, reason: '', validFrom: today, validTo: today, isApproved: false };
  }
}
