import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  InventoryItemSummaryGridRow,
  InventoryItemSummaryResult,
  InventoryItemSummaryService,
  PayableInvoiceCategoryChoice,
  PayableInvoiceItemChoice,
  appendInventoryItemSummaryPage,
  categoryChoiceLabel,
  hasMoreInventoryItemSummary,
  inventoryItemSummaryRows,
  inventoryItemSummarySentence,
  itemChoiceLabel,
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
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';

type FilterName = 'fromDate' | 'toDate' | 'itemCategoryId' | 'itemId';

/**
 * The Inventory Item Summary (TK-243): its own page on its own endpoint. For each stocked item over a period,
 * what stock it opened with, what was purchased, sold and adjusted, what it closed with and what its sales
 * earned over what they cost, a quantity over a value in each column and the grand total of the values under
 * the items. A page of items loads at a time.
 *
 * It opens on the financial year to date and runs straight away. Both dates are required, so a missing or
 * out-of-order one shows at its field, as does any other refusal; a failure shows in the message box. An item
 * with no stock at the start and no movement in the period is left out unless the tick says to list it.
 * Values are in the branch's currency; stock is one pool for the branch, so nothing is split by warehouse.
 * Nothing is posted.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-inventory-item-summary-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    BranchDateInputComponent,
    SelectComponent,
    CheckboxComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    ReportAmountComponent,
    ReportDatePipe,
  ],
  templateUrl: './inventory-item-summary.page.html',
  styleUrl: './inventory-item-summary.page.scss',
})
export class InventoryItemSummaryPage implements OnInit {
  private readonly report = inject(InventoryItemSummaryService);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each item is a card rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    fromDate: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    toDate: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    itemCategoryId: new FormControl<number | null>(null),
    itemId: new FormControl<number | null>(null),
    showItemsWithNoMovement: new FormControl<boolean>(false, { nonNullable: true }),
  });

  protected readonly items = signal<PayableInvoiceItemChoice[]>([]);
  protected readonly categories = signal<PayableInvoiceCategoryChoice[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly result = signal<InventoryItemSummaryResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  protected readonly itemOptions = computed<BbSelectOption<number>[]>(() =>
    this.items().map((i) => ({ value: i.itemId, label: itemChoiceLabel(i) })),
  );

  protected readonly categoryOptions = computed<BbSelectOption<number>[]>(() =>
    this.categories().map((c) => ({ value: c.itemCategoryId, label: categoryChoiceLabel(c) })),
  );

  protected readonly rows = computed<InventoryItemSummaryGridRow[]>(() => {
    const result = this.result();
    return result ? inventoryItemSummaryRows(result) : [];
  });

  protected readonly more = computed(() => {
    const result = this.result();
    return result ? hasMoreInventoryItemSummary(result) : false;
  });

  protected readonly columns: ColumnDef[] = [
    { field: 'item', header: 'Item' },
    { field: 'opening', header: 'Opening', numeric: true, width: '7rem' },
    { field: 'purchased', header: 'Purchased', numeric: true, width: '7rem' },
    { field: 'sold', header: 'Sold', numeric: true, width: '7rem' },
    { field: 'adjusted', header: 'Adjusted', numeric: true, width: '7rem' },
    { field: 'closing', header: 'Closing', numeric: true, width: '7rem' },
    { field: 'cogs', header: 'COGS', numeric: true, width: '6.5rem' },
    { field: 'profit', header: 'Profit', numeric: true, width: '6.5rem' },
  ];

  /** The period's stock in words; a failure above it. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const messages: UiMessage[] = [];
    const failure = this.failure();
    if (failure) {
      messages.push({ tone: 'error', text: failure });
    }

    const result = this.result();
    if (result && result.totalItems > 0) {
      messages.push({ tone: 'info', text: inventoryItemSummarySentence(result, (value) => this.formats.formatMoney(value)) });
    }

    return messages;
  });

  protected readonly quantity = (value: number | null): string => this.formats.formatQuantity(value);

  protected readonly rowClass = (row: InventoryItemSummaryGridRow): string => (row.kind === 'total' ? 'row--total' : '');

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
      // Items from the filters before this run would sit under a refusal of the ones now chosen.
      this.result.set(null);
      this.showFailure(err);
    } finally {
      this.busy.set(false);
    }
  }

  /** Adds the next page of items to the ones loaded. */
  async loadMore(): Promise<void> {
    const loaded = this.result();
    if (!loaded || this.busy() || !hasMoreInventoryItemSummary(loaded)) {
      return;
    }

    this.busy.set(true);

    try {
      const next = await this.report.run(this.request(loaded.pageNumber + 1));
      this.result.set(appendInventoryItemSummaryPage(loaded, next));
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
      itemCategoryId: value.itemCategoryId,
      itemId: value.itemId,
      showItemsWithNoMovement: value.showItemsWithNoMovement,
      pageNumber,
    };
  }

  private async loadChoices(): Promise<void> {
    const [items, categories] = await Promise.all([
      this.report.items().catch(() => [] as PayableInvoiceItemChoice[]),
      this.report.itemCategories().catch(() => [] as PayableInvoiceCategoryChoice[]),
    ]);
    this.items.set(items);
    this.categories.set(categories);
    this.choicesLoaded.set(true);
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the Inventory Item Summary on this branch.');
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

    this.failure.set(failure?.error?.message || 'The Inventory Item Summary could not be run. Please try again.');
  }

  private clearServerError(name: string): void {
    const errors = this.serverErrors();
    if (name in errors) {
      const { [name as FilterName]: _cleared, ...rest } = errors;
      this.serverErrors.set(rest);
    }
  }
}
