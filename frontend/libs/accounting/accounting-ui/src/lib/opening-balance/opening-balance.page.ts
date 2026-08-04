import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

type LineType = 'GlAccount' | 'ContactReceivable' | 'ContactPayable' | 'Item';

interface AccountOption {
  accountId: number;
  accountCode: string;
  accountName: string;
  isLock: boolean;
  isActive: boolean;
}

interface ContactOption {
  contactId: number;
  contactCode: string;
  displayName: string;
}

interface ItemOption {
  itemId: number;
  itemCode: string;
  itemName: string;
}

interface OpeningBalanceLineView {
  openingBalanceLineId: number;
  lineNumber: number;
  lineType: LineType;
  accountId: number | null;
  accountCode: string | null;
  accountName: string | null;
  contactId: number | null;
  itemId: number | null;
  quantity: number | null;
  unitCost: number | null;
  debitAmount: number;
  creditAmount: number;
  documentReference: string | null;
  documentDate: string | null;
  dueDate: string | null;
  lineMemo: string | null;
}

interface OpeningBalanceView {
  openingBalanceId: number;
  transactionNo: string | null;
  asOfDate: string;
  currencyCode: string;
  memo: string | null;
  status: 'Draft' | 'Finalized';
  finalizedAt: string | null;
  lines: OpeningBalanceLineView[];
}

interface Readiness {
  totalDebit: number;
  totalCredit: number;
  openingBalanceEquity: number;
  inventoryValue: number;
  contactReceivableLines: number;
  contactPayableLines: number;
  itemLines: number;
  blockers: string[];
  canFinalize: boolean;
}

/** A line being keyed. Amounts are strings so an empty box stays empty. */
interface LineForm {
  lineType: LineType;
  accountId: number | null;
  contactId: number | null;
  itemId: number | null;
  quantity: string;
  unitCost: string;
  debit: string;
  credit: string;
  documentReference: string;
  documentDate: string;
  dueDate: string;
  lineMemo: string;
}

/**
 * Accounting › Opening balances. Where the branch's books begin: everything it
 * owned, owed and was owed on the day it started using this product.
 *
 * <b>The panel is the screen, not the grid.</b> A migration is keyed over weeks
 * from an accountant's trial balance, a stock count and a pile of unpaid
 * invoices — so what matters is not entering a line, it is knowing at every
 * moment whether the whole thing ties. **Opening Balance Equity** is the figure
 * to watch: every line posts against it, so whatever it is left holding is
 * exactly the part of the position nobody has keyed yet. Zero means done.
 *
 * Discovering at the end that the total is out by ₹4,300 means auditing weeks of
 * work; discovering it as the line is typed means fixing the line. That is the
 * entire reason readiness is re-read after every save rather than computed when
 * Finalize is pressed.
 *
 * <b>One per branch, and one way.</b> There is no second opening balance and no
 * reopening the first — reversing it would delete the ground every balance since
 * has been measured from. After go-live the screen is a record of what was
 * brought across, and an error is corrected with a journal entry.
 */
@Component({
  selector: 'bb-opening-balance-page',
  standalone: true,
  imports: [DecimalPipe, FormsModule],
  templateUrl: './opening-balance.page.html',
  styleUrl: './opening-balance.page.scss',
})
export class OpeningBalancePage implements OnInit {
  private readonly http = inject(HttpClient);

  protected readonly document = signal<OpeningBalanceView | null>(null);
  protected readonly readiness = signal<Readiness | null>(null);
  protected readonly accounts = signal<AccountOption[]>([]);
  protected readonly contacts = signal<ContactOption[]>([]);
  protected readonly items = signal<ItemOption[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  asOfDate = new Date().toISOString().slice(0, 10);
  memo = '';

  /**
   * A signal, not a plain array, so the totals below move as the document is
   * keyed. `computed` tracks signals; a field it could read but not watch would
   * leave the figures frozen, and those figures are what this screen is for.
   *
   * ngModel writes into the line objects in place, so every input also calls
   * `touch()` to republish the array.
   */
  protected readonly lines = signal<LineForm[]>([]);

  protected readonly finalized = computed(() => this.document()?.status === 'Finalized');

  /** Only accounts an opening balance may be brought in against. */
  protected readonly postable = computed(() =>
    this.accounts().filter((a) => a.isActive && !a.isLock),
  );

  /**
   * Getters rather than computeds: they read `lines()`, which is a signal, but a
   * reader looking for the pattern should find the same rule everywhere — these
   * are cheap, and the screen re-reads them each pass anyway.
   */
  protected get totalDebit(): number {
    return this.lines().reduce((sum, l) => sum + this.money(l.debit), 0);
  }

  protected get totalCredit(): number {
    return this.lines().reduce((sum, l) => sum + this.money(l.credit), 0);
  }

  /** Stock value: quantity × unit cost, which Inventory will post against equity. */
  protected get inventoryValue(): number {
    return this.lines()
      .filter((l) => l.lineType === 'Item')
      .reduce((sum, l) => sum + this.money(l.quantity) * this.money(l.unitCost), 0);
  }

  /**
   * What Opening Balance Equity would be left holding — computed here as well as
   * on the server so it moves while typing rather than only after a save. The
   * server's answer is authoritative and is what blocks the finalize.
   */
  protected get equityPlug(): number {
    return this.totalDebit - this.totalCredit + this.inventoryValue;
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const [accounts, contacts, items] = await Promise.all([
        this.req<AccountOption[]>('GET', '/api/accounts'),
        this.req<ContactOption[]>('GET', '/api/contacts'),
        this.req<ItemOption[]>('GET', '/api/items'),
      ]);

      this.accounts.set(accounts);
      this.contacts.set(contacts);
      this.items.set(items);

      await this.reload();
    } catch {
      this.show('Could not load the opening balance.', true);
    } finally {
      this.busy.set(false);
    }
  }

  /** Re-reads the document and the readiness. 404 means the branch has none yet. */
  private async reload(): Promise<void> {
    try {
      const document = await this.req<OpeningBalanceView>('GET', '/api/opening-balance');
      this.document.set(document);
      this.asOfDate = document.asOfDate;
      this.memo = document.memo ?? '';
      this.lines.set(document.lines.map((l) => this.toForm(l)));

      this.readiness.set(await this.req<Readiness>('GET', '/api/opening-balance/readiness'));
    } catch (err: unknown) {
      if ((err as HttpErrorResponse)?.status === 404) {
        // No opening balance yet, which is the ordinary state of a new branch.
        this.document.set(null);
        this.readiness.set(null);
        if (this.lines().length === 0) {
          this.lines.set([this.blankLine()]);
        }
        return;
      }

      throw err;
    }
  }

  contactName(contactId: number | null): string {
    if (contactId === null) {
      return '—';
    }

    return this.contacts().find((c) => c.contactId === contactId)?.displayName ?? `#${contactId}`;
  }

  itemName(itemId: number | null): string {
    if (itemId === null) {
      return '—';
    }

    return this.items().find((i) => i.itemId === itemId)?.itemName ?? `#${itemId}`;
  }

  /**
   * What one stock line is worth. Kept out of the template because the two
   * figures behind it are strings — an empty box has to stay empty — and
   * multiplying strings in a binding compiles to something that is only
   * accidentally right.
   */
  lineValue(line: LineForm): number {
    return this.money(line.quantity) * this.money(line.unitCost);
  }

  addLine(): void {
    this.lines.update((lines) => [...lines, this.blankLine()]);
  }

  removeLine(index: number): void {
    this.lines.update((lines) => lines.filter((_, i) => i !== index));
  }

  touch(): void {
    this.lines.update((lines) => [...lines]);
  }

  /**
   * Changing what a line is clears what it named as something else. A receivable
   * carrying an account id from when it was a bank balance is exactly what the
   * server refuses, and a screen that offers what the server refuses is a screen
   * arguing with itself.
   */
  onLineType(line: LineForm): void {
    line.accountId = null;
    line.contactId = null;
    line.itemId = null;
    line.quantity = '';
    line.unitCost = '';

    if (line.lineType === 'Item') {
      line.debit = '';
      line.credit = '';
    }

    this.touch();
  }

  /** A line is a debit or a credit, never both. */
  onDebit(line: LineForm): void {
    if (this.money(line.debit) > 0) {
      line.credit = '';
    }
    this.touch();
  }

  onCredit(line: LineForm): void {
    if (this.money(line.credit) > 0) {
      line.debit = '';
    }
    this.touch();
  }

  async save(): Promise<void> {
    this.busy.set(true);
    try {
      await this.req('PUT', '/api/opening-balance', {
        asOfDate: this.asOfDate,
        memo: this.memo || null,
        // A row left behind by "add line" is not an error to complain about, it
        // is a row the user did not fill in.
        lines: this.lines().filter((l) => this.isFilled(l)).map((l) => ({
          lineType: l.lineType,
          accountId: l.lineType === 'GlAccount' ? l.accountId : null,
          contactId:
            l.lineType === 'ContactReceivable' || l.lineType === 'ContactPayable'
              ? l.contactId
              : null,
          itemId: l.lineType === 'Item' ? l.itemId : null,
          quantity: l.lineType === 'Item' ? this.money(l.quantity) : null,
          unitCost: l.lineType === 'Item' ? this.money(l.unitCost) : null,
          debitAmount: l.lineType === 'Item' ? 0 : this.money(l.debit),
          creditAmount: l.lineType === 'Item' ? 0 : this.money(l.credit),
          documentReference: l.documentReference || null,
          documentDate: l.documentDate || null,
          dueDate: l.dueDate || null,
          lineMemo: l.lineMemo || null,
        })),
      });

      this.show('Saved.', false);
      await this.reload();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not save the opening balance.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async finalize(): Promise<void> {
    if (
      !confirm(
        'Open the books on these figures?\n\nThis cannot be undone or reopened — every balance ' +
          'from here on is measured from it. An error found afterwards is corrected with a ' +
          'journal entry.',
      )
    ) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('POST', '/api/opening-balance/finalize');
      this.show('The books are open.', false);
      await this.reload();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not open the books.'), true);
      await this.reload();
    } finally {
      this.busy.set(false);
    }
  }

  async discard(): Promise<void> {
    if (!confirm('Delete this draft opening balance? Everything keyed into it is lost.')) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', '/api/opening-balance');
      this.document.set(null);
      this.readiness.set(null);
      this.lines.set([this.blankLine()]);
      this.memo = '';
      this.show('Draft deleted.', false);
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not delete the draft.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  /** Whether a row has enough on it to be worth sending. */
  private isFilled(line: LineForm): boolean {
    switch (line.lineType) {
      case 'GlAccount':
        return line.accountId !== null;
      case 'Item':
        return line.itemId !== null;
      default:
        return line.contactId !== null;
    }
  }

  private toForm(line: OpeningBalanceLineView): LineForm {
    return {
      lineType: line.lineType,
      accountId: line.accountId,
      contactId: line.contactId,
      itemId: line.itemId,
      quantity: line.quantity === null ? '' : String(line.quantity),
      unitCost: line.unitCost === null ? '' : String(line.unitCost),
      debit: line.debitAmount ? String(line.debitAmount) : '',
      credit: line.creditAmount ? String(line.creditAmount) : '',
      documentReference: line.documentReference ?? '',
      documentDate: line.documentDate ?? '',
      dueDate: line.dueDate ?? '',
      lineMemo: line.lineMemo ?? '',
    };
  }

  private blankLine(): LineForm {
    return {
      lineType: 'GlAccount',
      accountId: null,
      contactId: null,
      itemId: null,
      quantity: '',
      unitCost: '',
      debit: '',
      credit: '',
      documentReference: '',
      documentDate: '',
      dueDate: '',
      lineMemo: '',
    };
  }

  /** Parses a typed amount. Anything unparseable is zero, never NaN in a total. */
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
