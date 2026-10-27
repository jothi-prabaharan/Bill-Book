import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedPayablesVendorChoice,
  ReceiveOrderTrackGridRow,
  ReceiveOrderTrackResult,
  ReceiveOrderTrackService,
  ReceiveOrderTrackStatus,
  appendReceiveOrderTrackPage,
  hasMoreReceiveOrderTrack,
  receiveOrderStatusLabel,
  receiveOrderStatusTag,
  receiveOrderTrackRows,
  receiveOrderTrackSentence,
  receiveOrderTrackTabs,
  vendorChoiceLabel,
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
 * The Receive Order Track Report (TK-242): its own page on its own endpoint. Each posted goods receipt
 * dated in a period, newest first, with what its accepted goods are worth, how much of that posted bills
 * have billed, what is still to bill and its status (to bill, partly billed, billed, or nothing to bill),
 * the status tabs above it counting every status and the total of every receipt the filters match under
 * it. A page of receipts loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a missing
 * or out-of-order one shows at its field, as does any other refusal; a failure shows in the message box.
 * Not yet billed only keeps the receipts with something still to bill. Values are in the branch's
 * currency; a receipt in another currency also shows its own value. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-receive-order-track-page',
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
  templateUrl: './receive-order-track.page.html',
  styleUrl: './receive-order-track.page.scss',
})
export class ReceiveOrderTrackPage implements OnInit {
  private readonly report = inject(ReceiveOrderTrackService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each receipt is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    notYetBilledOnly: new FormControl<boolean>(false, { nonNullable: true }),
  });

  /** The status tab chosen; empty is All. */
  protected readonly status = signal<ReceiveOrderTrackStatus | ''>('');

  protected readonly vendors = signal<AgedPayablesVendorChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<ReceiveOrderTrackResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly vendorOptions = computed<BbSelectOption<number>[]>(() =>
    this.vendors().map((c) => ({ value: c.contactId, label: vendorChoiceLabel(c) })),
  );

  protected readonly tabs = computed(() => receiveOrderTrackTabs(this.result()?.statusCounts));

  protected readonly rows = computed<ReceiveOrderTrackGridRow[]>(() => {
    const result = this.result();
    return result ? receiveOrderTrackRows(result) : [];
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreReceiveOrderTrack(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'document', header: 'Receipt', width: '9rem' },
    { field: 'vendor', header: 'Vendor' },
    { field: 'order', header: 'Order', width: '6.5rem' },
    { field: 'value', header: 'Value', numeric: true, width: '8rem' },
    { field: 'billedValue', header: 'Billed', numeric: true, width: '7rem' },
    { field: 'toBillValue', header: 'To bill', numeric: true, width: '7rem' },
    { field: 'status', header: 'Status', width: '8.5rem' },
  ];

  /** The period's totals in words; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalReceipts > 0) {
      messages.push({ tone: 'info', text: receiveOrderTrackSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: ReceiveOrderTrackGridRow): string => (row.kind === 'total' ? 'row--total' : '');

  protected readonly statusLabel = receiveOrderStatusLabel;
  protected readonly statusTag = receiveOrderStatusTag;

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
  protected chooseStatus(value: string): void {
    this.status.set(value as ReceiveOrderTrackStatus | '');
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
      // Receipts from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of receipts to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreReceiveOrderTrack(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendReceiveOrderTrackPage(loaded, next));
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
      status: this.status() || null,
      notYetBilledOnly: value.notYetBilledOnly,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    this.vendors.set(await this.report.vendors().catch(() => [] as AgedPayablesVendorChoice[]));
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Receive Order Track Report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Receive Order Track Report could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
