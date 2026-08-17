import { CardTableComponent } from '@bill-book/ui-components';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

interface BankAccountOption {
  bankAccountId: number;
  accountName: string;
  currencyCode: string;
  isDefault: boolean;
  isActive: boolean;
}

interface ImportProfile {
  bankAccountId: number;
  skipRows: number;
  hasHeaderRow: boolean;
  dateFormat: string;
  dateColumn: string;
  valueDateColumn: string | null;
  descriptionColumn: string | null;
  referenceColumn: string | null;
  withdrawalColumn: string | null;
  depositColumn: string | null;
  amountColumn: string | null;
  negativeIsDeposit: boolean;
  balanceColumn: string | null;
}

interface StatementListItem {
  bankStatementId: number;
  bankAccountId: number;
  bankAccountName: string;
  fromDate: string;
  toDate: string;
  fileName: string | null;
  importedAt: string;
  lineCount: number;
  matchedCount: number;
  ignoredCount: number;
  isReconciled: boolean;
}

interface MatchCandidate {
  transactionTypeCode: string;
  transactionId: number;
  transactionNo: string | null;
  transactionDate: string;
  amount: number;
  contactId: number | null;
  referenceNo: string | null;
  reason: string;
  isConfident: boolean;
}

interface StatementLine {
  bankStatementLineId: number;
  lineNumber: number;
  transactionDate: string;
  valueDate: string | null;
  description: string | null;
  referenceNo: string | null;
  withdrawalAmount: number;
  depositAmount: number;
  runningBalance: number | null;
  status: 'Unmatched' | 'Matched' | 'Ignored';
  matchedTransactionTypeCode: string | null;
  matchedTransactionId: number | null;
  matchedTransactionNo: string | null;
  matchedAutomatically: boolean;
  note: string | null;
  candidates: MatchCandidate[];
}

/**
 * Banking › Statements. What the bank says happened, against what was recorded.
 *
 * <b>Nothing on this screen posts anything.</b> The money already moved and the
 * document that recorded it already posted. Matching a line says the bank and the
 * books agree about one movement — it is a comparison of two accounts of the same
 * events, not a third set of entries. A reconciliation that posted would double
 * every transaction it touched, and it would look fine until somebody read the
 * trial balance.
 *
 * <b>Unmatched is the working list.</b> A reconciliation is finished when nothing
 * is left undecided, so the screen leads with what still needs a decision rather
 * than with a tidy full statement — the matched lines are the ones nobody needs
 * to look at.
 */
@Component({
  selector: 'bb-statements-page',
  standalone: true,
  imports: [CardTableComponent, DecimalPipe, FormsModule],
  templateUrl: './statements.page.html',
  styleUrl: './statements.page.scss',
})
export class StatementsPage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly accounts = signal<BankAccountOption[]>([]);
  protected readonly statements = signal<StatementListItem[]>([]);
  protected readonly lines = signal<StatementLine[]>([]);
  protected readonly openStatement = signal<StatementListItem | null>(null);
  protected readonly profile = signal<ImportProfile | null>(null);
  protected readonly editingProfile = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  bankAccountId: number | null = null;
  showAll = false;
  file: File | null = null;

  form = this.blankProfile();

  protected readonly usableAccounts = computed(() =>
    this.accounts().filter((a) => a.isActive),
  );

  /**
   * What still needs a decision. Getters rather than computeds because
   * `showAll` is a plain ngModel field — a computed over it would cache its
   * first answer and the toggle would do nothing.
   */
  protected get visibleLines(): StatementLine[] {
    return this.showAll ? this.lines() : this.lines().filter((l) => l.status === 'Unmatched');
  }

  protected get undecided(): number {
    return this.lines().filter((l) => l.status === 'Unmatched').length;
  }

  /** The two amount layouts, and which one the mapping is currently using. */
  protected get usesSignedColumn(): boolean {
    return (this.form.amountColumn ?? '').trim().length > 0;
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const accounts = await this.req<BankAccountOption[]>('GET', '/api/bank-accounts');
      this.accounts.set(accounts);

      this.bankAccountId ??=
        accounts.find((a) => a.isDefault && a.isActive)?.bankAccountId ??
        accounts.find((a) => a.isActive)?.bankAccountId ??
        null;

      await this.refresh();
    } catch {
      this.show('Could not load statements.', true);
    } finally {
      this.busy.set(false);
    }
  }

  async refresh(): Promise<void> {
    const account = this.bankAccountId;
    const query = account === null ? '' : `?bankAccountId=${account}`;

    this.statements.set(await this.req<StatementListItem[]>('GET', `/api/statements${query}`));

    if (account !== null) {
      await this.loadProfile(account);
    }
  }

  /** 404 means this account has no mapping yet, which is the ordinary first state. */
  private async loadProfile(bankAccountId: number): Promise<void> {
    try {
      this.profile.set(
        await this.req<ImportProfile>('GET', `/api/statements/profiles/${bankAccountId}`),
      );
    } catch {
      this.profile.set(null);
    }
  }

  async onAccountChange(): Promise<void> {
    this.openStatement.set(null);
    this.lines.set([]);
    this.editingProfile.set(false);
    await this.refresh();
  }

  startProfile(): void {
    const existing = this.profile();

    this.form = existing
      ? { ...existing }
      : { ...this.blankProfile(), bankAccountId: this.bankAccountId ?? 0 };

    this.form.bankAccountId = this.bankAccountId ?? 0;
    this.editingProfile.set(true);
  }

  /**
   * The two layouts are exclusive, and the server refuses a profile that is
   * both. Clearing the other side as soon as one is typed means the screen never
   * offers a shape the server will reject.
   */
  onAmountShape(): void {
    if (this.usesSignedColumn) {
      this.form.withdrawalColumn = '';
      this.form.depositColumn = '';
    }
  }

  onTwoColumns(): void {
    if ((this.form.withdrawalColumn ?? '').trim() || (this.form.depositColumn ?? '').trim()) {
      this.form.amountColumn = '';
    }
  }

  async saveProfile(): Promise<void> {
    this.busy.set(true);
    try {
      await this.req('PUT', '/api/statements/profiles', {
        ...this.form,
        bankAccountId: this.bankAccountId,
        valueDateColumn: this.form.valueDateColumn || null,
        descriptionColumn: this.form.descriptionColumn || null,
        referenceColumn: this.form.referenceColumn || null,
        withdrawalColumn: this.form.withdrawalColumn || null,
        depositColumn: this.form.depositColumn || null,
        amountColumn: this.form.amountColumn || null,
        balanceColumn: this.form.balanceColumn || null,
      });

      this.editingProfile.set(false);
      this.show('Mapping saved.', false);
      await this.refresh();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not save the mapping.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  onFile(event: Event): void {
    this.file = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  async import(): Promise<void> {
    if (this.file === null || this.bankAccountId === null) {
      return;
    }

    const body = new FormData();
    body.append('bankAccountId', String(this.bankAccountId));
    body.append('file', this.file);

    this.busy.set(true);
    try {
      // FormData, not JSON: these are spreadsheets straight out of a bank
      // portal, and base64-ing one to send it is work with nothing to show.
      const result = await this.req<{ message: string }>('POST', '/api/statements/import', body);

      this.show(result.message, false);
      this.file = null;
      await this.refresh();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not import that file.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async open(statement: StatementListItem): Promise<void> {
    this.busy.set(true);
    try {
      this.openStatement.set(statement);
      this.lines.set(
        await this.req<StatementLine[]>(
          'GET',
          `/api/statements/${statement.bankStatementId}/lines`,
        ),
      );
    } catch {
      this.show('Could not load that statement.', true);
    } finally {
      this.busy.set(false);
    }
  }

  close(): void {
    this.openStatement.set(null);
    this.lines.set([]);
  }

  async match(line: StatementLine, candidate: MatchCandidate): Promise<void> {
    await this.act(
      () =>
        this.req('POST', `/api/statements/lines/${line.bankStatementLineId}/match`, {
          transactionTypeCode: candidate.transactionTypeCode,
          transactionId: candidate.transactionId,
        }),
      'Matched.',
    );
  }

  async unmatch(line: StatementLine): Promise<void> {
    await this.act(
      () => this.req('POST', `/api/statements/lines/${line.bankStatementLineId}/unmatch`),
      'Put back for a decision.',
    );
  }

  async ignore(line: StatementLine): Promise<void> {
    const note = prompt(
      'Why is this line being set aside?\n\nIt stays on the statement with your reason — a ' +
        'line that vanishes cannot be told from one that was never imported.',
      '',
    );

    if (note === null || note.trim().length === 0) {
      return;
    }

    await this.act(
      () =>
        this.req('POST', `/api/statements/lines/${line.bankStatementLineId}/ignore`, {
          note: note.trim(),
        }),
      'Set aside.',
    );
  }

  /** Export goes through the browser rather than fetch, so the file downloads. */
  exportUrl(format: 'csv' | 'xlsx'): string {
    const statement = this.openStatement();
    return statement === null
      ? ''
      : `/api/statements/${statement.bankStatementId}/export?format=${format}`;
  }

  private async act(action: () => Promise<unknown>, ok: string): Promise<void> {
    this.busy.set(true);
    try {
      await action();
      this.show(ok, false);

      const statement = this.openStatement();
      if (statement !== null) {
        this.lines.set(
          await this.req<StatementLine[]>(
            'GET',
            `/api/statements/${statement.bankStatementId}/lines`,
          ),
        );
      }

      this.statements.set(
        await this.req<StatementListItem[]>(
          'GET',
          `/api/statements?bankAccountId=${this.bankAccountId}`,
        ),
      );
    } catch (err: unknown) {
      this.show(this.reason(err, 'That did not work.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  private blankProfile(): ImportProfile {
    return {
      bankAccountId: 0,
      skipRows: 0,
      hasHeaderRow: true,
      dateFormat: 'dd/MM/yyyy',
      dateColumn: '',
      valueDateColumn: '',
      descriptionColumn: '',
      referenceColumn: '',
      withdrawalColumn: '',
      depositColumn: '',
      amountColumn: '',
      negativeIsDeposit: false,
      balanceColumn: '',
    };
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
