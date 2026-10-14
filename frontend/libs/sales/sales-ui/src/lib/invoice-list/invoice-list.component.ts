import { ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import {
  AllocationApiService,
  LedgerSide,
  allocationPair,
  locateDocument,
  readApiFailure,
} from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  E_INVOICE_STATUS_LABELS,
  EInvoiceStatus,
  eInvoiceNeedsAttention,
  InvoiceListItem,
  InvoiceService,
} from '@bill-book/sales-core';
import {
  AllocationModalComponent,
  AllocationRow,
  AllocationSubmission,
  AllocationTarget,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DataGridService,
  DocNumberComponent,
  MessageBoxComponent,
  MoneyCellComponent,
  PeriodFilterBarComponent,
  PeriodRange,
  RegisterDatePipe,
  RegisterHeaderComponent,
  RegisterStatusOption,
  SearchInputComponent,
  StatusTabsComponent,
  UiMessage,
  rangeForPreset,
  registerStatusLabel,
  registerStatusTone,
  statusTabsFromCounts,
} from '@bill-book/ui-components';

const GRID_CODE = 'INVOICE_LIST';

/** The statuses an invoice can be in, for the register's tabs (TK-187). */
export const INVOICE_STATUSES: readonly RegisterStatusOption[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'ReadyToPost', label: 'Ready to post' },
  { value: 'Posted', label: 'Posted' },
  { value: 'Void', label: 'Void' },
];


/**
 * The invoice list.
 *
 * **Paged on the server, not in the browser** — the same reasoning as the sales
 * order list, and more pressing here: invoices are the fastest-growing table a
 * branch has, so a list that fetched all of them to show twenty-five is the one
 * that gets slower every week.
 *
 * The **overdue** filter and its running total are what a collections call
 * actually needs: who owes, how much, and how late. Only a posted invoice can be
 * overdue — a draft owes nothing yet and a voided one never will — which is
 * decided on the server so the screen and a report cannot disagree about it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-invoice-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    MessageBoxComponent,
    AllocationModalComponent,
    SearchInputComponent,
    PeriodFilterBarComponent,
    RegisterHeaderComponent,
    StatusTabsComponent,
    DocNumberComponent,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  host: { class: 'register-page' },
  templateUrl: './invoice-list.component.html',
  styleUrl: './invoice-list.component.scss',
})
export class InvoiceListComponent implements OnInit {
  private readonly invoices = inject(InvoiceService);
  private readonly router = inject(Router);
  private readonly allocations = inject(AllocationApiService);
  protected readonly formatSettings = inject(FormatSettingsService);

  protected readonly rows = signal<InvoiceListItem[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly loading = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  /** The register's page size: the person's last choice, else the smallest. */
  protected readonly pageSize = signal(inject(DataGridService).loadState(GRID_CODE)?.pageSize ?? 10);
  protected readonly gridCode = GRID_CODE;

  /** The register's period; a dated register opens on the financial year. */
  protected readonly range = signal<PeriodRange>(rangeForPreset('this-financial-year'));

  /** Matches per status under the other filters, from the server. */
  protected readonly statusCounts = signal<Record<string, number>>({});

  /** What every matching invoice comes to in base currency, from the server. */
  protected readonly totalBase = signal(0);

  protected readonly tabs = computed(() => statusTabsFromCounts(this.statusCounts(), INVOICE_STATUSES));

  /** Invoices in the period under the other filters — the All tab's count. */
  protected readonly periodCount = computed(() => this.tabs()[0]?.count ?? 0);

  protected readonly statusLabel = registerStatusLabel;
  protected readonly statusTone = registerStatusTone;

  // --- Allocation modal -----------------------------------------------------
  protected readonly allocateOpen = signal(false);
  protected readonly allocateTarget = signal<AllocationTarget | null>(null);
  protected readonly allocateRows = signal<readonly AllocationRow[]>([]);
  protected readonly allocateLoading = signal(false);
  protected readonly allocateSaving = signal(false);
  protected readonly allocateMessages = signal<UiMessage[]>([]);

  /** Which side the opened document sits on, as `locateDocument` found it. */
  protected readonly allocateSide = signal<LedgerSide>('target');

  /** Bound with ngModel, so they are plain fields rather than signals. */
  protected status = '';
  protected search = '';
  protected overdueOnly = false;

  /** Only invoices whose IRN was refused or is still pending (TK-92). */
  protected eInvoiceAttention = false;

  protected readonly columns: ColumnDef[] = [
    { field: 'documentNo', header: 'Number', isTemplate: true },
    { field: 'documentDate', header: 'Date', dataType: 'date', isTemplate: true },
    { field: 'contactName', header: 'Customer' },
    { field: 'dueDate', header: 'Due', isTemplate: true },
    { field: 'status', header: 'Status', isTemplate: true },
    { field: 'settlementStatus', header: 'Payment', isTemplate: true },
    { field: 'eInvoiceStatus', header: 'E-invoice', isTemplate: true },
    { field: 'taxableAmount', header: 'Taxable', dataType: 'money', isTemplate: true },
    { field: 'totalAmount', header: 'Total', dataType: 'money', isTemplate: true },
  ];


  /**
   * What the rows on this page come to.
   *
   * **Of the page, and it says so** — the server pages before it sums, so a
   * running total across every match would be a different query. A figure
   * labelled as the page's own is honest; the same figure labelled "total
   * outstanding" is the one somebody reconciles against and finds short.
   */
  protected readonly pageTotal = computed(() =>
    this.rows().reduce((sum, row) => sum + row.totalAmount, 0),
  );

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    this.messages.set([]);

    try {
      const result = await this.invoices.list({
        // The server clamps this again. Both do it, because a page that trusted
        // the server and a server that trusted the page would each be relying
        // on the other.
        skip: Math.max(0, (this.page() - 1) * this.pageSize()),
        take: this.pageSize(),
        from: this.range().from || undefined,
        to: this.range().to || undefined,
        status: this.status || undefined,
        search: this.search.trim() || undefined,
        overdueOnly: this.overdueOnly || undefined,
        eInvoiceAttention: this.eInvoiceAttention || undefined,
      });

      this.rows.set(result.rows);
      this.total.set(result.total);
      this.statusCounts.set(result.statusCounts ?? {});
      this.totalBase.set(result.totalAmountBase ?? this.pageTotal());
    } catch (error) {
      const failure = readApiFailure(error);
      this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
      this.rows.set([]);
      this.total.set(0);
      this.statusCounts.set({});
      this.totalBase.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  /** A changed filter goes back to page one — page four of the old result is not page four of the new one. */
  protected async onFilterChange(): Promise<void> {
    this.page.set(1);
    await this.load();
  }

  protected async onPageChange(page: number): Promise<void> {
    if (this.page() === page) return;
    this.page.set(page);
    await this.load();
  }

  protected async onPageSizeChange(size: number): Promise<void> {
    this.pageSize.set(size);
    this.page.set(1);
    await this.load();
  }

  protected async setStatus(status: string): Promise<void> {
    this.status = status;
    await this.onFilterChange();
  }

  protected async setRange(range: PeriodRange): Promise<void> {
    this.range.set(range);
    await this.onFilterChange();
  }

  protected open(invoice: InvoiceListItem): void {
    void this.router.navigate(['/sales/invoices', invoice.invoiceId]);
  }

  protected create(): void {
    void this.router.navigate(['/sales/invoices/new']);
  }

  /**
   * Whether this invoice can take an allocation at all.
   *
   * A draft owes nothing yet and a voided one never will, so neither is a
   * receivable anything can be applied to. `settlementStatus` is absent for
   * exactly those, which is why it is the test rather than `status`.
   */
  protected canAllocate(invoice: InvoiceListItem): boolean {
    return invoice.settlementStatus === 'Unpaid' || invoice.settlementStatus === 'PartPaid';
  }

  /**
   * Opens the modal for one invoice.
   *
   * **The cap comes from the ledger, not from this row.** The list was paged
   * some time ago and a credit may have been applied since; reading the
   * outstanding from `open-documents` at the moment the modal opens means the
   * figure the user apportions against is the one the API will check the claim
   * against.
   */
  protected async allocate(invoice: InvoiceListItem, event?: Event): Promise<void> {
    // The row itself navigates to the invoice; the button must not.
    event?.stopPropagation();

    this.allocateOpen.set(true);
    this.allocateLoading.set(true);
    this.allocateMessages.set([]);
    this.allocateRows.set([]);
    this.allocateTarget.set(null);

    try {
      const open = await this.allocations.openDocuments(invoice.contactId);

      // Which side this document sits on is read off the payload rather than
      // assumed by this screen: an invoice is Dr AR and lands among the
      // targets, a bill is Cr AP and lands among the sources, and a credit note
      // is a source too. Deriving it means no screen can hold a stale opinion
      // about the direction, and the counterparts arrive from the other side by
      // construction.
      const located = locateDocument(open, 'INV', invoice.invoiceId);

      if (!located) {
        this.allocateMessages.set([
          { tone: 'warning', text: `${invoice.documentNo} has nothing left to settle.` },
        ]);
        return;
      }

      this.allocateSide.set(located.side);

      this.allocateTarget.set({
        transactionTypeCode: located.document.transactionTypeCode,
        transactionId: located.document.transactionId,
        documentNo: located.document.documentNo,
        documentDate: located.document.documentDate,
        totalAmount: located.document.totalAmount,
        outstandingAmount: located.document.unallocatedAmount,
      });

      this.allocateRows.set(
        located.counterparts
          .filter((counterpart) => counterpart.unallocatedAmount > 0)
          .map((counterpart) => ({
            transactionTypeCode: counterpart.transactionTypeCode,
            transactionId: counterpart.transactionId,
            documentNo: counterpart.documentNo,
            documentDate: counterpart.documentDate,
            totalAmount: counterpart.totalAmount,
            outstandingAmount: counterpart.unallocatedAmount,
            allocatedAmount: 0,
          })),
      );
    } catch (error) {
      const failure = readApiFailure(error);
      this.allocateMessages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.allocateLoading.set(false);
    }
  }

  protected closeAllocate(): void {
    this.allocateOpen.set(false);
  }

  /**
   * Posts the apportionment, one claim at a time.
   *
   * **Sequential rather than parallel**: each claim is checked against what is
   * left at the moment it lands, so firing them together makes them race for
   * the same remaining balance. The first refusal stops the run and is shown —
   * the ones already posted stand, which is safe because the API replaces on
   * (source, target) rather than appending, so a corrected retry does not
   * double up.
   */
  protected async onAllocate(submission: AllocationSubmission): Promise<void> {
    this.allocateSaving.set(true);
    this.allocateMessages.set([]);

    try {
      for (const decision of submission.decisions) {
        // An invoice is Dr AR, so open-documents lists it as a *target* and the
        // credits settling it as sources. That is the same way round the
        // settlement workspace posts them, which matters: the API replaces on
        // the ordered pair, so the two screens disagreeing would write two live
        // rows for one settlement.
        await this.allocations.allocate(
          allocationPair(
            this.allocateSide(),
            submission.target,
            decision,
            decision.amount,
            submission.allocationDate,
            submission.notes,
          ),
        );
      }

      this.allocateOpen.set(false);
      // The settlement column is now stale on every row this touched.
      await this.load();
    } catch (error) {
      const failure = readApiFailure(error);
      this.allocateMessages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.allocateSaving.set(false);
    }
  }

  /**
   * The tag a settlement status wears.
   *
   * A draft has no settlement at all — it is not an unpaid receivable — so it
   * gets no tag rather than an "Unpaid" one, which would put it beside invoices
   * somebody is actually chasing.
   */
  protected settlementTag(status: string | undefined): string {
    switch (status) {
      case 'Paid':
        return 'badge valid';
      case 'PartPaid':
        return 'badge soon';
      case 'Unpaid':
        return 'badge expired';
      default:
        return '';
    }
  }

  /** The tag class a status wears, from the shared set in `_tags.scss`. */
  protected eInvoiceLabel(status: EInvoiceStatus): string {
    return E_INVOICE_STATUS_LABELS[status] ?? status;
  }

  /** Refused and pending stand out: until the IRN is issued it is not a valid tax invoice. */
  protected eInvoiceTag(status: EInvoiceStatus): string {
    return eInvoiceNeedsAttention(status) ? 'badge expired' : 'chip';
  }

}

