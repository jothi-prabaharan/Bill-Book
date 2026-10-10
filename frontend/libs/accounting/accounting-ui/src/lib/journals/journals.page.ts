import { ChangeDetectionStrategy } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ProjectListItem, loadProjects, projectOptions } from '../projects/project-options';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DocNumberComponent,
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
  BranchDateInputComponent,
  MoneyInputComponent,
  SelectComponent,
  TextInputComponent,
  ApprovalPanelComponent,
  EditHistoryComponent,
} from '@bill-book/ui-components';

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
  projectId?: number | null;
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
  /** The project the line belongs to (TK-104). */
  projectId: number | null;
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
import { InfoTooltipComponent } from '@bill-book/ui-components';
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-journals-page',
  standalone: true,
  imports: [InfoTooltipComponent, 
    EditHistoryComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    RegisterHeaderComponent,
    StatusTabsComponent,
    PeriodFilterBarComponent,
    DocNumberComponent,
    MoneyCellComponent,
    RegisterDatePipe,
    DecimalPipe,
    FormsModule,
    RouterLink,
    BranchDateInputComponent,
    TextInputComponent,
    MoneyInputComponent,
    SelectComponent,
    ApprovalPanelComponent,
  ],
  host: { class: 'register-page' },
  templateUrl: './journals.page.html',
  styleUrl: './journals.page.scss',
})
export class JournalsPage implements OnInit {

  /*
   * The account and sub-account pickers, in the shape `bb-select` takes.
   * Mapped on the class so a grid of journal lines builds each list once
   * rather than per row per render.
   */
  protected readonly accountOptions = computed<BbSelectOption<number>[]>(() =>
    this.postable().map((account) => ({
      value: account.accountId,
      label: `${account.accountCode} — ${account.accountName}`,
    })),
  );

  /** Keyed by account, because which sub-accounts apply depends on it. */
  protected subAccountOptionsFor(accountId: number | null): BbSelectOption<number>[] {
    return this.subAccountsFor(accountId).map((sub) => ({
      value: sub.subAccountId,
      label: sub.subAccountName,
    }));
  }

  /** A journal's statuses, for the register's tabs (TK-187). */
  protected readonly statuses: readonly RegisterStatusOption[] = [
    { value: 'Draft', label: 'Draft' },
    { value: 'Posted', label: 'Posted' },
    { value: 'Reversed', label: 'Reversed' },
  ];

  /** The register's period; a dated register opens on the financial year. */
  protected readonly range = signal<PeriodRange>(rangeForPreset('this-financial-year'));

  /** The status tab chosen; empty is All. */
  protected readonly statusFilter = signal('');

  /** Every journal in the period, before the status tab narrows it. */
  protected readonly periodRows = computed(() =>
    this.rows().filter((row) => isWithin(this.range(), row.journalDate)),
  );

  protected readonly tabs = computed(() => countStatusTabs(this.periodRows(), this.statuses, (row) => row.status));

  protected readonly visibleRows = computed(() => {
    const status = this.statusFilter();
    return this.periodRows().filter((row) => !status || row.status === status);
  });

  /** What the journals on screen move: each one's debit side, which equals its credit side once balanced. */
  protected readonly periodTotal = computed(() =>
    this.visibleRows().reduce((sum, row) => sum + row.totalDebit, 0),
  );

  protected readonly statusTone = registerStatusTone;
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  columns: ColumnDef[] = [
    { field: 'journalNo', header: 'Number' },
    { field: 'journalDate', header: 'Date', dataType: 'date' },
    { field: 'reference', header: 'Reference' },
    { field: 'status', header: 'Status' },
    { field: 'totalDebit', header: 'Debit', dataType: 'money' },
    { field: 'totalCredit', header: 'Credit', dataType: 'money' },
    { field: 'actions', header: '', sortable: false, filterable: false }
  ];

  editLinesColumns: ColumnDef[] = [
    { field: 'lineNumber', header: '#' },
    { field: 'account', header: 'Account' },
    { field: 'subAccount', header: 'Sub-account' },
    { field: 'project', header: 'Project' },
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

  /** The branch's projects, for tagging a line (TK-104). Empty for a user who cannot read them. */
  protected readonly projects = signal<ProjectListItem[]>([]);
  protected readonly projectOptions = computed<BbSelectOption<number>[]>(() =>
    projectOptions(this.projects(), this.lines().map((l) => l.projectId)),
  );
  protected readonly editing = signal(false);
  protected readonly viewing = signal<JournalDetailView | null>(null);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  /** Expense accounts the last save took past the approved budget (TK-109). */
  protected readonly budgetWarnings = signal<string[]>([]);

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
      // Every journal, so the tabs can count each status; the tab and the
      // period narrow in the browser, the way the document registers do.
      this.rows.set(await this.req<JournalListItem[]>('GET', '/api/journals'));

      if (this.accounts().length === 0) {
        const [accounts, subs] = await Promise.all([
          this.req<AccountOption[]>('GET', '/api/accounts'),
          this.req<SubAccountOption[]>('GET', '/api/sub-accounts'),
        ]);
        this.accounts.set(accounts);
        this.subAccounts.set(subs);
        this.projects.set(await loadProjects(this.http));
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
          projectId: l.projectId ?? null,
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
            projectId: l.projectId,
            debitAmount: this.amount(l.debit),
            creditAmount: this.amount(l.credit),
            lineMemo: l.lineMemo || null,
          })),
      };

      let id: number;

      // A save that takes an expense account past the approved budget still
      // stands; the warnings say so (TK-109).
      if (this.editingId === null) {
        const created = await this.req<{ journalId: number; warnings?: string[] }>('POST', '/api/journals', body);
        id = created.journalId;
        this.budgetWarnings.set(created.warnings ?? []);
      } else {
        const saved = await this.req<{ warnings?: string[] } | null>('PUT', `/api/journals/${this.editingId}`, body);
        id = this.editingId;
        this.budgetWarnings.set(saved?.warnings ?? []);
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
    return { accountId: null, subAccountId: null, projectId: null, debit: '', credit: '', lineMemo: '' };
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


