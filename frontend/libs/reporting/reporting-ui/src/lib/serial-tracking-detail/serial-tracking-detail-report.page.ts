import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  SerialTrackingChoice,
  SerialTrackingContactChoice,
  SerialTrackingDetailGroupDto,
  SerialTrackingDetailReportRequest,
  SerialTrackingDetailReportResultDto,
  SerialTrackingDetailReportService,
  SerialTrackingItemChoice,
  SerialTrackingWarehouseChoice,
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
  TextInputComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'itemSerialId' | 'itemId' | 'warehouseId' | 'contactId' | 'search';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-serial-tracking-detail-report-page',
  standalone: true,
  imports: [
    DecimalPipe,
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    TextInputComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportDatePipe,
  ],
  templateUrl: './serial-tracking-detail-report.page.html',
  styleUrl: './serial-tracking-detail-report.page.scss',
})
export class SerialTrackingDetailReportPageComponent implements OnInit {
  private readonly service = inject(SerialTrackingDetailReportService);

  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    itemSerialId: new FormControl<number | null>(null),
    itemId: new FormControl<number | null>(null),
    warehouseId: new FormControl<number | null>(null),
    contactId: new FormControl<number | null>(null),
    search: new FormControl<string>(''),
  });

  protected readonly serials = signal<SerialTrackingChoice[]>([]);
  protected readonly items = signal<SerialTrackingItemChoice[]>([]);
  protected readonly warehouses = signal<SerialTrackingWarehouseChoice[]>([]);
  protected readonly contacts = signal<SerialTrackingContactChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<SerialTrackingDetailReportResultDto | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly serialOptions = computed<BbSelectOption<number>[]>(() =>
    this.serials().map((s) => ({
      value: s.itemSerialId,
      label: `${s.serialNumber} (${s.itemCode})`,
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

  protected readonly serialGroups = computed<SerialTrackingDetailGroupDto[]>(() => {
    const res = this.result();
    return res ? res.serials : [];
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
    { field: 'notes', header: 'Notes' },
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
      const req: SerialTrackingDetailReportRequest = {
        fromDate: fromVal,
        toDate: toVal,
        itemSerialId: this.filters.value.itemSerialId ?? undefined,
        itemId: this.filters.value.itemId ?? undefined,
        warehouseId: this.filters.value.warehouseId ?? undefined,
        contactId: this.filters.value.contactId ?? undefined,
        search: this.filters.value.search ? this.filters.value.search.trim() : undefined,
        pageNumber: 1,
        pageSize: 50,
      };

      const res = await this.service.getReport(req);
      this.result.set(res);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to run serial tracking detail report.';
      this.failure.set(msg);
    } finally {
      this.busy.set(false);
    }
  }

  private async loadFilterChoices(): Promise<void> {
    try {
      const [serials, items, warehouses, contacts] = await Promise.all([
        this.service.getSerials().catch(() => []),
        this.service.getItems().catch(() => []),
        this.service.getWarehouses().catch(() => []),
        this.service.getContacts().catch(() => []),
      ]);
      this.serials.set(serials);
      this.items.set(items);
      this.warehouses.set(warehouses);
      this.contacts.set(contacts);
      this.choicesLoaded.set(true);
    } catch {
      this.choicesLoaded.set(true);
    }
  }
}
