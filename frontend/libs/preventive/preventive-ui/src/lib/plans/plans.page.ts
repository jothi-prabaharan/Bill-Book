import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { FacilityApiService } from '@bill-book/facility-core';
import { EmployeeApiService } from '@bill-book/employee-core';
import {
  FREQUENCIES,
  OCCURRENCE_STATUSES,
  Occurrence,
  OccurrenceStatus,
  PreventiveApiService,
  PreventivePlan,
  SavePlan,
  everyLabel,
} from '@bill-book/preventive-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

type Tab = 'plans' | 'occurrences';

/**
 * Maintenance › Preventive plans (S7, TK-67): recurring jobs for an asset or a
 * space, and the occurrences they generate. Each occurrence raises one work
 * order, lead days before it falls due, hourly or when **Generate now** is
 * pressed; pressing it twice raises nothing twice.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-preventive-plans-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    DateInputComponent,
    NumberInputComponent,
    SelectComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './plans.page.html',
  styleUrl: '../preventive-page.scss',
})
export class PlansPage implements OnInit {
  private readonly api = inject(PreventiveApiService);
  private readonly facility = inject(FacilityApiService);
  private readonly employeeApi = inject(EmployeeApiService);

  protected readonly tab = signal<Tab>('plans');
  protected readonly plans = signal<PreventivePlan[]>([]);
  protected readonly occurrences = signal<Occurrence[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);

  protected readonly assetOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly spaceOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly employeeOptions = signal<BbSelectOption<number>[]>([]);

  protected form: SavePlan = PlansPage.blank();
  protected occurrenceStatus: OccurrenceStatus | null = null;

  protected readonly frequencies = FREQUENCIES.map((f) => ({ value: f.value, label: f.label }));
  protected readonly statuses = [...OCCURRENCE_STATUSES];

  protected readonly planColumns: ColumnDef[] = [
    { field: 'name', header: 'Plan' },
    { field: 'where', header: 'For' },
    { field: 'frequency', header: 'Recurs' },
    { field: 'nextDueDate', header: 'Next due' },
    { field: 'isActive', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  protected readonly occurrenceColumns: ColumnDef[] = [
    { field: 'dueDate', header: 'Due' },
    { field: 'planName', header: 'Plan' },
    { field: 'workOrderNo', header: 'Work order' },
    { field: 'occurrenceStatus', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected every(plan: PreventivePlan): string {
    return everyLabel(plan.frequency, plan.interval);
  }

  protected where(plan: PreventivePlan): string {
    const asset = this.assetOptions().find((o) => o.value === plan.facilityAssetId)?.label;
    const space = this.spaceOptions().find((o) => o.value === plan.spaceId)?.label;
    return [asset, space].filter(Boolean).join(' · ') || '—';
  }

  protected statusLabel(status: OccurrenceStatus): string {
    return OCCURRENCE_STATUSES.find((s) => s.value === status)?.label ?? status;
  }

  protected show(tab: Tab): void {
    this.tab.set(tab);
    if (tab === 'occurrences') void this.loadOccurrences();
  }

  protected startAdd(): void {
    this.form = PlansPage.blank();
    this.editingId.set(null);
  }

  protected edit(plan: PreventivePlan): void {
    const { preventivePlanId, nextDueDate, ...rest } = plan;
    void nextDueDate;
    this.form = { ...rest };
    this.editingId.set(preventivePlanId);
  }

  protected async save(): Promise<void> {
    await this.run('The plan is saved.', async () => {
      await this.api.savePlan(this.editingId() ?? null, this.form);
      this.editingId.set(undefined);
    });
  }

  protected async generate(): Promise<void> {
    await this.run(null, async () => {
      const result = await this.api.generate();
      this.messages.set([{
        tone: result.failed > 0 ? 'warning' : 'success',
        text: `${result.generated} occurrence(s) generated and ${result.raised} work order(s) raised.`
          + (result.failed > 0 ? ` ${result.failed} could not be raised and will be retried.` : ''),
      }]);
    });
  }

  protected async setOccurrence(row: Occurrence, status: OccurrenceStatus): Promise<void> {
    await this.run(status === 'Skipped' ? 'The occurrence is skipped.' : 'The occurrence is marked done.',
      () => this.api.setOccurrence(row.preventiveOccurrenceId, status));
  }

  protected async loadOccurrences(): Promise<void> {
    try {
      this.occurrences.set(await this.api.occurrences({ status: this.occurrenceStatus }));
    } catch (error) {
      this.fail(error);
    }
  }

  private async loadPlans(): Promise<void> {
    try {
      this.plans.set(await this.api.plans());
    } catch (error) {
      this.fail(error);
    }
  }

  private async run(success: string | null, work: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      if (success) this.messages.set([{ tone: 'success', text: success }]);
      await this.loadPlans();
      if (this.tab() === 'occurrences') await this.loadOccurrences();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    await this.loadPlans();
    await Promise.allSettled([
      this.facility.assets({}).then((assets) =>
        this.assetOptions.set(assets.filter((a) => a.assetStatus !== 'Disposed').map((a) => ({ value: a.facilityAssetId, label: `${a.assetTag} ${a.name}` })))),
      this.facility.spaces().then((spaces) =>
        this.spaceOptions.set(spaces.filter((s) => s.isActive).map((s) => ({ value: s.spaceId, label: `${s.code} ${s.name}`, group: s.buildingName })))),
      this.employeeApi.employees({ status: 'Active', page: 1, pageSize: 500 }).then((page) =>
        this.employeeOptions.set(page.items.map((e) => ({ value: e.employeeId, label: `${e.fullName} (${e.employeeCode})` })))),
    ]);
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private static blank(): SavePlan {
    return {
      name: '', facilityAssetId: null, spaceId: null, frequency: 'Monthly', interval: 1,
      startDate: new Date().toISOString().slice(0, 10), endDate: null, leadDays: 0, defaultAssigneeEmployeeId: null, isActive: true,
    };
  }
}
