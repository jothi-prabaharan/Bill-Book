import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedReceivablesCustomerChoice,
  DELIVERY_CHALLAN_TYPES,
  DeliveryChallanProgress,
  DeliveryChallanType,
  DeliveryOrderTrackGridRow,
  DeliveryOrderTrackResult,
  DeliveryOrderTrackService,
  appendDeliveryOrderTrackPage,
  customerChoiceLabel,
  deliveryChallanProgressLabel,
  deliveryChallanProgressTag,
  deliveryChallanTypeLabel,
  deliveryOrderTrackRows,
  deliveryOrderTrackSentence,
  deliveryOrderTrackTabs,
  hasMoreDeliveryOrderTrack,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  SelectComponent,
  StatusTabsComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'contactId' | 'challanType';

/**
 * The Delivery Order Track Report (TK-232): its own page on its own endpoint. Each delivery challan dated
 * in a period, newest first, with how far it has been billed (not invoiced, partly invoiced or invoiced),
 * its lines with what was delivered and invoiced of each and what is still to bill, the sales order it was
 * delivered against and the invoices raised from it, the status tabs above it counting every status and the
 * total of every challan the filters match under it. A page of challans loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a missing or
 * out-of-order one shows at its field, as does any other refusal; a failure shows in the message box. Not
 * yet invoiced only keeps the challans with something still to bill. Values are before tax in the branch's
 * currency, at the challan's own price. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-delivery-order-track-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    SelectComponent,
    CheckboxComponent,
    StatusTabsComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './delivery-order-track.page.html',
  styleUrl: './delivery-order-track.page.scss',
})
export class DeliveryOrderTrackPage implements OnInit {
  private readonly report = inject(DeliveryOrderTrackService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each challan is a card rather than a run of rows and columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    challanType: new FormControl<DeliveryChallanType | null>(null),
    notInvoicedOnly: new FormControl<boolean>(false, { nonNullable: true }),
  });

  /** The status tab chosen; empty is All. */
  protected readonly progress = signal<DeliveryChallanProgress | ''>('');

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<DeliveryOrderTrackResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly typeOptions: BbSelectOption<DeliveryChallanType>[] = DELIVERY_CHALLAN_TYPES.map((t) => ({ value: t.value, label: t.label }));

  protected readonly tabs = computed(() => deliveryOrderTrackTabs(this.result()?.progressCounts));

  protected readonly rows = computed<DeliveryOrderTrackGridRow[]>(() => {
    const result = this.result();
    return result ? deliveryOrderTrackRows(result) : [];
  });

  /** The cards of the phone layout: a challan's heading, lines and value together; the total last. */
  protected readonly cards = computed<DeliveryOrderTrackGridRow[][]>(() => {
    const cards: DeliveryOrderTrackGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'challan' || row.kind === 'total' || cards.length === 0) {
        cards.push([row]);
      } else {
        cards[cards.length - 1].push(row);
      }
    }
    return cards;
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreDeliveryOrderTrack(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'item', header: 'Challan / item' },
    { field: 'delivered', header: 'Delivered', numeric: true, width: '5.5rem' },
    { field: 'invoiced', header: 'Invoiced', numeric: true, width: '5.5rem' },
    { field: 'toBill', header: 'To bill', numeric: true, width: '5.5rem' },
    { field: 'deliveredValue', header: 'Delivered value', numeric: true, width: '7.5rem' },
    { field: 'invoicedValue', header: 'Invoiced value', numeric: true, width: '7.5rem' },
    { field: 'toBillValue', header: 'To bill value', numeric: true, width: '7.5rem' },
  ];

  /** The period's totals in words; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalChallans > 0) {
      messages.push({ tone: 'info', text: deliveryOrderTrackSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: DeliveryOrderTrackGridRow): string =>
    row.kind === 'challan' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'total' ? 'row--total' : '';

  protected readonly progressLabel = deliveryChallanProgressLabel;
  protected readonly progressTag = deliveryChallanProgressTag;
  protected readonly typeLabel = deliveryChallanTypeLabel;

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      const phone = window.matchMedia('(max-width: 640px)');
      const follow = (): void => this.narrow.set(phone.matches);
      follow();
      phone.addEventListener('change', follow);
      this.destroyRef.onDestroy(() => phone.removeEventListener('change', follow));
    }

    for (const name of Object.keys(this.filters.controls)) {
      const control: AbstractControl = this.filters.controls[name as keyof typeof this.filters.controls];
      control.valueChanges.subscribe(() => this.clearServerError(name));
    }

    void this.loadChoices();
    void this.run();
  }

  /** The words under a field: the server's refusal, else a missing required date once touched. */
  protected err(name: FilterName, label: string): string {
    const server = this.serverErrors()[name];
    if (server) {
      return server;
    }

    const control = this.filters.controls[name];
    if (!control.touched || !control.errors) {
      return '';
    }

    return control.errors['required'] ? `${label} is required.` : '';
  }

  /** Chooses a status tab and runs again from the first page. */
  protected chooseProgress(value: string): void {
    this.progress.set(value as DeliveryChallanProgress | '');
    void this.run();
  }

  /** Runs for the period and filters now chosen, from the first page. */
  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      this.result.set(await this.report.run(this.request(1)));
    } catch (err: unknown) {
      // Challans from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of challans to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreDeliveryOrderTrack(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendDeliveryOrderTrackPage(loaded, next));
    } catch (err: unknown) {
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  private request(pageNumber: number) {
    const value = this.filters.getRawValue();
    return {
      fromDate: value.fromDate as string,
      toDate: value.toDate as string,
      contactId: value.contactId,
      progress: this.progress() || null,
      challanType: value.challanType,
      notInvoicedOnly: value.notInvoicedOnly,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    this.customers.set(await this.report.customers().catch(() => [] as AgedReceivablesCustomerChoice[]));
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Delivery Order Track Report on this branch.');
      return;
    }

    const fieldErrors: Partial<Record<FilterName, string>> = {};
    for (const [key, words] of Object.entries(failure?.error?.errors ?? {})) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as FilterName;
      if (name in this.filters.controls && words?.[0]) {
        fieldErrors[name] = words[0];
      }
    }

    if (Object.keys(fieldErrors).length > 0) {
      this.serverErrors.set(fieldErrors);
      return;
    }

    this.failure.set(failure?.error?.message || 'The Delivery Order Track Report could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
