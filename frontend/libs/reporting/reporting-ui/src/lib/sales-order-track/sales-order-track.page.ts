import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedReceivablesCustomerChoice,
  SalesOrderProgress,
  SalesOrderTrackGridRow,
  SalesOrderTrackResult,
  SalesOrderTrackService,
  appendSalesOrderTrackPage,
  customerChoiceLabel,
  hasMoreSalesOrderTrack,
  salesOrderProgressLabel,
  salesOrderProgressTag,
  salesOrderTrackRows,
  salesOrderTrackSentence,
  salesOrderTrackTabs,
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

type FilterName = 'fromDate' | 'toDate' | 'contactId';

/**
 * The Sales Order Track Report (TK-230): its own page on its own endpoint. Each sales order dated in a
 * period, newest first, with how far it has got (open, partly delivered, delivered, invoiced, closed
 * short or cancelled), its lines with what was ordered, delivered and invoiced of each and what is
 * still to deliver or bill, the progress tabs above it counting every progress and the total of every
 * order the filters match under it. A page of orders loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a missing
 * or out-of-order one shows at its field, as does any other refusal; a failure shows in the message
 * box. Outstanding only keeps the orders with something still to deliver or bill. Values are before tax
 * in the branch's currency, at the order's own price. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sales-order-track-page',
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
  templateUrl: './sales-order-track.page.html',
  styleUrl: './sales-order-track.page.scss',
})
export class SalesOrderTrackPage implements OnInit {
  private readonly report = inject(SalesOrderTrackService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each order is a card rather than a run of rows and columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    outstandingOnly: new FormControl<boolean>(false, { nonNullable: true }),
  });

  /** The progress tab chosen; empty is All. */
  protected readonly progress = signal<SalesOrderProgress | ''>('');

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<SalesOrderTrackResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly tabs = computed(() => salesOrderTrackTabs(this.result()?.progressCounts));

  protected readonly rows = computed<SalesOrderTrackGridRow[]>(() => {
    const result = this.result();
    return result ? salesOrderTrackRows(result) : [];
  });

  /** The cards of the phone layout: an order's heading, lines and value together; the total last. */
  protected readonly cards = computed<SalesOrderTrackGridRow[][]>(() => {
    const cards: SalesOrderTrackGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'order' || row.kind === 'total' || cards.length === 0) {
        cards.push([row]);
      } else {
        cards[cards.length - 1].push(row);
      }
    }
    return cards;
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreSalesOrderTrack(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'item', header: 'Order / item' },
    { field: 'ordered', header: 'Ordered', numeric: true, width: '5.5rem' },
    { field: 'delivered', header: 'Delivered', numeric: true, width: '5.5rem' },
    { field: 'invoiced', header: 'Invoiced', numeric: true, width: '5.5rem' },
    { field: 'toDeliver', header: 'To deliver', numeric: true, width: '5.5rem' },
    { field: 'toBill', header: 'To bill', numeric: true, width: '5.5rem' },
    { field: 'orderedValue', header: 'Value', numeric: true, width: '7.5rem' },
    { field: 'toDeliverValue', header: 'To deliver', numeric: true, width: '7.5rem' },
    { field: 'toBillValue', header: 'To bill', numeric: true, width: '7.5rem' },
  ];

  /** The period's totals in words; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalOrders > 0) {
      messages.push({ tone: 'info', text: salesOrderTrackSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: SalesOrderTrackGridRow): string =>
    row.kind === 'order' ? 'row--group' : row.kind === 'subtotal' ? 'row--subtotal' : row.kind === 'total' ? 'row--total' : '';

  protected readonly progressLabel = salesOrderProgressLabel;
  protected readonly progressTag = salesOrderProgressTag;

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

  /** Chooses a progress tab and runs again from the first page. */
  protected chooseProgress(value: string): void {
    this.progress.set(value as SalesOrderProgress | '');
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
      // Orders from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of orders to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreSalesOrderTrack(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendSalesOrderTrackPage(loaded, next));
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
      outstandingOnly: value.outstandingOnly,
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
      this.failure.set('You do not have access to the Sales Order Track Report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Sales Order Track Report could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
