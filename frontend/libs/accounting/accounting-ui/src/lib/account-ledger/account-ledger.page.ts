import { DataGridComponent, ColumnDef , DateInputComponent } from '@bill-book/ui-components';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

interface AccountLedgerRow {
  ledgerId: number;
  ledgerDate: string;
  transactionTypeCode: string;
  transactionId: number;
  transactionDetailId: number;
  journalId: number | null;
  journalNo: string | null;
  ledgerTypeId: number;
  ledgerSourceId: number;
  subAccountId: number | null;
  subAccountName: string | null;
  contactId: number | null;
  transactionDesc: string | null;
  debitAmount: number;
  creditAmount: number;
  debitAmountBase: number;
  creditAmountBase: number;
  currencyCode: string;
  exchangeRate: number;
  runningBalance: number;
}

interface AccountLedger {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
  isContra: boolean;
  openingBalance: number;
  closingBalance: number;
  totalDebit: number;
  totalCredit: number;
  rows: AccountLedgerRow[];
}

interface AccountOption {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
}

interface AccountType {
  accountTypeId: number;
  displayName: string;
  normalBalance: string;
}

/**
 * One account's ledger: what it stood at when the range opened, every posting
 * inside it, and a running balance down the page.
 *
 * The server returns balances in debit terms — positive is a debit balance. This
 * page turns that the right way up for a credit-balance account, because
 * "Accounts Payable: −40,000" is a number every accountant has to stop and
 * translate.
 */
@Component({
  selector: 'bb-account-ledger-page',
  standalone: true,
  imports: [DataGridComponent, DecimalPipe, FormsModule, RouterLink, DateInputComponent],
  templateUrl: './account-ledger.page.html',
  styleUrl: './account-ledger.page.scss',
})
export class AccountLedgerPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly ledger = signal<AccountLedger | null>(null);
  protected readonly accounts = signal<AccountOption[]>([]);
  protected readonly types = signal<AccountType[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);

  accountId: number | null = null;
  from = '';
  to = '';

  columns: ColumnDef[] = [
    { field: 'ledgerDate', header: 'Date', isTemplate: true },
    { field: 'document', header: 'Document', isTemplate: true },
    { field: 'detail', header: 'Detail', isTemplate: true },
    { field: 'debit', header: 'Debit', type: 'number', isTemplate: true },
    { field: 'credit', header: 'Credit', type: 'number', isTemplate: true },
    { field: 'balance', header: 'Balance', type: 'number', isTemplate: true }
  ];

  /**
   * Which way up this account reads. An asset or an expense grows on the debit
   * side; everything else grows on the credit side, so its balance is shown as
   * a positive credit rather than a negative debit.
   */
  protected readonly normalBalance = computed(() => {
    const account = this.ledger();
    if (!account) {
      return 'Debit';
    }

    const type = this.types().find((t) => t.accountTypeId === account.accountTypeId);
    return type?.normalBalance ?? 'Debit';
  });

  ngOnInit(): void {
    // The account can arrive in the URL — the trial balance links straight here,
    // and a ledger worth reading is a ledger worth sending someone.
    const routed = Number(this.route.snapshot.paramMap.get('accountId'));
    this.accountId = Number.isFinite(routed) && routed > 0 ? routed : null;
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      if (this.accounts().length === 0) {
        const [accounts, types] = await Promise.all([
          this.req<AccountOption[]>('GET', '/api/accounts'),
          this.req<AccountType[]>('GET', '/api/master/account-types'),
        ]);
        this.accounts.set(accounts);
        this.types.set(types);
      }

      if (this.accountId === null) {
        this.ledger.set(null);
        return;
      }

      this.ledger.set(
        await this.req<AccountLedger>(
          'GET',
          `/api/ledger/accounts/${this.accountId}${this.range()}`,
        ),
      );
    } catch {
      this.ledger.set(null);
      this.message.set('Could not load the ledger for that account.');
    } finally {
      this.busy.set(false);
    }
  }

  /** Keeps the URL in step with the picker, so the page can be linked and reloaded. */
  async choose(): Promise<void> {
    await this.router.navigate(
      this.accountId === null ? ['/accounting/ledger'] : ['/accounting/ledger', this.accountId],
      { replaceUrl: true },
    );
    await this.load();
  }

  /**
   * A balance the way the account reads. Positive on its own normal side; a
   * balance on the wrong side keeps its sign, because an overdrawn bank account
   * or a customer in credit is exactly what someone needs to see.
   */
  presented(balance: number): number {
    return this.normalBalance() === 'Credit' ? -balance : balance;
  }

  private range(): string {
    const parts: string[] = [];
    if (this.from) {
      parts.push(`from=${this.from}`);
    }
    if (this.to) {
      parts.push(`to=${this.to}`);
    }
    return parts.length === 0 ? '' : `?${parts.join('&')}`;
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }),
    );
  }
}
