import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  AgedReceivablesCustomerChoice,
  COLLECTION_PAYMENT_METHODS,
  CollectionBankAccountChoice,
  CollectionGridRow,
  CollectionPaymentMethod,
  InvoiceDnPaymentCollectionResult,
  InvoiceDnPaymentCollectionService,
  appendCollectionPage,
  bankAccountChoiceLabel,
  collectionRows,
  collectionSentence,
  customerChoiceLabel,
  hasMoreCollection,
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
 * The Invoice/DN Payment Collection Report (TK-227): its own page on its own endpoint. What was
 * collected in a period: the receipts posted in it, newest first, each with the invoices it settled
 * and what it did not settle, the total collected in the branch's currency under the list and the
 * customers who paid most above it. A page of receipts loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a
 * missing or out-of-order one shows at its field, as does any other refusal; a failure shows in the
 * message box. A receipt in another currency shows its amounts in that currency and its amount in
 * the branch's beside it. A voided receipt and a draft are not collections and are not listed.
 * Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-invoice-dn-payment-collection-page',
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
  templateUrl: './invoice-dn-payment-collection.page.html',
  styleUrl: './invoice-dn-payment-collection.page.scss',
})
export class InvoiceDnPaymentCollectionPage implements OnInit {
  private readonly report = inject(InvoiceDnPaymentCollectionService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each receipt is a card rather than a run of rows and columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    bankAccountId: new FormControl<number | null>(null),
    paymentMethod: new FormControl<CollectionPaymentMethod | null>(null),
  });

  protected readonly customers = signal<AgedReceivablesCustomerChoice[]>([]);
  protected readonly bankAccounts = signal<CollectionBankAccountChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<InvoiceDnPaymentCollectionResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly customerOptions = computed<BbSelectOption<number>[]>(() =>
    this.customers().map((c) => ({ value: c.contactId, label: customerChoiceLabel(c) })),
  );

  protected readonly bankAccountOptions = computed<BbSelectOption<number>[]>(() => {
    const base = this.result()?.baseCurrencyCode ?? this.formats.settings().currencyCode;
    return this.bankAccounts().map((b) => ({ value: b.bankAccountId, label: bankAccountChoiceLabel(b, base) }));
  });

  protected readonly methodOptions: BbSelectOption<CollectionPaymentMethod>[] = COLLECTION_PAYMENT_METHODS.map((m) => ({ value: m.value, label: m.label }));

  protected readonly rows = computed<CollectionGridRow[]>(() => {
    const result = this.result();
    return result ? collectionRows(result) : [];
  });

  /** The cards of the phone layout: a receipt's heading, documents and unsettled row together; the total last. */
  protected readonly groups = computed<CollectionGridRow[][]>(() => {
    const groups: CollectionGridRow[][] = [];
    for (const row of this.rows()) {
      if (row.kind === 'receipt' || row.kind === 'total' || groups.length === 0) {
        groups.push([row]);
      } else {
        groups[groups.length - 1].push(row);
      }
    }
    return groups;
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreCollection(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'item', header: 'Receipt / document' },
    { field: 'customer', header: 'Customer', width: '13rem' },
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
    if (result && result.totalReceipts > 0) {
      messages.push({ tone: 'info', text: collectionSentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly rowClass = (row: CollectionGridRow): string =>
    row.kind === 'receipt' ? 'row--group' : row.kind === 'total' ? 'row--total' : row.kind === 'unallocated' ? 'row--muted' : '';

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
    if (!loaded || this.busy() || !hasMoreCollection(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendCollectionPage(loaded, next));
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
    const [customers, accounts] = await Promise.all([
      this.report.customers().catch(() => [] as AgedReceivablesCustomerChoice[]),
      this.report.bankAccounts().catch(() => [] as CollectionBankAccountChoice[]),
    ]);
    this.customers.set(customers);
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
      this.failure.set('You do not have access to the Invoice/DN Payment Collection Report on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Invoice/DN Payment Collection Report could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
