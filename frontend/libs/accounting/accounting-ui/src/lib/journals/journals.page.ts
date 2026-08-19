import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DataGridComponent, ColumnDef , DateInputComponent , TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';

interface JournalListItem {
  journalId: number;
  journalNo: string | null;
  journalDate: string;
  currencyCode: string;
  exchangeRate: number;
  reference: string | null;
  memo: string | null;
  status: 'Draft' | 'Posted' | 'Reversed';
  postedAt: string | null;
  lineCount: number;
  totalDebit: number;
  totalCredit: number;
  reversesJournalId: number | null;
  reversesJournalNo: string | null;
  reversedByJournalId: number | null;
  reversedByJournalNo: string | null;
}

interface JournalLineView {
  journalDetailId: number;
  lineNumber: number;
  accountId: number;
  accountCode: string;
  accountName: string;
  subAccountId: number | null;
  subAccountName: string | null;
  debitAmount: number;
  creditAmount: number;
  lineMemo: string | null;
  reversesJournalDetailId: number | null;
  reversedByJournalDetailId: number | null;
}

interface JournalDetailView extends JournalListItem {
  lines: JournalLineView[];
}

interface AccountOption {
  accountId: number;
  accountCode: string;
  accountName: string;
  isSystemDefault: boolean;
  isJE: boolean;
  isLock: boolean;
  isActive: boolean;
}

interface SubAccountOption {
  subAccountId: number;
  accountId: number;
  subAccountName: string;
  isActive: boolean;
}

/** A line being keyed. Amounts are strings so an empty box stays empty. */
interface LineForm {
  accountId: number | null;
  subAccountId: number | null;
  debit: string;
  credit: string;
  lineMemo: string;
}

/**
 * Accounting › Journal entries. The list and the entry form on one page: a
 * journal is short enough that pushing it to a second screen buys nothing.
 *
 * The totals panel shows debits, credits and the difference between them
 * continuously. That difference is the whole reason this screen is not a form
 * with a save button — an entry is wrong until it is zero, and finding that out
 * at save time means finding it out after the typing is done.
 */
@Component({
  selector: 'bb-journals-page',
  standalone: true,
  imports: [DataGridComponent, DecimalPipe, FormsModule, RouterLink, DateInputComponent, TextInputComponent, NumberInputComponent],
  templateUrl: './journals.page.html',
  styleUrl: './journals.page.scss',
})
export class JournalsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  columns: ColumnDef[] = [
    { field: 'journalDate', header: 'Date' },
    { field: 'journalNo', header: 'Number' },
    { field: 'reference', header: 'Reference' },
    { field: 'totalDebit', header: 'Debit' },
    { field: 'totalCredit', header: 'Credit' },
    { field: 'status', header: 'Status' },
    { field: 'actions', header: 'Actions' }
  ];

  editLinesColumns: ColumnDef[] = [
    { field: 'lineNumber', header: '#' },
    { field: 'account', header: 'Account' },
    { field: 'subAccount', header: 'Sub-account' },
    { field: 'memo', header: 'Memo' },
    { field: 'debit', header: 'Debit' },
    { field: 'credit', header: 'Credit' },
    { field: 'actions', header: 'Actions' }
  ];

  viewLinesColumns: ColumnDef[] = [
    { field: 'lineNumber', header: '#' },
    { field: 'account', header: 'Account' },
    { field: 'memo', header: 'Memo' },
    { field: 'debit', header: 'Debit' },
    { field: 'credit', header: 'Credit' }
  ];

  protected readonly rows = signal<JournalListItem[]>([]);
  protected readonly accounts = signal<AccountOption[]>([]);
  protected readonly subAccounts = signal<SubAccountOption[]>([]);
  protected readonly editing = signal(false);
  protected readonly viewing = signal<JournalDetailView | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  statusFilter = '';
  protected editingId: number | null = null;

  journalDate = new Date().toISOString().slice(0, 10);
  reference = '';
  memo = '';

  /**
   * A signal, not a plain array, and that is the difference between a running
   * total and a total computed once. `computed` tracks signals; a field it can
   * read but not watch would leave the difference panel frozen at whatever the
   * first two lines said — which is the one thing this screen exists to show.
   *
   * ngModel writes into the line objects in place, so every input also calls
   * `touch()` to replace the array and let the computeds run again.
   */
  protected readonly lines = signal<LineForm[]>([]);

  /**
   * Only accounts a hand entry may target. A seeded control account is driven by
   * its own subledger, so offering it here would let someone put the control
   * account and its subledger out of step with nothing to reconcile them — the
   * server refuses those, and a picker that offers what the server refuses is a
   * screen arguing with itself.
   */
  protected readonly postable = computed(() =>
    this.accounts().filter((a) => a.isActive && !a.isLock && (!a.isSystemDefault || a.isJE)),
  );

  protected readonly totalDebit = computed(() =>
    this.lines().reduce((sum, l) => sum + this.amount(l.debit), 0),
  );

  protected readonly totalCredit = computed(() =>
    this.lines().reduce((sum, l) => sum + this.amount(l.credit), 0),
  );

  protected readonly difference = computed(() => this.totalDebit() - this.totalCredit());

  /**
   * A journal with no lines is not balanced, it is empty — hence the third
   * condition. The tolerance is half a paisa: the amounts are keyed to two
   * decimals, and comparing floating point for exact equality would refuse an
   * entry that foots perfectly.
   */
  protected readonly canPost = computed(
    () =>
      this.lines().some((l) => l.accountId !== null) &&
      Math.abs(this.difference()) < 0.005 &&
      this.totalDebit() > 0,
  );

  ngOnInit(): void {
    const routed = Number(this.route.snapshot.paramMap.get('journalId'));
    void this.load(Number.isFinite(routed) && routed > 0 ? routed : null);
  }

  async load(openId: number | null = null): Promise<void> {
    this.busy.set(true);
    try {
      const query = this.statusFilter ? `?status=${this.statusFilter}` : '';
      this.rows.set(await this.req<JournalListItem[]>('GET', `/api/journals${query}`));

      if (this.accounts().length === 0) {
        const [accounts, subs] = await Promise.all([
          this.req<AccountOption[]>('GET', '/api/accounts'),
          this.req<SubAccountOption[]>('GET', '/api/sub-accounts'),
        ]);
        this.accounts.set(accounts);
        this.subAccounts.set(subs);
      }

      if (openId !== null) {
        await this.open(openId);
      }
    } catch {
      this.show('Could not load journal entries.', true);
    } finally {
      this.busy.set(false);
    }
  }

  /** The sub-accounts that hang from a line's own account, and only those. */
  subAccountsFor(accountId: number | null): SubAccountOption[] {
    return accountId === null
      ? []
      : this.subAccounts().filter((s) => s.accountId === accountId && s.isActive);
  }

  startCreate(): void {
    this.editingId = null;
    this.viewing.set(null);
    this.journalDate = new Date().toISOString().slice(0, 10);
    this.reference = '';
    this.memo = '';
    // Two lines to begin with, because the smallest entry that means anything
    // is a debit and a credit.
    this.lines.set([this.blankLine(), this.blankLine()]);
    this.editing.set(true);
  }

  async open(journalId: number): Promise<void> {
    const journal = await this.req<JournalDetailView>('GET', `/api/journals/${journalId}`);

    if (journal.status === 'Draft') {
      this.editingId = journal.journalId;
      this.viewing.set(null);
      this.journalDate = journal.journalDate;
      this.reference = journal.reference ?? '';
      this.memo = journal.memo ?? '';
      this.lines.set(
        journal.lines.map((l) => ({
          accountId: l.accountId,
          subAccountId: l.subAccountId,
          debit: l.debitAmount ? String(l.debitAmount) : '',
          credit: l.creditAmount ? String(l.creditAmount) : '',
          lineMemo: l.lineMemo ?? '',
        })),
      );
      this.editing.set(true);
      return;
    }

    // Posted or reversed: read-only, because a posted entry is never edited.
    this.editing.set(false);
    this.viewing.set(journal);
  }

  close(): void {
    this.editing.set(false);
    this.viewing.set(null);
    this.editingId = null;
    void this.router.navigate(['/accounting/journals'], { replaceUrl: true });
  }

  addLine(): void {
    this.lines.update((lines) => [...lines, this.blankLine()]);
  }

  removeLine(index: number): void {
    this.lines.update((lines) => lines.filter((_, i) => i !== index));
  }

  /**
   * Re-publishes the array after ngModel has written into one of its objects in
   * place. Without it the totals below would not move as the entry is keyed.
   */
  touch(): void {
    this.lines.update((lines) => [...lines]);
  }

  /**
   * A line is a debit or a credit, never both. Typing in one box clears the
   * other rather than letting a line be keyed that the server will refuse.
   */
  onDebit(line: LineForm): void {
    if (this.amount(line.debit) > 0) {
      line.credit = '';
    }
    this.touch();
  }

  onCredit(line: LineForm): void {
    if (this.amount(line.credit) > 0) {
      line.debit = '';
    }
    this.touch();
  }

  onAccount(line: LineForm): void {
    // The old sub-account belonged to the old account, and hanging it under the
    // new one is exactly what the server refuses.
    line.subAccountId = null;
    this.touch();
  }

  async save(post: boolean): Promise<void> {
    this.busy.set(true);
    try {
      const body = {
        journalDate: this.journalDate,
        reference: this.reference || null,
        memo: this.memo || null,
        // An empty row left behind by "add line" is not an error to complain
        // about, it is a row the user did not fill in.
        lines: this.lines()
          .filter((l) => l.accountId !== null)
          .map((l) => ({
            accountId: l.accountId,
            subAccountId: l.subAccountId,
            debitAmount: this.amount(l.debit),
            creditAmount: this.amount(l.credit),
            lineMemo: l.lineMemo || null,
          })),
      };

      let id: number;

      if (this.editingId === null) {
        const created = await this.req<{ journalId: number }>('POST', '/api/journals', body);
        id = created.journalId;
      } else {
        await this.req('PUT', `/api/journals/${this.editingId}`, body);
        id = this.editingId;
      }

      if (post) {
        await this.req('POST', `/api/journals/${id}/post`);
        this.show('Entry posted.', false);
        this.editing.set(false);
        this.editingId = null;
      } else {
        this.editingId = id;
        this.show('Draft saved.', false);
      }

      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not save the entry.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async reverse(journal: JournalListItem): Promise<void> {
    if (
      !confirm(
        `Reverse ${journal.journalNo}? An offsetting entry is created — both stay in the ledger.`,
      )
    ) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('POST', `/api/journals/${journal.journalId}/reverse`, {
        reversalDate: null,
        reference: null,
        memo: null,
      });
      this.show('Reversing entry posted.', false);
      this.viewing.set(null);
      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not reverse the entry.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async discard(journal: JournalListItem): Promise<void> {
    if (!confirm('Delete this draft?')) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `/api/journals/${journal.journalId}`);
      this.editing.set(false);
      this.editingId = null;
      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not delete the draft.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  /** Parses a typed amount. Anything unparseable is zero, never NaN in a total. */
  private amount(raw: string): number {
    const parsed = Number(raw);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
  }

  private blankLine(): LineForm {
    return { accountId: null, subAccountId: null, debit: '', credit: '', lineMemo: '' };
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
