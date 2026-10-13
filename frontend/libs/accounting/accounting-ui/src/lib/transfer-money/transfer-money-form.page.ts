import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  BbSelectOption,
  BranchDateInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { PAYMENT_METHODS } from '../money-document/money-sources';

interface BankAccountOption {
  bankAccountId: number;
  accountName: string;
  currencyCode: string;
  isDefault: boolean;
  isActive: boolean;
  isLedgerLinked: boolean;
}

interface TransferDetailView {
  documentId: number;
  transactionNo: string;
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
}

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-transfer-money-form-page',
  standalone: true,
  imports: [
    FormsModule,
    BranchDateInputComponent,
    TextInputComponent,
    NumberInputComponent,
    SelectComponent,
    MessageBoxComponent,
  ],
  templateUrl: './transfer-money-form.page.html',
  styleUrl: './transfer-money-form.page.scss',
})
export class TransferMoneyFormPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly bankAccounts = signal<BankAccountOption[]>([]);
  protected readonly viewing = signal<TransferDetailView | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  protected editingId: number | null = null;

  transactionDate = new Date().toISOString().slice(0, 10);
  fromBankAccountId: number | null = null;
  toBankAccountId: number | null = null;
  amount = '';
  paymentMethod = 2;
  referenceNo = '';
  referenceDate = '';
  memo = '';

  protected readonly paymentMethods = PAYMENT_METHODS;

  protected readonly usableAccounts = computed(() =>
    this.bankAccounts().filter((a) => a.isActive && a.isLedgerLinked),
  );

  protected readonly bankAccountOptions = computed<BbSelectOption<number>[]>(() =>
    this.usableAccounts().map((a) => ({
      value: a.bankAccountId,
      label: a.accountName,
    })),
  );

  protected get sameAccount(): boolean {
    return (
      this.fromBankAccountId !== null &&
      this.toBankAccountId !== null &&
      this.fromBankAccountId === this.toBankAccountId
    );
  }

  protected get canPost(): boolean {
    return (
      this.fromBankAccountId !== null &&
      this.toBankAccountId !== null &&
      !this.sameAccount &&
      this.money(this.amount) > 0
    );
  }

  ngOnInit(): void {
    void this.init();
  }

  private async init(): Promise<void> {
    await this.loadAccounts();

    const idParam = this.route.snapshot.paramMap.get('id');
    if (idParam && idParam !== 'new') {
      const docId = Number(idParam);
      if (!isNaN(docId)) {
        await this.open(docId);
        return;
      }
    }

    this.startCreate();
  }

  private async loadAccounts(): Promise<void> {
    try {
      this.bankAccounts.set(await this.req<BankAccountOption[]>('GET', '/api/bank-accounts'));
    } catch {
      this.show('Could not load bank accounts.', true);
    }
  }

  startCreate(): void {
    this.editingId = null;
    this.viewing.set(null);
    this.transactionDate = new Date().toISOString().slice(0, 10);
    this.fromBankAccountId =
      this.usableAccounts().find((a) => a.isDefault)?.bankAccountId ??
      this.usableAccounts()[0]?.bankAccountId ??
      null;
    this.toBankAccountId =
      this.usableAccounts().find((a) => a.bankAccountId !== this.fromBankAccountId)?.bankAccountId ??
      null;
    this.amount = '';
    this.paymentMethod = 2;
    this.referenceNo = '';
    this.referenceDate = '';
    this.memo = '';
  }

  async open(documentId: number): Promise<void> {
    try {
      const document = await this.req<TransferDetailView>('GET', `/api/transfer-money/${documentId}`);
      if (document.status !== 'Draft') {
        this.viewing.set(document);
        this.editingId = null;
        return;
      }

      this.editingId = document.documentId;
      this.viewing.set(null);
      this.transactionDate = document.transactionDate;
      this.fromBankAccountId = document.bankAccountId;
      this.toBankAccountId = document.toBankAccountId;
      this.amount = String(document.amount);
      this.paymentMethod =
        this.paymentMethods.find((m) => m.label.toLowerCase() === document.paymentMethod.toLowerCase())
          ?.value ?? 2;
      this.referenceNo = document.referenceNo ?? '';
      this.referenceDate = '';
      this.memo = document.memo ?? '';
    } catch {
      this.show('Could not open transfer document.', true);
    }
  }

  swap(): void {
    const tmp = this.fromBankAccountId;
    this.fromBankAccountId = this.toBankAccountId;
    this.toBankAccountId = tmp;
  }

  goBack(): void {
    void this.router.navigate(['/banking/transfer-money']);
  }

  async save(post: boolean): Promise<void> {
    if (!this.validateSave(post)) return;

    this.busy.set(true);
    try {
      const body = {
        transactionDate: this.transactionDate,
        bankAccountId: this.fromBankAccountId,
        toBankAccountId: this.toBankAccountId,
        amount: this.money(this.amount),
        currencyCode: null,
        exchangeRate: null,
        paymentMethod: this.paymentMethod,
        referenceNo: this.referenceNo || null,
        referenceDate: this.referenceDate || null,
        memo: this.memo || null,
      };

      let id: number;
      if (this.editingId === null) {
        const created = await this.req<{ id: number }>('POST', '/api/transfer-money', body);
        id = created.id;
      } else {
        await this.req('PUT', `/api/transfer-money/${this.editingId}`, body);
        id = this.editingId;
      }

      if (post) {
        await this.req('POST', `/api/transfer-money/${id}/post`);
        this.goBack();
      } else {
        this.editingId = id;
        this.show('Draft saved.', false);
      }
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not save the transfer.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async voidDocument(d: TransferDetailView): Promise<void> {
    const reason = prompt(
      `Void ${d.transactionNo}? Its ledger rows are withdrawn; the document and its number stay.`,
      '',
    );
    if (reason === null) return;

    this.busy.set(true);
    try {
      await this.req('POST', `/api/transfer-money/${d.documentId}/void`, {
        reason: reason || null,
      });
      this.goBack();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not void the document.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  private validateSave(post: boolean): boolean {
    if (this.fromBankAccountId === null) {
      this.show('Choose the account the money came from.', true);
      return false;
    }
    if (this.toBankAccountId === null) {
      this.show('Choose the account the money went to.', true);
      return false;
    }
    if (this.sameAccount) {
      this.show('The two ends must be different accounts.', true);
      return false;
    }
    if (this.money(this.amount) <= 0) {
      this.show('Amount must be greater than zero.', true);
      return false;
    }
    if (post && !this.canPost) {
      this.show('Fill all required fields to post the transfer.', true);
      return false;
    }
    return true;
  }

  private money(val: string | number): number {
    const n = typeof val === 'number' ? val : parseFloat(val);
    return isNaN(n) ? 0 : Math.round(n * 100) / 100;
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
