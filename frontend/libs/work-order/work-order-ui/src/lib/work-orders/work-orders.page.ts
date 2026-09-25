import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { FacilityApiService } from '@bill-book/facility-core';
import { EmployeeApiService } from '@bill-book/employee-core';
import {
  BbSelectOption,
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
import {
  SaveWorkOrder,
  WORK_ORDER_PRIORITIES,
  WORK_ORDER_SOURCES,
  WORK_ORDER_STATUSES,
  WorkOrder,
  WorkOrderAction,
  WorkOrderApiService,
  WorkOrderStatus,
  isEditable,
  nextActions,
  takesParts,
} from '@bill-book/work-order-core';

const ACTION_LABELS: Record<WorkOrderAction, string> = {
  Assign: 'Assign',
  Start: 'Start',
  Hold: 'Put on hold',
  Resume: 'Resume',
  Complete: 'Complete',
  Close: 'Close',
  Cancel: 'Cancel work order',
};

/**
 * Maintenance › Work orders (S6, TK-66): what needs fixing and where, who is
 * on it, the checklist, and the parts drawn from the store. Only an Open work
 * order can be edited; closing a completed one is a separate permission.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-work-orders-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    DateInputComponent,
    MoneyInputComponent,
    NumberInputComponent,
    SelectComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './work-orders.page.html',
  styleUrl: '../work-order-page.scss',
})
export class WorkOrdersPage implements OnInit {
  private readonly api = inject(WorkOrderApiService);
  private readonly facility = inject(FacilityApiService);
  private readonly employeeApi = inject(EmployeeApiService);

  protected readonly rows = signal<WorkOrder[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);
  protected readonly open = signal<WorkOrder | null>(null);

  protected readonly assetOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly spaceOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly employeeOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly itemOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly warehouseOptions = signal<BbSelectOption<number>[]>([]);

  protected readonly actions = computed(() => (this.open() ? nextActions(this.open()!.workOrderStatus) : []));
  protected readonly editable = computed(() => !!this.open() && isEditable(this.open()!.workOrderStatus));
  protected readonly partsOpen = computed(() => !!this.open() && takesParts(this.open()!.workOrderStatus));

  protected status: WorkOrderStatus | null = null;
  protected form: SaveWorkOrder = WorkOrdersPage.blank();
  protected tasksText = '';

  // The move being made, and what it needs.
  protected pending: WorkOrderAction | null = null;
  protected employeeId: number | null = null;
  protected completedDate: string | null = null;
  protected labourCost: number | null = null;
  protected reason = '';

  protected part = { itemId: null as number | null, warehouseId: null as number | null, quantity: 1, issueDate: WorkOrdersPage.today() };

  protected readonly statuses = [...WORK_ORDER_STATUSES];
  protected readonly priorities = [...WORK_ORDER_PRIORITIES];
  protected readonly sources = [...WORK_ORDER_SOURCES];

  protected readonly columns: ColumnDef[] = [
    { field: 'workOrderNo', header: 'No.' },
    { field: 'title', header: 'Work' },
    { field: 'where', header: 'Where' },
    { field: 'priority', header: 'Priority' },
    { field: 'reportedDate', header: 'Reported' },
    { field: 'workOrderStatus', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected label(status: WorkOrderStatus): string {
    return WORK_ORDER_STATUSES.find((s) => s.value === status)?.label ?? status;
  }

  protected actionLabel(action: WorkOrderAction): string {
    return ACTION_LABELS[action];
  }

  protected where(row: WorkOrder): string {
    const asset = this.assetOptions().find((o) => o.value === row.facilityAssetId)?.label;
    const space = this.spaceOptions().find((o) => o.value === row.spaceId)?.label;
    return [asset, space].filter(Boolean).join(' · ') || '—';
  }

  protected employee(id: number | null): string {
    return id === null ? 'Nobody yet' : (this.employeeOptions().find((o) => o.value === id)?.label ?? `Employee ${id}`);
  }

  protected startAdd(): void {
    this.form = WorkOrdersPage.blank();
    this.tasksText = '';
    this.open.set(null);
    this.editingId.set(null);
  }

  protected edit(row: WorkOrder): void {
    this.form = {
      title: row.title, description: row.description, workOrderSource: row.workOrderSource, priority: row.priority,
      facilityAssetId: row.facilityAssetId, spaceId: row.spaceId, reportedDate: row.reportedDate, dueDate: row.dueDate, tasks: [],
    };
    this.tasksText = row.tasks.map((t) => t.description).join('\n');
    this.editingId.set(row.workOrderId);
  }

  protected async view(row: WorkOrder): Promise<void> {
    this.editingId.set(undefined);
    this.pending = null;
    try {
      this.open.set(await this.api.get(row.workOrderId));
    } catch (error) {
      this.fail(error);
    }
  }

  protected async load(): Promise<void> {
    try {
      this.rows.set(await this.api.list(this.status));
    } catch (error) {
      this.fail(error);
    }
  }

  protected async save(): Promise<void> {
    await this.run('The work order is saved.', async () => {
      const tasks = this.tasksText.split('\n').map((t) => t.trim()).filter((t) => t.length > 0);
      await this.api.save(this.editingId() ?? null, { ...this.form, tasks });
      this.editingId.set(undefined);
    });
  }

  protected choose(action: WorkOrderAction): void {
    this.pending = action;
    this.employeeId = this.open()?.assignedEmployeeId ?? null;
    this.completedDate = WorkOrdersPage.today();
    this.labourCost = null;
    this.reason = '';
    if (action === 'Start' || action === 'Hold' || action === 'Resume') {
      void this.act();
    }
  }

  protected async act(): Promise<void> {
    const order = this.open();
    if (!order || !this.pending) return;
    const action = this.pending;
    await this.run('The work order is updated.', async () => {
      await this.api.act(order.workOrderId, {
        action, employeeId: this.employeeId, completedDate: this.completedDate, labourCost: this.labourCost, reason: this.reason || null,
      });
      this.pending = null;
    });
  }

  protected async close(): Promise<void> {
    const order = this.open();
    if (!order) return;
    await this.run('The work order is closed.', () => this.api.close(order.workOrderId));
  }

  protected async tick(taskId: number, isDone: boolean): Promise<void> {
    const order = this.open();
    if (!order) return;
    await this.run(null, () => this.api.tick(order.workOrderId, taskId, isDone));
  }

  protected async issue(): Promise<void> {
    const order = this.open();
    if (!order || this.part.itemId === null) return;
    const body = { itemId: this.part.itemId, warehouseId: this.part.warehouseId, quantity: this.part.quantity, issueDate: this.part.issueDate };
    await this.run('The part is issued from stock.', async () => {
      await this.api.issuePart(order.workOrderId, body);
      this.part = { ...this.part, itemId: null, quantity: 1 };
    });
  }

  private async run(success: string | null, work: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      if (success) this.messages.set([{ tone: 'success', text: success }]);
      const order = this.open();
      if (order) this.open.set(await this.api.get(order.workOrderId));
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    await this.load();

    // Pickers load on their own, so one service being down leaves the rest usable.
    await Promise.allSettled([
      this.facility.assets({}).then((assets) =>
        this.assetOptions.set(assets.filter((a) => a.assetStatus !== 'Disposed').map((a) => ({ value: a.facilityAssetId, label: `${a.assetTag} ${a.name}` })))),
      this.facility.spaces().then((spaces) =>
        this.spaceOptions.set(spaces.filter((s) => s.isActive).map((s) => ({ value: s.spaceId, label: `${s.code} ${s.name}`, group: s.buildingName })))),
      this.employeeApi.employees({ status: 'Active', page: 1, pageSize: 500 }).then((page) =>
        this.employeeOptions.set(page.items.map((e) => ({ value: e.employeeId, label: `${e.fullName} (${e.employeeCode})` })))),
      this.api.stockItems().then((items) =>
        this.itemOptions.set(items.map((i) => ({ value: i.itemId, label: `${i.itemCode} ${i.itemName} · ${i.quantityOnHand} in stock` })))),
      this.api.warehouses().then((warehouses) =>
        this.warehouseOptions.set(warehouses.map((w) => ({ value: w.warehouseId, label: w.warehouseName })))),
    ]);
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private static today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private static blank(): SaveWorkOrder {
    return {
      title: '', description: null, workOrderSource: 'Complaint', priority: 'Medium',
      facilityAssetId: null, spaceId: null, reportedDate: WorkOrdersPage.today(), dueDate: null, tasks: [],
    };
  }
}
