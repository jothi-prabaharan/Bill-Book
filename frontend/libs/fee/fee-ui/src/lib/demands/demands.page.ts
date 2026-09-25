import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { FeeApiService, FeeDemand, FeeStructure, periodOf } from '@bill-book/fee-core';
import { SisApiService } from '@bill-book/sis-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  MessageBoxComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * Fees › Fee demands (S4, TK-64): raise a period's demands for a class's fee
 * structure, then post them. Raising a period again raises nothing twice.
 * A posted demand is voided, never edited, and only while nothing is paid.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-fee-demands-page',
  standalone: true,
  imports: [FormsModule, DataGridComponent, DataGridCellTemplateDirective, SelectComponent, DateInputComponent, TextInputComponent, MessageBoxComponent, IfCanDirective],
  templateUrl: './demands.page.html',
  styleUrl: '../fee-page.scss',
})
export class DemandsPage implements OnInit {
  private readonly api = inject(FeeApiService);
  private readonly sis = inject(SisApiService);

  protected readonly structures = signal<FeeStructure[]>([]);
  protected readonly classNames = signal<Map<number, string>>(new Map());
  protected readonly rows = signal<FeeDemand[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly voiding = signal<FeeDemand | null>(null);

  protected structureId: number | null = null;
  protected demandDate = new Date().toISOString().slice(0, 10);
  protected period = periodOf(this.demandDate);
  protected voidReason = '';

  protected readonly structureOptions = computed<BbSelectOption<number>[]>(() =>
    this.structures().filter((s) => s.isActive).map((s) => ({ value: s.feeStructureId, label: `${this.classNames().get(s.schoolClassId) ?? ''} · ${s.name}` })),
  );

  protected readonly drafts = computed(() => this.rows().filter((d) => d.documentStatus === 'Draft').length);

  protected readonly columns: ColumnDef[] = [
    { field: 'demandNo', header: 'Demand' },
    { field: 'studentId', header: 'Student' },
    { field: 'dueDate', header: 'Due' },
    { field: 'netAmount', header: 'Net' },
    { field: 'openAmount', header: 'Open' },
    { field: 'documentStatus', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected async load(): Promise<void> {
    if (!this.structureId) {
      this.rows.set([]);
      return;
    }

    try {
      this.rows.set(await this.api.demands({ feeStructureId: this.structureId, periodKey: this.period }));
    } catch (error) {
      this.fail(error);
    }
  }

  protected async generate(): Promise<void> {
    if (!this.structureId || !/^\d{4}-\d{2}$/.test(this.period)) {
      this.messages.set([{ tone: 'error', text: 'Choose a structure and a period, like 2026-06.' }]);
      return;
    }

    this.busy.set(true);
    try {
      const result = await this.api.generate(this.structureId, this.period, this.demandDate);
      this.messages.set([
        { tone: 'success', text: `${result.created} raised, ${result.alreadyRaised} already raised, ${result.skipped} skipped.` },
        ...result.notes.map((n) => ({ tone: 'warning' as const, text: n })),
      ]);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected async postAll(): Promise<void> {
    if (!this.structureId) {
      return;
    }

    this.busy.set(true);
    try {
      const result = await this.api.post({ feeStructureId: this.structureId, periodKey: this.period });
      this.messages.set([{ tone: 'success', text: `${result.posted} demands posted.` }]);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected startVoid(row: FeeDemand): void {
    this.voiding.set(row);
    this.voidReason = '';
  }

  protected async confirmVoid(): Promise<void> {
    const row = this.voiding();
    if (!row || !this.voidReason.trim()) {
      this.messages.set([{ tone: 'error', text: 'Give a reason to void.' }]);
      return;
    }

    this.busy.set(true);
    try {
      await this.api.voidDemand(row.feeDemandId, this.voidReason);
      this.voiding.set(null);
      this.messages.set([{ tone: 'success', text: 'The demand is void.' }]);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    try {
      const [structures, classes] = await Promise.all([this.api.structures(), this.sis.classes()]);
      this.structures.set(structures);
      this.classNames.set(new Map(classes.map((c) => [c.schoolClassId, c.name])));
    } catch (error) {
      this.fail(error);
    }
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }
}
