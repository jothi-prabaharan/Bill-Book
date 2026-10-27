import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  BatchTrackingBatchChoice,
  BatchTrackingContactChoice,
  BatchTrackingDetailGroupDto,
  BatchTrackingDetailReportRequest,
  BatchTrackingDetailReportResultDto,
  BatchTrackingDetailReportService,
  BatchTrackingItemChoice,
  BatchTrackingWarehouseChoice,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'batchId' | 'itemId' | 'warehouseId' | 'contactId';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-batch-tracking-detail-report-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportDatePipe,
  ],
  templateUrl: './batch-tracking-detail-report.page.html',
  styleUrl: './batch-tracking-detail-report.page.scss',
})
export class BatchTrackingDetailReportPageComponent implements OnInit {
  private readonly service = inject(BatchTrackingDetailReportService);

  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    batchId: new FormControl<number | null>(null),
    itemId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    contactId: new FormControl<number | null>(null),
  });

  protected readonly batches = signal<BatchTrackingBatchChoice[]>([]);
  protected readonly items = signal<BatchTrackingItemChoice[]>([]);
  protected readonly warehouses = signal<BatchTrackingWarehouseChoice[]>([]);
  protected readonly contacts = signal<BatchTrackingContactChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<BatchTrackingDetailReportResultDto | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly batchOptions = computed<BbSelectOption<number>[]>(() =>
    this.batches().map((b) => ({
      value: b.itemBatchId,
      label: `${b.batchNumber} (${b.itemName})`,
    })),
  );

  protected readonly itemOptions = computed<BbSelectOption<number>[]>(() =>
    this.items().map((i) => ({
      value: i.itemId,
      label: `${i.itemCode} - ${i.itemName}`,
    })),
  );

  protected readonly warehouseOptions = computed<BbSelectOption<number>[]>(() =>
    this.warehouses().map((w) => ({
      value: w.warehouseId,
      label: w.warehouseName,
    })),
  );

  protected readonly contactOptions = computed<BbSelectOption<number>[]>(() =>
    this.contacts().map((c) => ({
      value: c.contactId,
      label: c.displayName,
    })),
  );

  protected readonly batchGroups = computed<BatchTrackingDetailGroupDto[]>(() => {
    const res = this.result();
    return res ? res.batches : [];
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'movementDate', header: 'Date', width: '7rem' },
    { field: 'documentNumber', header: 'Document', width: '8.5rem' },
    { field: 'movementType', header: 'Type', width: '8rem' },
    { field: 'contactName', header: 'Party / Contact' },
    { field: 'warehouseName', header: 'Warehouse', width: '8.5rem' },
    { field: 'quantityIn', header: 'Qty In', numeric: true, width: '6.5rem' },
    { field: 'quantityOut', header: 'Qty Out', numeric: true, width: '6.5rem' },
    { field: 'runningBalance', header: 'Running Bal', numeric: true, width: '7.5rem' },
  ];

  protected readonly messages = computed<UiMessage[]>(() => {
    const err = this.failure();
    return err ? [{ text: err, tone: 'error' }] : [];
  });

  @HostListener('window:resize')
  onResize(): void {
    if (typeof window !== 'undefined') {
      this.narrow.set(window.innerWidth < 800);
    }
  }

  ngOnInit(): void {
    this.onResize();
    void this.init();
  }

  private async init(): Promise<void> {
    await this.loadFilterChoices();
    await this.run();
  }

  protected err(field: FilterName, label: string): string {
    const srv = this.serverErrors()[field];
    if (srv) {
      return srv;
    }
    const ctrl = this.filters.get(field);
    if (!ctrl || !ctrl.touched || ctrl.valid) {
      return '';
    }
    if (ctrl.hasError('required')) {
      return `${label} is required.`;
    }
    return 'Invalid value.';
  }

  protected async run(): Promise<void> {
    this.serverErrors.set({});
    this.failure.set(null);

    const fromVal = this.filters.value.fromDate;
    const toVal = this.filters.value.toDate;

    if (!fromVal) {
      this.filters.get('fromDate')?.markAsTouched();
      return;
    }
    if (!toVal) {
      this.filters.get('toDate')?.markAsTouched();
      return;
    }

    if (toVal < fromVal) {
      this.serverErrors.set({ toDate: 'To date cannot be before from date.' });
      return;
    }

    this.busy.set(true);
    try {
      const req: BatchTrackingDetailReportRequest = {
        fromDate: fromVal,
        toDate: toVal,
        batchId: this.filters.value.batchId ?? undefined,
        itemId: this.filters.value.itemId ?? undefined,
        warehouseId: this.filters.value.warehouseId ?? undefined,
        contactId: this.filters.value.contactId ?? undefined,
        pageNumber: 1,
        pageSize: 50,
      };

      const res = await this.service.getReport(req);
      this.result.set(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to run batch tracking detail report.';
      this.failure.set(msg);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadFilterChoices(): Promise<void> {
    try {
      const [batches, items, warehouses, contacts] = await Promise.all([
        this.service.getBatches().catch(() => []),
        this.service.getItems().catch(() => []),
        this.service.getWarehouses().catch(() => []),
        this.service.getContacts().catch(() => []),
      ]);
      this.batches.set(batches);
      this.items.set(items);
      this.warehouses.set(warehouses);
      this.contacts.set(contacts);
      this.choicesLoaded.set(true);
    } catch {
      this.choicesLoaded.set(true);
    }
  }
}
