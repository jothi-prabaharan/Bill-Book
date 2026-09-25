import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import {
  Allocation,
  FeeApiService,
  FeeDemand,
  FeeReceipt,
  PaymentMode,
  SaveReceipt,
  allocationProblem,
  autoAllocate,
  unallocated,
} from '@bill-book/fee-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  MessageBoxComponent,
  MoneyInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * Fees › Fee receipts (S4, TK-64): money from a guardian, settling their open
 * demands (the oldest first, or as chosen). What is left over is kept as
 * their advance. A receipt is voided, never edited.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-fee-receipts-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    SelectComponent,
    DateInputComponent,
    MoneyInputComponent,
    TextInputComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './receipts.page.html',
  styleUrl: '../fee-page.scss',
})
export class ReceiptsPage implements OnInit {
  private readonly api = inject(FeeApiService);
  private readonly http = inject(HttpClient);

  protected readonly guardians = signal<BbSelectOption<number>[]>([]);
  protected readonly bankAccounts = signal<BbSelectOption<number>[]>([]);
  protected readonly open = signal<FeeDemand[]>([]);
  protected readonly receipts = signal<FeeReceipt[]>([]);
  protected readonly allocations = signal<Allocation[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly taking = signal(false);
  protected readonly voiding = signal<FeeReceipt | null>(null);

  protected form: SaveReceipt = ReceiptsPage.blank();
  protected voidReason = '';

  protected readonly advance = computed(() => unallocated(this.form.amount, this.allocations()));

  protected readonly modes: BbSelectOption<PaymentMode>[] = [
    { value: 'Cash', label: 'Cash' },
    { value: 'Upi', label: 'UPI' },
    { value: 'Card', label: 'Card' },
    { value: 'Cheque', label: 'Cheque' },
    { value: 'BankTransfer', label: 'Bank transfer' },
  ];

  protected readonly columns: ColumnDef[] = [
    { field: 'receiptNo', header: 'Receipt' },
    { field: 'receiptDate', header: 'Date' },
    { field: 'contactId', header: 'Guardian' },
    { field: 'amount', header: 'Amount' },
    { field: 'unallocatedAmount', header: 'Advance' },
    { field: 'documentStatus', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected guardianName(id: number): string {
    return this.guardians().find((g) => g.value === id)?.label ?? String(id);
  }

  protected allocationFor(demandId: number): number {
    return this.allocations().find((a) => a.feeDemandId === demandId)?.amount ?? 0;
  }

  protected setAllocation(demandId: number, amount: number | null): void {
    const others = this.allocations().filter((a) => a.feeDemandId !== demandId);
    this.allocations.set(amount && amount > 0 ? [...others, { feeDemandId: demandId, amount }] : others);
  }

  protected startTake(): void {
    this.form = ReceiptsPage.blank();
    this.form.bankAccountId = this.bankAccounts()[0]?.value ?? 0;
    this.open.set([]);
    this.allocations.set([]);
    this.taking.set(true);
  }

  protected async guardianChosen(contactId: number | null): Promise<void> {
    this.open.set([]);
    this.allocations.set([]);
    if (!contactId) {
      return;
    }

    try {
      this.open.set(await this.api.demands({ contactId, open: true }));
      this.receipts.set(await this.api.receipts(contactId));
    } catch (error) {
      this.fail(error);
    }
  }

  protected oldestFirst(): void {
    this.allocations.set(autoAllocate(this.form.amount, this.open()));
  }

  protected async take(): Promise<void> {
    const problem = allocationProblem(this.form.amount, this.allocations(), this.open());
    if (!this.form.contactId || !this.form.bankAccountId || !(this.form.amount > 0) || problem) {
      this.messages.set([{ tone: 'error', text: problem ?? 'Choose the guardian and the account, and give the amount.' }]);
      return;
    }

    this.busy.set(true);
    try {
      const receipt = await this.api.createReceipt({ ...this.form, allocations: this.allocations() });
      this.messages.set([{ tone: 'success', text: `Receipt ${receipt.receiptNo} saved.` }]);
      this.taking.set(false);
      this.receipts.set(await this.api.receipts(this.form.contactId));
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  protected startVoid(row: FeeReceipt): void {
    this.voiding.set(row);
    this.voidReason = '';
  }

  protected async confirmVoid(): Promise<void> {
    const row = this.voiding();
    if (!row || !this.voidReason.trim()) {
      this.messages.set([{ tone: 'error', text: 'Give a reason to void.' }]);
      return;
    }

    this.busy.set(true);
    try {
      await this.api.voidReceipt(row.feeReceiptId, this.voidReason);
      this.voiding.set(null);
      this.messages.set([{ tone: 'success', text: 'The receipt is void.' }]);
      this.receipts.set(await this.api.receipts(row.contactId));
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    try {
      const [guardians, accounts, receipts] = await Promise.all([
        firstValueFrom(this.http.get<{ contactId: number; displayName: string; contactCode: string }[]>('/api/contacts', { params: { role: 'guardian' } })),
        this.api.bankAccounts(),
        this.api.receipts(),
      ]);
      this.guardians.set(guardians.map((g) => ({ value: g.contactId, label: `${g.displayName} (${g.contactCode})` })));
      this.bankAccounts.set(accounts.map((a) => ({ value: a.bankAccountId, label: `${a.accountName} ${a.maskedNumber}` })));
      this.receipts.set(receipts);
    } catch (error) {
      this.fail(error);
    }
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private static blank(): SaveReceipt {
    return {
      contactId: 0,
      receiptDate: new Date().toISOString().slice(0, 10),
      paymentMode: 'Cash',
      bankAccountId: 0,
      amount: 0,
      reference: null,
      allocations: [],
    };
  }
}
