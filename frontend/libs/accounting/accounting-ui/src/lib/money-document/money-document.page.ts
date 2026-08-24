import { ChangeDetectionStrategy } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { DataGridComponent, ColumnDef , DateInputComponent , TextInputComponent , NumberInputComponent } from '@bill-book/ui-components';
import { AllocationFormComponent } from '../allocation-form/allocation-form.component';
import {
  MoneySource,
  PAYMENT_METHODS,
  RECEIVE_SOURCES,
  SPEND_SOURCES,
} from './money-sources';

type Direction = 'spend' | 'receive';

interface BankAccountOption {
  bankAccountId: number;
  accountName: string;
  accountNumber: string;
  currencyCode: string;
  isDefault: boolean;
  isActive: boolean;
  isLedgerLinked: boolean;
}

interface ContactOption {
  contactId: number;
  contactCode: string;
  displayName: string;
  isCustomer: boolean;
  isVendor: boolean;
}

/** As `/api/sub-accounts` returns them — enums by name, not by number. */
interface SubAccountRow {
  subAccountId: number;
  accountId: number;
  accountName: string;
  accountCode: string;
  referenceType: string;
  referenceId: number;
  purpose: string;
  subAccountName: string;
  isActive: boolean;
}

interface MoneyLineView {
  detailId: number;
  lineNumber: number;
  ledgerSourceId: number;
  mappingTransactionTypeCode: string | null;
  mappingTransactionId: number | null;
  amount: number;
  amountBase: number;
  lineMemo: string | null;
}

interface MoneyDocumentListItem {
  documentId: number;
  transactionNo: string | null;
  transactionDate: string;
  bankAccountId: number;
  bankAccountName: string;
  contactId: number | null;
  amount: number;
  currencyCode: string;
  exchangeRate: number;
  paymentMethod: string;
  referenceNo: string | null;
  memo: string | null;
  status: 'Draft' | 'Posted' | 'Void';
  postedAt: string | null;
  mappingTransactionTypeCode: string | null;
  mappingTransactionId: number | null;
}

interface MoneyDocumentView extends MoneyDocumentListItem {
  lines: MoneyLineView[];
}

/** A line being keyed. The amount is a string so an empty box stays empty. */
interface LineForm {
  ledgerSourceId: number | null;
  mappingTransactionTypeCode: string | null;
  mappingTransactionId: string;
  amount: string;
  lineMemo: string;
}

/**
 * Banking › Spend money and Receive money. One component for both: they are the
 * same document read in opposite directions, and the only things that differ are
 * the wording, the endpoint and which meanings a line may carry. Two copies of
 * this screen would drift, and the half that drifted would be the one nobody
 * opened.
 *
 * <b>The account is not a field on this screen.</b> Once a contact is chosen the
 * account each line posts to follows from what the line is for — a bill payment
 * hits that contact's balance under Accounts Payable, an advance hits their
 * prepayment balance under Accounts Receivable — and the screen shows it rather
 * than asking. It is shown, not hidden, because someone keying a payment still
 * has to be able to see where it lands; but it cannot be overridden, because the
 * server resolves the same sub-account from the same rule and a picker that
 * disagreed with it would be a screen arguing with the ledger.
 *
 * That is also why choosing the contact comes before keying any line: without
 * one there is no sub-ledger to point at, and a payment against the bare control
 * account is exactly the posting that makes a contact statement stop tying.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-money-document-page',
  standalone: true,
  imports: [DataGridComponent, DecimalPipe, FormsModule, DateInputComponent, TextInputComponent, NumberInputComponent, AllocationFormComponent],
  templateUrl: './money-document.page.html',
  styleUrl: './money-document.page.scss',
})
export class MoneyDocumentPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);

  editLinesColumns: ColumnDef[] = [
    { field: 'lineNumber', header: '#' },
    { field: 'source', header: 'What this is for' },
    { field: 'postsTo', header: 'Posts to' },
    { field: 'settles', header: 'Settles' },
    { field: 'memo', header: 'Memo' },
    { field: 'amount', header: 'Amount' },
    { field: 'actions', header: 'Actions' }
  ];

  viewLinesColumns: ColumnDef[] = [
    { field: 'lineNumber', header: '#' },
    { field: 'source', header: 'What this is for' },
    { field: 'settles', header: 'Settles' },
    { field: 'memo', header: 'Memo' },
    { field: 'amount', header: 'Amount' }
  ];

  protected readonly paymentMethods = PAYMENT_METHODS;

  protected readonly direction = signal<Direction>('spend');
  protected readonly rows = signal<MoneyDocumentListItem[]>([]);
  protected readonly bankAccounts = signal<BankAccountOption[]>([]);
  protected readonly contacts = signal<ContactOption[]>([]);
  protected readonly subAccounts = signal<SubAccountRow[]>([]);
  protected readonly subAccountsFor = signal<number | null>(null);
  protected readonly editing = signal(false);
  protected readonly viewing = signal<MoneyDocumentView | null>(null);
  protected readonly busy = signal(false);
  protected readonly allocating = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  statusFilter = '';
  protected editingId: number | null = null;

  transactionDate = new Date().toISOString().slice(0, 10);
  bankAccountId: number | null = null;
  contactId: number | null = null;
  amount = '';
  paymentMethod = 2;
  referenceNo = '';
  referenceDate = '';
  memo = '';

  /**
   * A signal rather than a plain array, so the allocated/unallocated panel below
   * moves as the lines are keyed. `computed` tracks signals; a field it can read
   * but not watch would leave those figures frozen at whatever the first line
   * said — and the difference between them is the thing that decides whether the
   * document can post at all.
   */
  protected readonly lines = signal<LineForm[]>([]);

  protected readonly sources = computed<readonly MoneySource[]>(() =>
    this.direction() === 'spend' ? SPEND_SOURCES : RECEIVE_SOURCES,
  );

  protected readonly title = computed(() =>
    this.direction() === 'spend' ? 'Spend money' : 'Receive money',
  );

  /** The word for the counterparty box. "Paid to" and "Received from" are not interchangeable. */
  protected readonly contactLabel = computed(() =>
    this.direction() === 'spend' ? 'Paid to' : 'Received from',
  );

  protected readonly columns = computed<ColumnDef[]>(() => [
    { field: 'transactionDate', header: 'Date' },
    { field: 'transactionNo', header: 'Number' },
    { field: 'contact', header: this.contactLabel() },
    { field: 'account', header: 'Account' },
    { field: 'amount', header: 'Amount' },
    { field: 'status', header: 'Status' },
    { field: 'actions', header: 'Actions' }
  ]);

  private get endpoint(): string {
    return this.direction() === 'spend' ? '/api/spend-money' : '/api/receive-money';
  }

  /**
   * Only accounts money can actually move through. An inactive account is not an
   * option, and one whose ledger account never got provisioned would be refused
   * at post — offering it here means finding that out after the typing is done.
   */
  protected readonly usableAccounts = computed(() =>
    this.bankAccounts().filter((a) => a.isActive && a.isLedgerLinked),
  );

  /**
   * Getters, not computeds, and deliberately.
   *
   * These read the header fields — amount, contact, account — which ngModel
   * writes as plain properties. A `computed` only recomputes when a signal it
   * read has changed, so one built over a plain field caches the first answer
   * forever: raising the header amount would leave "unallocated" showing zero and
   * Post still enabled. A getter is re-read on every change-detection pass, which
   * is what a template bound to plain fields needs.
   */
  protected get allocated(): number {
    return this.lines().reduce((sum, l) => sum + this.money(l.amount), 0);
  }

  protected get unallocated(): number {
    return this.money(this.amount) - this.allocated;
  }

  /**
   * A document posts when it has a contact, an account, a positive amount, and
   * lines that add up to it. Half a paisa of tolerance, because the amounts are
   * keyed to two decimals and exact floating-point equality would refuse a
   * document that adds up perfectly.
   */
  protected get canPost(): boolean {
    return (
      this.contactId !== null &&
      this.bankAccountId !== null &&
      this.money(this.amount) > 0 &&
      this.lines().some((l) => l.ledgerSourceId !== null) &&
      Math.abs(this.unallocated) < 0.005
    );
  }

  ngOnInit(): void {
    // Set once from the route rather than watched: spend and receive are
    // separate routes, so arriving at one is a fresh component.
    this.direction.set(this.route.snapshot.data['direction'] === 'receive' ? 'receive' : 'spend');
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const query = this.statusFilter ? `?status=${this.statusFilter}` : '';
      this.rows.set(await this.req<MoneyDocumentListItem[]>('GET', `${this.endpoint}${query}`));

      if (this.bankAccounts().length === 0) {
        const [accounts, contacts] = await Promise.all([
          this.req<BankAccountOption[]>('GET', '/api/bank-accounts'),
          this.req<ContactOption[]>('GET', '/api/contacts'),
        ]);
        this.bankAccounts.set(accounts);
        this.contacts.set(contacts);
      }
    } catch {
      this.show(`Could not load ${this.title().toLowerCase()} documents.`, true);
    } finally {
      this.busy.set(false);
    }
  }

  contactName(contactId: number | null): string {
    if (contactId === null) {
      return '—';
    }

    return this.contacts().find((c) => c.contactId === contactId)?.displayName ?? `#${contactId}`;
  }

  sourceFor(ledgerSourceId: number | null): MoneySource | null {
    return this.sources().find((s) => s.ledgerSourceId === ledgerSourceId) ?? null;
  }

  /**
   * The account a line will post to: the chosen contact's sub-account beneath the
   * control account the line's meaning names. Null when the contact has none —
   * which happens when provisioning failed at the time the contact was saved, and
   * is worth saying out loud here rather than letting the post fail.
   */
  subAccountFor(line: LineForm): SubAccountRow | null {
    const source = this.sourceFor(line.ledgerSourceId);
    if (source === null || this.subAccountsFor() !== this.contactId) {
      return null;
    }

    return (
      this.subAccounts().find(
        (s) =>
          s.isActive &&
          s.accountName === source.accountSystemName &&
          s.purpose === source.purpose,
      ) ?? null
    );
  }

  /** True once a contact is chosen but its sub-ledger came back short. */
  protected get subLedgerMissing(): boolean {
    return (
      this.contactId !== null &&
      this.subAccountsFor() === this.contactId &&
      this.subAccounts().length === 0
    );
  }

  async onContact(): Promise<void> {
    this.subAccounts.set([]);
    this.subAccountsFor.set(null);

    if (this.contactId === null) {
      return;
    }

    const wanted = this.contactId;
    try {
      const rows = await this.req<SubAccountRow[]>(
        'GET',
        `/api/sub-accounts?referenceType=Contact&referenceId=${wanted}`,
      );

      // The contact may have changed again while this was in flight, and
      // showing one contact's accounts against another's name is worse than
      // showing none.
      if (this.contactId === wanted) {
        this.subAccounts.set(rows);
        this.subAccountsFor.set(wanted);
      }
    } catch {
      this.show('The contact’s ledger accounts could not be read.', true);
    }
  }

  startCreate(): void {
    this.editingId = null;
    this.viewing.set(null);
    this.transactionDate = new Date().toISOString().slice(0, 10);
    this.bankAccountId =
      this.usableAccounts().find((a) => a.isDefault)?.bankAccountId ??
      this.usableAccounts()[0]?.bankAccountId ??
      null;
    this.contactId = null;
    this.subAccounts.set([]);
    this.subAccountsFor.set(null);
    this.amount = '';
    this.paymentMethod = 2;
    this.referenceNo = '';
    this.referenceDate = '';
    this.memo = '';
    this.lines.set([this.blankLine()]);
    this.editing.set(true);
  }

  async open(documentId: number): Promise<void> {
    const document = await this.req<MoneyDocumentView>('GET', `${this.endpoint}/${documentId}`);

    if (document.status !== 'Draft') {
      // Posted or void: read-only, because a posted document is never edited.
      this.editing.set(false);
      this.viewing.set(document);
      return;
    }

    this.editingId = document.documentId;
    this.viewing.set(null);
    this.transactionDate = document.transactionDate;
    this.bankAccountId = document.bankAccountId;
    this.contactId = document.contactId;
    this.amount = String(document.amount);
    this.paymentMethod =
      this.paymentMethods.find((m) => m.label.toLowerCase() === document.paymentMethod.toLowerCase())
        ?.value ?? 2;
    this.referenceNo = document.referenceNo ?? '';
    this.referenceDate = '';
    this.memo = document.memo ?? '';
    this.lines.set(
      document.lines.map((l) => ({
        ledgerSourceId: l.ledgerSourceId,
        mappingTransactionTypeCode: l.mappingTransactionTypeCode,
        mappingTransactionId: l.mappingTransactionId === null ? '' : String(l.mappingTransactionId),
        amount: String(l.amount),
        lineMemo: l.lineMemo ?? '',
      })),
    );
    this.editing.set(true);
    await this.onContact();
  }

  close(): void {
    this.editing.set(false);
    this.viewing.set(null);
    this.editingId = null;
    this.allocating.set(false);
  }

  startAllocation(): void {
    this.allocating.set(true);
  }

  onAllocationClosed(success: boolean): void {
    this.allocating.set(false);
    if (success) {
      this.show('Allocation successful.', false);
      // We could reload the document to show updated lines, but allocations live in TransactionRatio
      // not in the document's own lines.
    }
  }

  addLine(): void {
    this.lines.update((lines) => [...lines, this.blankLine()]);
  }

  removeLine(index: number): void {
    this.lines.update((lines) => lines.filter((_, i) => i !== index));
  }

  /**
   * Re-publishes the array after ngModel has written into one of its objects in
   * place. Without it the allocated figure below would not move as lines are keyed.
   */
  touch(): void {
    this.lines.update((lines) => [...lines]);
  }

  /**
   * The document a line settles is decided by what the line is for, not chosen
   * beside it — an advance settles nothing, and a bill payment can only settle a
   * bill. Changing the meaning therefore drops an id keyed against the old one,
   * which would otherwise point the payment at a document of the wrong kind.
   */
  onSource(line: LineForm): void {
    const source = this.sourceFor(line.ledgerSourceId);
    line.mappingTransactionTypeCode = source?.settles ?? null;

    if (line.mappingTransactionTypeCode === null) {
      line.mappingTransactionId = '';
    }

    this.touch();
  }

  /** Puts whatever is unallocated onto this line, which is most of what keying one is. */
  fill(line: LineForm): void {
    const rest = this.unallocated + this.money(line.amount);
    line.amount = rest > 0 ? rest.toFixed(2) : '';
    this.touch();
  }

  /**
   * The source an excess belongs under — an overpayment against a document
   * rather than a deliberate advance, which is what money left over on a payment
   * that settles something actually is.
   */
  private get overpaymentSource(): MoneySource | null {
    return this.sourceFor(this.direction() === 'spend' ? 16 : 17);
  }

  protected get overpaymentLabel(): string {
    return this.overpaymentSource?.label ?? 'an overpayment';
  }

  /**
   * Adds the unallocated remainder as an overpayment.
   *
   * <b>This is what stops an excess becoming a negative balance.</b> Left on the
   * settled line, money paid over what a bill was for turns the supplier's
   * payable into a debit — which every aging report reads as "they owe us", on a
   * line that nets against the next bill. As an overpayment it sits in that
   * contact's overpayment advance instead: visible as money held, refundable on
   * its own, and separable on a balance sheet where trade balances and advances
   * are different lines.
   *
   * The excess names the document it ran past when there is exactly one, so it
   * stays traceable to the bill that caused it.
   */
  addOverpayment(): void {
    const source = this.overpaymentSource;
    const rest = this.unallocated;

    if (source === null || rest <= 0) {
      return;
    }

    const settled = this.lines().filter((l) => l.mappingTransactionId.trim() !== '');

    this.lines.update((lines) => [
      ...lines,
      {
        ledgerSourceId: source.ledgerSourceId,
        mappingTransactionTypeCode: source.settles,
        mappingTransactionId:
          settled.length === 1 && settled[0].mappingTransactionTypeCode === source.settles
            ? settled[0].mappingTransactionId
            : '',
        amount: rest.toFixed(2),
        lineMemo: '',
      },
    ]);
  }

  async save(post: boolean): Promise<void> {
    if (!this.validateSave(post)) {
      return;
    }

    this.busy.set(true);
    try {
      const kept = this.lines().filter((l) => l.ledgerSourceId !== null);

      const body = {
        transactionDate: this.transactionDate,
        bankAccountId: this.bankAccountId,
        contactId: this.contactId,
        amount: this.money(this.amount),
        currencyCode: null,
        exchangeRate: null,
        paymentMethod: this.paymentMethod,
        referenceNo: this.referenceNo || null,
        referenceDate: this.referenceDate || null,
        memo: this.memo || null,
        // No header mapping is sent. The server derives it from the lines,
        // because it is a summary of them rather than a fact beside them —
        // sending one from here would only create something to disagree with.
        lines: kept.map((l) => ({
          ledgerSourceId: l.ledgerSourceId,
          mappingTransactionTypeCode: l.mappingTransactionTypeCode,
          mappingTransactionId: this.reference(l.mappingTransactionId),
          amount: this.money(l.amount),
          lineMemo: l.lineMemo || null,
        })),
      };

      let id: number;

      if (this.editingId === null) {
        const created = await this.req<{ id: number }>('POST', this.endpoint, body);
        id = created.id;
      } else {
        await this.req('PUT', `${this.endpoint}/${this.editingId}`, body);
        id = this.editingId;
      }

      if (post) {
        await this.req('POST', `${this.endpoint}/${id}/post`);
        this.show('Posted.', false);
        this.close();
      } else {
        this.editingId = id;
        this.show('Draft saved.', false);
      }

      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not save the document.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async voidDocument(row: MoneyDocumentListItem): Promise<void> {
    const reason = prompt(
      `Void ${row.transactionNo}? Its ledger rows are withdrawn; the document and its number stay.`,
      '',
    );

    if (reason === null) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('POST', `${this.endpoint}/${row.documentId}/void`, {
        reason: reason || null,
      });
      this.show('Voided.', false);
      this.viewing.set(null);
      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not void the document.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async discard(row: MoneyDocumentListItem): Promise<void> {
    if (!confirm('Delete this draft?')) {
      return;
    }

    this.busy.set(true);
    try {
      await this.req('DELETE', `${this.endpoint}/${row.documentId}`);
      this.close();
      await this.load();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not delete the draft.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  private validateSave(post: boolean): boolean {
    if (this.bankAccountId === null) {
      this.show('Choose a bank or cash account.', true);
      return false;
    }

    if (this.contactId === null) {
      this.show('Choose the contact.', true);
      return false;
    }

    if (this.money(this.amount) <= 0) {
      this.show('Amount must be greater than zero.', true);
      return false;
    }

    if (post && !this.canPost) {
      this.show('Complete allocations before posting.', true);
      return false;
    }

    return true;
  }

  /** Parses a typed amount. Anything unparseable is zero, never NaN in a total. */
  private money(raw: string): number {
    const parsed = Number(raw);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : 0;
  }

  /** A settled document's id, or null — an empty box is "none", not zero. */
  private reference(raw: string): number | null {
    const parsed = Number(raw);
    return raw.trim() !== '' && Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  }

  private blankLine(): LineForm {
    return {
      ledgerSourceId: null,
      mappingTransactionTypeCode: null,
      mappingTransactionId: '',
      amount: '',
      lineMemo: '',
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

