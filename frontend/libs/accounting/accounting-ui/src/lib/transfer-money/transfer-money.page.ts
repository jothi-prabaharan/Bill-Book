import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PAYMENT_METHODS } from '../money-document/money-sources';

interface BankAccountOption {
  bankAccountId: number;
  accountName: string;
  currencyCode: string;
  isDefault: boolean;
  isActive: boolean;
  isLedgerLinked: boolean;
}

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

/**
 * Banking › Transfer money. Money moving between the organization's own accounts
 * — a till banked, a current account topped up from savings.
 *
 * <b>The one money document that settles nothing.</b> No contact, no allocation
 * lines, no sub-ledger: both sides are the organization's own accounts, so there
 * is no counterparty balance to point at. That is why this is its own screen
 * rather than a third mode of the payment screen — everything that makes that
 * one complicated is absent here, and a shared screen would carry the complexity
 * anyway.
 */
@Component({
  selector: 'bb-transfer-money-page',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './transfer-money.page.html',
  styleUrl: './transfer-money.page.scss',
})
export class TransferMoneyPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly paymentMethods = PAYMENT_METHODS;

  protected readonly rows = signal<TransferListItem[]>([]);
  protected readonly bankAccounts = signal<BankAccountOption[]>([]);
  protected readonly editing = signal(false);
  protected readonly viewing = signal<TransferListItem | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  statusFilter = '';
  protected editingId: number | null = null;

  transactionDate = new Date().toISOString().slice(0, 10);
  fromBankAccountId: number | null = null;
  toBankAccountId: number | null = null;
  amount = '';
  paymentMethod = 2;
  referenceNo = '';
  referenceDate = '';
  memo = '';

  /**
   * Only accounts money can actually move through. One whose ledger account was
   * never provisioned would be refused at post, and offering it here means
   * finding that out after the typing is done.
   */
  protected readonly usableAccounts = computed(() =>
    this.bankAccounts().filter((a) => a.isActive && a.isLedgerLinked),
  );

  /**
   * A getter rather than a computed: every field it reads is a plain property
   * that ngModel writes, and a computed over those would cache its first answer
   * and never recover.
   */
  protected get canPost(): boolean {
    return (
      this.fromBankAccountId !== null &&
      this.toBankAccountId !== null &&
      this.fromBankAccountId !== this.toBankAccountId &&
      this.money(this.amount) > 0
    );
  }

  /** The refusal the server would give, said before the button is pressed. */
  protected get sameAccount(): boolean {
    return (
      this.fromBankAccountId !== null && this.fromBankAccountId === this.toBankAccountId
    );
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const query = this.statusFilter ? `?status=${this.statusFilter}` : '';
      this.rows.set(await this.req<TransferListItem[]>('GET', `/api/transfer-money${query}`));

      if (this.bankAccounts().length === 0) {
        this.bankAccounts.set(await this.req<BankAccountOption[]>('GET', '/api/bank-accounts'));
      }
    } catch {
      this.show('Could not load transfers.', true);
    } finally {
      this.busy.set(false);
    }
  }

  startCreate(): void {
    this.editingId = null;
    this.viewing.set(null);
    this.transactionDate = new Date().toISOString().slice(0, 10);
    this.fromBankAccountId =
      this.usableAccounts().find((a) => a.isDefault)?.bankAccountId ?? null;
    this.toBankAccountId = null;
    this.amount = '';
    this.paymentMethod = 2;
    this.referenceNo = '';
    this.referenceDate = '';
    this.memo = '';
    this.editing.set(true);
  }

  async open(documentId: number): Promise<void> {
    const document = await this.req<TransferListItem>('GET', `/api/transfer-money/${documentId}`);

    if (document.status !== 'Draft') {
      this.editing.set(false);
      this.viewing.set(document);
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
    this.editing.set(true);
  }

  close(): void {
    this.editing.set(false);
    this.viewing.set(null);
    this.editingId = null;
  }

  /** Swaps the two ends, which is the correction people reach for most often. */
  swap(): void {
    const from = this.fromBankAccountId;
    this.fromBankAccountId = this.toBankAccountId;
    this.toBankAccountId = from;
  }

  async save(post: boolean): Promise<void> {
    if (!this.validateSave(post)) {
      return;
    }

    this.busy.set(true);
    try {
      const body = {
        transactionDate: this.transactionDate,
        fromBankAccountId: this.fromBankAccountId,
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
        this.show('Transfer posted.', false);
        this.close();
      } else {
        this.editingId = id;
        this.show('Draft saved.', false);
      }

      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not save the transfer.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async voidDocument(row: TransferListItem): Promise<void> {
    const reason = prompt(
      `Void ${row.transactionNo}? Its ledger rows are withdrawn; the document and its number stay.`,
      '',
    );

    if (reason === null) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('POST', `/api/transfer-money/${row.documentId}/void`, {
        reason: reason || null,
      });
      this.show('Transfer voided.', false);
      this.viewing.set(null);
      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not void the transfer.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async discard(row: TransferListItem): Promise<void> {
    if (!confirm('Delete this draft?')) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/transfer-money/${row.documentId}`);
      this.close();
      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not delete the draft.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  private validateSave(post: boolean): boolean {
    if (this.fromBankAccountId === null) {
      this.show('Choose the account the money leaves.', true);
      return false;
    }

    if (this.toBankAccountId === null) {
      this.show('Choose the account the money arrives in.', true);
      return false;
    }

    if (this.fromBankAccountId === this.toBankAccountId) {
      this.show('From and To accounts must be different.', true);
      return false;
    }

    if (this.money(this.amount) <= 0) {
      this.show('Amount must be greater than zero.', true);
      return false;
    }

    if (post && !this.canPost) {
      this.show('Fill all mandatory fields before posting.', true);
      return false;
    }

    return true;
  }

  private money(raw: string): number {
    const parsed = Number(raw);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
  }

  private reason(err: unknown, fallback: string): string {
    return (err as { error?: { message?: string } })?.error?.message ?? fallback;
  }

  private show(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
