import { CdkDrag, CdkDragDrop, CdkDragHandle, CdkDropList } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

type AccountType =
  | 'Savings'
  | 'Current'
  | 'OverDraft'
  | 'CashCredit'
  | 'CreditCard'
  | 'Wallet'
  | 'Cash';

interface Bank {
  bankId: number;
  bankName: string;
  isActive: boolean;
}

interface BankAccount {
  bankAccountId: number;
  bankId: number | null;
  bankName: string | null;
  ledgerAccountId: number | null;
  ledgerAccountCode: string | null;
  accountName: string;
  accountNumber: string;
  accountType: AccountType;
  ifsc: string | null;
  micr: string | null;
  swiftCode: string | null;
  iban: string | null;
  branchName: string | null;
  currencyCode: string;
  odLimit: number | null;
  isDefault: boolean;
  displayOrder: number;
  isActive: boolean;
  isLedgerLinked: boolean;
}

/**
 * Banking › Bank accounts. Creating one creates its ledger account in
 * Accounting, because only Accounting writes ledger rows and an account with no
 * ledger identity cannot be posted to.
 *
 * When that call fails the account still saves, marked unlinked, and Link ledger
 * retries it — the alternative is losing everything the user typed because
 * another service was briefly down.
 */
@Component({
  selector: 'bb-bank-accounts-page',
  standalone: true,
  imports: [FormsModule, CdkDropList, CdkDrag, CdkDragHandle],
  templateUrl: './bank-accounts.page.html',
  styleUrl: './bank-accounts.page.scss',
})
export class BankAccountsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly rows = signal<BankAccount[]>([]);
  protected readonly banks = signal<Bank[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<number | null>(null);

  showInactive = false;
  form = this.blank();

  ngOnInit(): void {
    void this.load();
    void this.loadBanks();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(
        await this.get<BankAccount[]>(`/api/bank-accounts?includeInactive=${this.showInactive}`),
      );
    } catch {
      this.fail('Could not load bank accounts.');
    } finally {
      this.busy.set(false);
    }
  }

  async loadBanks(): Promise<void> {
    try {
      this.banks.set(await this.get<Bank[]>('/api/banks'));
    } catch {
      // The bank dropdown is empty; cash and wallet accounts still work.
    }
  }

  startAdd(): void {
    this.editingId.set(0);
    this.form = this.blank();
  }

  startEdit(row: BankAccount): void {
    this.editingId.set(row.bankAccountId);
    this.form = {
      bankId: row.bankId,
      accountName: row.accountName,
      accountNumber: row.accountNumber,
      accountType: row.accountType,
      ifsc: row.ifsc,
      micr: row.micr,
      swiftCode: row.swiftCode,
      iban: row.iban,
      branchName: row.branchName,
      currencyCode: row.currencyCode,
      odLimit: row.odLimit,
      isActive: row.isActive,
    };
  }

  /** Cash and wallets have no institution, and only borrowing kinds take a limit. */
  onTypeChange(): void {
    if (this.form.accountType === 'Cash' || this.form.accountType === 'Wallet') {
      this.form.bankId = null;
      this.form.ifsc = null;
    }

    if (!this.canOverdraw) {
      this.form.odLimit = null;
    }
  }

  protected get needsBank(): boolean {
    return this.form.accountType !== 'Cash' && this.form.accountType !== 'Wallet';
  }

  protected get canOverdraw(): boolean {
    return ['OverDraft', 'CashCredit', 'CreditCard'].includes(this.form.accountType);
  }

  /** What the ledger account will be: a liability for anything that can be overdrawn. */
  protected get ledgerNote(): string {
    if (this.form.accountType === 'Cash' || this.form.accountType === 'Wallet') {
      return 'Creates an Asset account under Cash in Hand.';
    }

    return this.canOverdraw
      ? 'Creates a Liability account under Bank OD & Credit Cards — an overdrawn account is borrowing.'
      : 'Creates an Asset account under Bank Accounts.';
  }

  async save(): Promise<void> {
    const id = this.editingId();
    await this.run(async () => {
      if (id === 0) {
        await this.send('POST', '/api/bank-accounts', this.form);
      } else {
        await this.send('PUT', `/api/bank-accounts/${id}`, this.form);
      }
      this.editingId.set(null);
    }, 'Bank account saved.');
  }

  async linkLedger(row: BankAccount): Promise<void> {
    await this.run(
      () => this.send('POST', `/api/bank-accounts/${row.bankAccountId}/link-ledger`, {}),
      'Ledger account linked.',
    );
  }

  async makeDefault(row: BankAccount): Promise<void> {
    await this.run(
      () => this.send('PUT', `/api/bank-accounts/${row.bankAccountId}/default`, {}),
      'Default account changed.',
    );
  }

  async deactivate(row: BankAccount): Promise<void> {
    await this.run(
      () => this.send('DELETE', `/api/bank-accounts/${row.bankAccountId}`, {}),
      'Bank account deactivated.',
    );
  }

  async onDrop(event: CdkDragDrop<BankAccount[]>): Promise<void> {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    const list = [...this.rows()];
    const [moved] = list.splice(event.previousIndex, 1);
    list.splice(event.currentIndex, 0, moved);
    this.rows.set(list);

    await this.run(
      () =>
        this.send('PATCH', '/api/bank-accounts/reorder', {
          movedId: moved.bankAccountId,
          previousId: list[event.currentIndex - 1]?.bankAccountId ?? null,
          nextId: list[event.currentIndex + 1]?.bankAccountId ?? null,
        }),
      null,
    );
  }

  private blank() {
    return {
      bankId: null as number | null,
      accountName: '',
      accountNumber: '',
      accountType: 'Current' as AccountType,
      ifsc: null as string | null,
      micr: null as string | null,
      swiftCode: null as string | null,
      iban: null as string | null,
      branchName: null as string | null,
      currencyCode: 'INR',
      odLimit: null as number | null,
      isActive: true,
    };
  }

  private async run(action: () => Promise<unknown>, ok: string | null): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await action();
      if (ok) {
        this.message.set(ok);
        this.messageIsError.set(false);
      }
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'That did not work.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}
