import { ChangeDetectionStrategy } from '@angular/core';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  AllocationModalComponent,
  AllocationRow,
  AllocationSubmission,
  AllocationTarget,
  ColumnDef,
  DataGridComponent,
  UiMessage,
} from '@bill-book/ui-components';
import {
  AllocationApiService,
  LedgerSide,
  allocationPair,
  locateDocument,
  readApiFailure,
} from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { TransactionService, PurchaseTransactionListItem } from '@bill-book/purchase-core';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-purchase-list',
  standalone: true,
  imports: [
    DataGridComponent,
    CommonModule,
    RouterModule,
    FormsModule,
    AllocationModalComponent,
  ],
  templateUrl: './purchase-list.page.html'
})
export class PurchaseListPage implements OnInit {
  private transactionService = inject(TransactionService);
  private router = inject(Router);
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

  transactions: PurchaseTransactionListItem[] = [];
  selectedType: string = '';
  openFilter: string | null = null;
  filterOp: string = 'contains';
  filterVal: string = '';

  toggleFilter(col: string, event: Event) {
    event.stopPropagation();
    this.openFilter = this.openFilter === col ? null : col;
  }

  // Mock properties for parity with design


  ngOnInit() {
    this.loadTransactions();
  }

  loadTransactions() {
    this.transactionService.list(this.selectedType).subscribe(t => this.transactions = t);
  }

  setType(type: string) {
    this.selectedType = type;
    this.loadTransactions();
  }

  onTypeChange() {
    this.loadTransactions();
  }

  getRouteForTransaction(transaction: PurchaseTransactionListItem): string {
    switch (transaction.transactionType) {
      case 'Bill': return `/purchase/bills/${transaction.transactionId}`;
      case 'PurchaseOrder': return `/purchase/purchase-orders/${transaction.transactionId}`;
      case 'GoodsReceipt': return `/purchase/goods-receipts/${transaction.transactionId}`;
      case 'DebitNote': return `/purchase/debit-notes/${transaction.transactionId}`;
      default: return `/purchase`;
    }
  }

  columns: ColumnDef[] = [
    { field: 'documentDate', header: 'Date' },
    { field: 'transactionType', header: 'Type' },
    { field: 'documentNo', header: 'Number' },
    { field: 'contactName', header: 'Vendor' },
    { field: 'totalAmount', header: 'Amount' },
    { field: 'status', header: 'Status' }
  ];

  navigateToTransaction(transaction: PurchaseTransactionListItem) {
    void this.router.navigate([this.getRouteForTransaction(transaction)]);
  }

  /**
   * Only a posted bill is a payable anything can be applied to. A draft owes
   * nothing yet and a voided one never will, and the other three document types
   * on this list are not payables at all — a goods receipt moves stock, not
   * money.
   */
  canAllocate(transaction: PurchaseTransactionListItem): boolean {
    return transaction.transactionType === 'Bill' && transaction.status === 'Posted';
  }

  /**
   * Opens the modal for one bill.
   *
   * **The cap comes from the ledger, not from this row.** This list carries no
   * outstanding figure at all, and even where one exists it was fetched some
   * time ago; reading it from `open-documents` as the modal opens means the
   * balance the user apportions against is the one the API will check against.
   */
  async allocate(transaction: PurchaseTransactionListItem, event?: Event): Promise<void> {
    // The cells navigate to the bill; the button must not.
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
      const located = locateDocument(open, 'BIL', transaction.transactionId);

      if (!located) {
        this.allocateMessages.set([
          { tone: 'warning', text: `${transaction.documentNo} has nothing left to settle.` },
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

  /**
   * Posts the apportionment, one claim at a time.
   *
   * **Sequential rather than parallel**: each claim is checked against what is
   * left at the moment it lands, so firing them together makes them race for
   * the same remaining balance. A refusal stops the run; what already posted
   * stands, which is safe because the API replaces on (source, target) rather
   * than appending, so a corrected retry does not double up.
   */
  async onAllocate(submission: AllocationSubmission): Promise<void> {
    this.allocateSaving.set(true);
    this.allocateMessages.set([]);

    try {
      for (const decision of submission.decisions) {
        // The bill posts as the source here, the credit as the target — the
        // opposite of the invoice screen, and the same way round the settlement
        // workspace pairs them.
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



