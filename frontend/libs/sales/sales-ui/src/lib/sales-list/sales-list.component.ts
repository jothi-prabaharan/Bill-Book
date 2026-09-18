import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  AllocationApiService,
  LedgerSide,
  allocationPair,
  locateDocument,
  readApiFailure,
} from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import { TransactionService, SalesTransactionListItem } from '@bill-book/sales-core';
import {
  AllocationModalComponent,
  AllocationRow,
  AllocationSubmission,
  AllocationTarget,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  PeriodFilterBarComponent,
  PeriodRange,
  UiMessage,
  isWithin,
  rangeForPreset,
} from '@bill-book/ui-components';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-sales-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    AllocationModalComponent,
    PeriodFilterBarComponent,
  ],
  templateUrl: './sales-list.component.html'
})
export class SalesListComponent implements OnInit {
  private transactionService = inject(TransactionService);
  private router = inject(Router);

  /*
   * Optional because this component is also constructed directly in specs, where
   * there is no routed context to inject. Without a route it simply opens on the
   * mixed list, which is the same thing as arriving with no type in the URL.
   */
  private readonly route = inject(ActivatedRoute, { optional: true });
  private readonly allocations = inject(AllocationApiService);
  protected readonly formatSettings = inject(FormatSettingsService);

  // --- Allocation modal -----------------------------------------------------
  protected readonly allocateOpen = signal(false);
  protected readonly allocateTarget = signal<AllocationTarget | null>(null);
  protected readonly allocateRows = signal<readonly AllocationRow[]>([]);
  protected readonly allocateLoading = signal(false);
  protected readonly allocateSaving = signal(false);
  protected readonly allocateMessages = signal<UiMessage[]>([]);

  /** Which side the opened document sits on, as `locateDocument` found it. */
  protected readonly allocateSide = signal<LedgerSide>('target');

  readonly transactions = signal<SalesTransactionListItem[]>([]);
  selectedType: string = '';

  /**
   * Sales is a dated register, so the period bar — not a search box — is what
   * narrows it. The grid's own column filters narrow whatever survives this.
   */
  readonly range = signal<PeriodRange>(rangeForPreset('this-month'));

  readonly visibleTransactions = computed(() =>
    this.transactions().filter((t) => isWithin(this.range(), String(t.documentDate ?? ''))),
  );

  /** What the bar's "Total in INR" figure sums: the rows on screen, nothing else. */
  readonly periodTotal = computed(() =>
    this.visibleTransactions().reduce((sum, t) => sum + (t.totalAmount ?? 0), 0),
  );

  columns: ColumnDef[] = [
    { field: 'documentDate', header: 'Date' },
    { field: 'transactionType', header: 'Type' },
    { field: 'documentNo', header: 'Number' },
    { field: 'contactName', header: 'Customer' },
    { field: 'totalAmount', header: 'Amount', align: 'right' },
    { field: 'status', header: 'Status' }
  ];

  // Mock properties for parity with design


  ngOnInit() {
    // The type lives in the URL so the menu can link straight to one document's
    // register — /sales/transactions?type=Invoice — and so a filtered list can be
    // bookmarked, shared and reached by the back button. Subscribed rather than
    // read once: moving between two document types is the same route with a
    // different parameter, and Angular reuses the component.
    if (this.route) {
      this.route.queryParamMap.subscribe((params) => {
        const type = params.get('type') ?? '';
        if (type === this.selectedType) return;
        this.selectedType = type;
        this.loadTransactions();
      });
      return;
    }

    this.loadTransactions();
  }

  loadTransactions() {
    this.transactionService.list(this.selectedType).subscribe((t) => this.transactions.set(t));
  }

  setRange(range: PeriodRange) {
    this.range.set(range);
  }

  setType(type: string) {
    this.selectedType = type;

    // Push the tab into the URL rather than only into local state, so what the
    // person is looking at and what the address bar says cannot disagree. With a
    // route present the subscription above does the reload; without one there is
    // nothing to subscribe to, so load here.
    if (this.route) {
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { type: type || null },
        queryParamsHandling: 'merge',
      });
      return;
    }

    this.loadTransactions();
  }

  onTypeChange() {
    this.loadTransactions();
  }

  getRouteForTransaction(transaction: SalesTransactionListItem): string {
    switch (transaction.transactionType) {
      case 'Quote': return `/sales/quotes/${transaction.transactionId}`;
      case 'SalesOrder': return `/sales/sales-orders/${transaction.transactionId}`;
      case 'Invoice': return `/sales/invoices/${transaction.transactionId}`;
      case 'DeliveryChallan': return `/sales/delivery-challans/${transaction.transactionId}`;
      case 'CreditNote': return `/sales/credit-notes/${transaction.transactionId}`;
      default: return `/sales`;
    }
  }

  navigateToTransaction(transaction: SalesTransactionListItem) {
    void this.router.navigate([this.getRouteForTransaction(transaction)]);
  }

  /**
   * A posted credit note is the one document on this list that carries a credit
   * to give. A quote and an order have not reached the ledger at all, an
   * invoice is settled from its own list, and a draft or voided note holds
   * nothing.
   */
  canAllocate(transaction: SalesTransactionListItem): boolean {
    return transaction.transactionType === 'CreditNote' && transaction.status === 'Posted';
  }

  /**
   * Opens the modal for one credit note — which invoices does this note cover.
   *
   * **A credit note is Cr AR, so it is a source**, and the invoices it settles
   * are the targets: the mirror of the invoice list, and the same way round as
   * a bill on the purchase list. The balance is read here rather than taken
   * from the row, because the list is a snapshot and the note may have been
   * partly applied since it was fetched.
   */
  async allocate(transaction: SalesTransactionListItem, event?: Event): Promise<void> {
    event?.stopPropagation();

    this.allocateOpen.set(true);
    this.allocateLoading.set(true);
    this.allocateMessages.set([]);
    this.allocateRows.set([]);
    this.allocateTarget.set(null);

    try {
      const open = await this.allocations.openDocuments(transaction.contactId);

      // Which side this document sits on is read off the payload rather than
      // assumed by this screen: an invoice is Dr AR and lands among the
      // targets, a bill is Cr AP and lands among the sources, and a credit note
      // is a source too. Deriving it means no screen can hold a stale opinion
      // about the direction, and the counterparts arrive from the other side by
      // construction.
      const located = locateDocument(open, 'CRN', transaction.transactionId);

      if (!located) {
        this.allocateMessages.set([
          { tone: 'warning', text: `${transaction.documentNo} has no credit left to apply.` },
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

  closeAllocate(): void {
    this.allocateOpen.set(false);
  }

  /** Posts one claim at a time, so they cannot race each other for the same balance. */
  async onAllocate(submission: AllocationSubmission): Promise<void> {
    this.allocateSaving.set(true);
    this.allocateMessages.set([]);

    try {
      for (const decision of submission.decisions) {
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
      this.loadTransactions();
    } catch (error) {
      const failure = readApiFailure(error);
      this.allocateMessages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.allocateSaving.set(false);
    }
  }
}

