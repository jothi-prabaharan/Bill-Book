import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import {
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
  countStatusTabs,
  isWithin,
  rangeForPreset,
  registerStatusTone,
} from '@bill-book/ui-components';

interface TransferListItem {
  documentId: number;
  transactionNo: string | null;
  transactionDate: string;
  bankAccountId: number;
  bankAccountName: string;
  toBankAccountId: number | null;
  toBankAccountName: string | null;
  amount: number;
  currencyCode: string;
  exchangeRate: number;
  paymentMethod: string;
  referenceNo: string | null;
  memo: string | null;
  status: 'Draft' | 'Posted' | 'Void';
  postedAt: string | null;
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-transfer-money-list-page',
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
  ],
  host: { class: 'register-page' },
  templateUrl: './transfer-money-list.page.html',
  styleUrl: './transfer-money-list.page.scss',
})
export class TransferMoneyListPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly confirmDialog = inject(ConfirmDialogService);

  columns: ColumnDef[] = [
    { field: 'transactionNo', header: 'Number' },
    { field: 'transactionDate', header: 'Date', dataType: 'date' },
    { field: 'bankAccountName', header: 'From' },
    { field: 'toBankAccountName', header: 'To' },
    { field: 'status', header: 'Status' },
    { field: 'amount', header: 'Amount', dataType: 'money' },
    { field: 'actions', header: '', sortable: false, filterable: false },
  ];

  protected readonly rows = signal<TransferListItem[]>([]);
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
  protected readonly statusTone = registerStatusTone;

  /** Every transfer in the period, before the status tab narrows it. */
  protected readonly periodRows = computed(() =>
    this.rows().filter((row) => isWithin(this.range(), row.transactionDate)),
  );

  protected readonly tabs = computed(() => countStatusTabs(this.periodRows(), this.statuses, (row) => row.status));

  protected readonly visibleRows = computed(() => {
    const status = this.statusFilter();
    return this.periodRows().filter((row) => !status || row.status === status);
  });

  protected readonly periodTotal = computed(() =>
    this.visibleRows().reduce((sum, r) => sum + r.amount, 0),
  );

  ngOnInit(): void {
    if (this.route.snapshot.queryParamMap.get('action') === 'create') {
      this.createTransfer();
      return;
    }
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.req<TransferListItem[]>('GET', '/api/transfer-money'));
    } catch {
      this.show('Could not load transfer documents.', true);
    } finally {
      this.busy.set(false);
    }
  }

  createTransfer(): void {
    void this.router.navigate(['/banking/transfer-money/new']);
  }

  openTransfer(documentId: number): void {
    void this.router.navigate(['/banking/transfer-money', documentId]);
  }

  async voidDocument(row: TransferListItem): Promise<void> {
    const reason = prompt(
      `Void ${row.transactionNo}? Its ledger rows are withdrawn; the document and its number stay.`,
      '',
    );
    if (reason === null) return;

    this.busy.set(true);
    try {
      await this.req('POST', `/api/transfer-money/${row.documentId}/void`, {
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

  async discard(row: TransferListItem): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Delete draft?',
      message: 'Delete this draft? Everything keyed into it will be lost.',
      confirmLabel: 'Delete',
      tone: 'danger',
    });
    if (!ok) return;

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/transfer-money/${row.documentId}`);
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
