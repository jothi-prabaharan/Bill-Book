import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import {
  AllocationModalComponent,
  AllocationRow,
  AllocationSubmission,
  AllocationTarget,
  ColumnDef,
  ConfirmDialogService,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DocNumberComponent,
  IconButtonComponent,
  MoneyCellComponent,
  PeriodFilterBarComponent,
  PeriodRange,
  RegisterDatePipe,
  RegisterHeaderComponent,
  RegisterStatusOption,
  StatusTabsComponent,
  UiMessage,
  countStatusTabs,
  isWithin,
  rangeForPreset,
  registerStatusTone,
} from '@bill-book/ui-components';
import {
  AllocationApiService,
  LedgerSide,
  allocationPair,
  locateDocument,
  readApiFailure,
} from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';

type Direction = 'spend' | 'receive';

interface ContactOption {
  contactId: number;
  contactCode: string;
  displayName: string;
  isCustomer: boolean;
  isVendor: boolean;
}

interface MoneyDocumentListItem {
  documentId: number;
  transactionNo: string | null;
  transactionDate: string;
  bankAccountId: number;
  bankAccountName: string;
  contactId: number | null;
  amount: number;
  currencyCode: string;
  exchangeRate: number;
  paymentMethod: string;
  referenceNo: string | null;
  memo: string | null;
  status: 'Draft' | 'Posted' | 'Void';
  postedAt: string | null;
  mappingTransactionTypeCode: string | null;
  mappingTransactionId: number | null;
  tdsAmount?: number;
  tdsSectionCode?: string | null;
  deductorTan?: string | null;
  isTdsChallan?: boolean;
  challanNo?: string | null;
  bsrCode?: string | null;
  challanSectionCode?: string | null;
  challanMonth?: string | null;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-money-document-list-page',
  standalone: true,
  imports: [
    DataGridComponent,
    DataGridCellTemplateDirective,
    RegisterHeaderComponent,
    StatusTabsComponent,
    PeriodFilterBarComponent,
    DocNumberComponent,
    MoneyCellComponent,
    RegisterDatePipe,
    IconButtonComponent,
    AllocationModalComponent,
  ],
  host: { class: 'register-page' },
  templateUrl: './money-document-list.page.html',
  styleUrl: './money-document-list.page.scss',
})
export class MoneyDocumentListPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly allocations = inject(AllocationApiService);
  protected readonly formatSettings = inject(FormatSettingsService);

  protected readonly direction = signal<Direction>('spend');
  protected readonly rows = signal<MoneyDocumentListItem[]>([]);
  protected readonly contacts = signal<ContactOption[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  /** Draft, posted or void, for the register's tabs (TK-187). */
  protected readonly statuses: readonly RegisterStatusOption[] = [
    { value: 'Draft', label: 'Draft' },
    { value: 'Posted', label: 'Posted' },
    { value: 'Void', label: 'Void' },
  ];

  protected readonly statusFilter = signal('');
  protected readonly range = signal<PeriodRange>(rangeForPreset('this-financial-year'));

  // Allocation modal
  protected readonly allocateOpen = signal(false);
  protected readonly allocateTarget = signal<AllocationTarget | null>(null);
  protected readonly allocateRows = signal<readonly AllocationRow[]>([]);
  protected readonly allocateLoading = signal(false);
  protected readonly allocateSaving = signal(false);
  protected readonly allocateMessages = signal<UiMessage[]>([]);
  protected readonly allocateSide = signal<LedgerSide>('source');

  protected readonly statusTone = registerStatusTone;

  protected readonly title = computed(() =>
    this.direction() === 'spend' ? 'Spend money' : 'Receive money',
  );

  protected readonly documentCode = computed(() =>
    this.direction() === 'spend' ? 'SPM' : 'RCM',
  );

  protected readonly contactLabel = computed(() =>
    this.direction() === 'spend' ? 'Paid to' : 'Received from',
  );

  protected readonly columns = computed<ColumnDef[]>(() => [
    { field: 'transactionNo', header: 'Number' },
    { field: 'transactionDate', header: 'Date', dataType: 'date' },
    { field: 'contact', header: this.contactLabel() },
    { field: 'account', header: 'Account' },
    { field: 'status', header: 'Status' },
    { field: 'amount', header: 'Amount', dataType: 'money' },
    { field: 'actions', header: '', sortable: false, filterable: false },
  ]);

  protected readonly periodRows = computed(() =>
    this.rows().filter((row) => isWithin(this.range(), row.transactionDate)),
  );

  protected readonly tabs = computed(() => countStatusTabs(this.periodRows(), this.statuses, (row) => row.status));

  protected readonly visibleRows = computed(() => {
    const status = this.statusFilter();
    return this.periodRows().filter((row) => !status || row.status === status);
  });

  protected readonly periodTotal = computed(() =>
    this.visibleRows().reduce((sum, row) => sum + row.amount * (row.exchangeRate || 1), 0),
  );

  private get endpoint(): string {
    return this.direction() === 'spend' ? '/api/spend-money' : '/api/receive-money';
  }

  ngOnInit(): void {
    this.direction.set(this.route.snapshot.data['direction'] === 'receive' ? 'receive' : 'spend');

    if (this.route.snapshot.queryParamMap.get('action') === 'create') {
      this.createDocument();
      return;
    }

    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const [rows, contacts] = await Promise.all([
        this.req<MoneyDocumentListItem[]>('GET', this.endpoint),
        this.req<ContactOption[]>('GET', '/api/contacts'),
      ]);
      this.rows.set(rows);
      this.contacts.set(contacts);
    } catch {
      this.show(`Could not load ${this.title().toLowerCase()} documents.`, true);
    } finally {
      this.busy.set(false);
    }
  }

  createDocument(): void {
    const route = this.direction() === 'spend' ? '/banking/spend-money/new' : '/banking/receive-money/new';
    void this.router.navigate([route]);
  }

  openDocument(documentId: number): void {
    const basePath = this.direction() === 'spend' ? '/banking/spend-money' : '/banking/receive-money';
    void this.router.navigate([basePath, documentId]);
  }

  contactName(contactId: number | null): string {
    if (contactId === null) return '—';
    return this.contacts().find((c) => c.contactId === contactId)?.displayName ?? `#${contactId}`;
  }

  private get typeCode(): string {
    return this.direction() === 'spend' ? 'SPM' : 'RCM';
  }

  protected canAllocate(row: MoneyDocumentListItem): boolean {
    return row.status === 'Posted' && row.contactId !== null;
  }

  protected async allocate(row: MoneyDocumentListItem, event?: Event): Promise<void> {
    event?.stopPropagation();

    if (row.contactId === null) {
      return;
    }

    this.allocateOpen.set(true);
    this.allocateLoading.set(true);
    this.allocateMessages.set([]);
    this.allocateRows.set([]);
    this.allocateTarget.set(null);

    try {
      const open = await this.allocations.openDocuments(row.contactId);
      const located = locateDocument(open, this.typeCode, row.documentId);

      if (!located) {
        this.allocateMessages.set([
          {
            tone: 'warning',
            text: `${row.transactionNo ?? 'This payment'} has nothing left to apply.`,
          },
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

  /** One claim at a time, so they cannot race each other for the same balance. */
  protected async onAllocate(submission: AllocationSubmission): Promise<void> {
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
      await this.load();
    } catch (error) {
      const failure = readApiFailure(error);
      this.allocateMessages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
    } finally {
      this.allocateSaving.set(false);
    }
  }

  async voidDocument(row: MoneyDocumentListItem): Promise<void> {
    const reason = prompt(
      `Void ${row.transactionNo}? Its ledger rows are withdrawn; the document and its number stay.`,
      '',
    );
    if (reason === null) return;

    this.busy.set(true);
    try {
      await this.req('POST', `${this.endpoint}/${row.documentId}/void`, {
        reason: reason || null,
      });
      this.show('Voided.', false);
      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not void the document.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async discard(row: MoneyDocumentListItem): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Delete draft?',
      message: 'Delete this draft? Everything keyed into it will be lost.',
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      await this.req('DELETE', `${this.endpoint}/${row.documentId}`);
      this.show('Draft deleted.', false);
      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not delete the draft.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  private async req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise<T>((resolve, reject) => {
      this.http.request<T>(method, url, { body }).subscribe({
        next: (val) => resolve(val),
        error: (err) => reject(err),
      });
    });
  }

  private show(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }

  private reason(err: unknown, fallback: string): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const e = (err as { error: unknown }).error;
      if (typeof e === 'string') return e;
      if (e && typeof e === 'object' && 'message' in e) {
        return String((e as { message: unknown }).message);
      }
    }
    return fallback;
  }
}
