import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedPayablesVendorChoice,
  COLLECTION_PAYMENT_METHODS,
  CollectionBankAccountChoice,
  PaymentGridRow,
  CollectionPaymentMethod,
  BillDnPaymentResult,
  BillDnPaymentService,
  appendPaymentPage,
  bankAccountChoiceLabel,
  paymentRows,
  paymentSentence,
  vendorChoiceLabel,
  hasMorePayments,
} from '@bill-book/reporting-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  ReportAmountComponent,
  ReportDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'contactId' | 'bankAccountId' | 'paymentMethod';

/**
 * The Bill/DN Payment Report (TK-238): its own page on its own endpoint. What was paid to vendors
 * in a period: the payments posted in it, newest first, each with the bills it settled and what it
 * did not settle, the total paid in the branch's currency under the list and the vendors paid most
 * above it. A page of payments loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a
 * missing or out-of-order one shows at its field, as does any other refusal; a failure shows in the
 * message box. A payment in another currency shows its amounts in that currency and its amount in
 * the branch's beside it. A voided payment, a draft and a TDS challan, which pays the government and
 * not a vendor, are not listed. Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bill-dn-payment-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './bill-dn-payment.page.html',
  styleUrl: './bill-dn-payment.page.scss',
})
export class BillDnPaymentPage implements OnInit {
  private readonly report = inject(BillDnPaymentService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each payment is a card rather than a run of rows and columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    bankAccountId: new FormControl<number | null>(null),
    paymentMethod: new FormControl<CollectionPaymentMethod | null>(null),
  });

  protected readonly vendors = signal<AgedPayablesVendorChoice[]>([]);
  protected readonly bankAccounts = signal<CollectionBankAccountChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<BillDnPaymentResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly vendorOptions = computed<BbSelectOption<number>[]>(() =>
    this.vendors().map((c) => ({ value: c.contactId, label: vendorChoiceLabel(c) })),
  );

  protected readonly bankAccountOptions = computed<BbSelectOption<number>[]>(() => {
    const base = this.result()?.baseCurrencyCode ?? this.formats.settings().currencyCode;
    return this.bankAccounts().map((b) => ({ value: b.bankAccountId, label: bankAccountChoiceLabel(b, base) }));
  });

  protected readonly methodOptions: BbSelectOption<CollectionPaymentMethod>[] = COLLECTION_PAYMENT_METHODS.map((m) => ({ value: m.value, label: m.label }));

  protected readonly rows = computed<PaymentGridRow[]>(() => {
    const result = this.result();
    return result ? paymentRows(result) : [];
  });

  /** The cards of the phone layout: a payment's heading, documents and unsettled row together; the total last. */
  protected readonly groups = computed<PaymentGridRow[][]>(() => {
    const groups: PaymentGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'payment' || row.kind === 'total' || groups.length === 0) {
        groups.push([row]);
      } else {
        groups[groups.length - 1].push(row);
      }
    }
    return groups;
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMorePayments(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'item', header: 'Payment / document' },
    { field: 'vendor', header: 'Vendor', width: '13rem' },
    { field: 'settledOn', header: 'Settled on', width: '7rem' },
    { field: 'amount', header: 'Amount', numeric: true, width: '10rem' },
  ];

  /** The period's totals in words; a failure above them. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalPayments > 0) {
      messages.push({ tone: 'info', text: paymentSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: PaymentGridRow): string =>
    row.kind === 'payment' ? 'row--group' : row.kind === 'total' ? 'row--total' : row.kind === 'unallocated' ? 'row--muted' : '';

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
      // Payments from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of payments to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMorePayments(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendPaymentPage(loaded, next));
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
      bankAccountId: value.bankAccountId,
      paymentMethod: value.paymentMethod,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [vendors, accounts] = await Promise.all([
      this.report.vendors().catch(() => [] as AgedPayablesVendorChoice[]),
      this.report.bankAccounts().catch(() => [] as CollectionBankAccountChoice[]),
    ]);
    this.vendors.set(vendors);
    this.bankAccounts.set(accounts);
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Bill/DN Payment Report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Bill/DN Payment Report could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
