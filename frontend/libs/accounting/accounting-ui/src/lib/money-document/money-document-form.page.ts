import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ProjectListItem, loadProjects, projectOptions } from '../projects/project-options';
import {
  ApprovalPanelComponent,
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  ConfirmDialogService,
  DataGridCellTemplateDirective,
  DataGridComponent,
  IconButtonComponent,
  InfoTooltipComponent,
  BranchDateInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { AllocationApiService, OpenDocument } from '@bill-book/api-client';
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
  projectId?: number | null;
  isBillable?: boolean;
  markupPercent?: number | null;
  billedInvoiceId?: number | null;
}

interface MoneyDocumentView {
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
  lines: MoneyLineView[];
  tdsAmount?: number;
  tdsSectionCode?: string | null;
  deductorTan?: string | null;
  isTdsChallan?: boolean;
  challanNo?: string | null;
  bsrCode?: string | null;
  challanSectionCode?: string | null;
  challanMonth?: string | null;
}

interface LineForm {
  ledgerSourceId: number | null;
  mappingTransactionTypeCode: string | null;
  mappingTransactionId: string;
  amount: string;
  lineMemo: string;
  projectId: number | null;
  isBillable: boolean;
  markupPercent: string;
  billedInvoiceId: number | null;
}

interface TdsQuote {
  applies: boolean;
  sectionCode: string | null;
  tdsAmount: number;
  note: string | null;
}

const SUPPLIER_ADVANCE_SOURCE = 8;

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-money-document-form-page',
  standalone: true,
  imports: [
    InfoTooltipComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DecimalPipe,
    FormsModule,
    BranchDateInputComponent,
    TextInputComponent,
    NumberInputComponent,
    AllocationFormComponent,
    SelectComponent,
    ApprovalPanelComponent,
    CheckboxComponent,
    IconButtonComponent,
  ],
  templateUrl: './money-document-form.page.html',
  styleUrl: './money-document-form.page.scss',
})
export class MoneyDocumentFormPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly confirmDialog = inject(ConfirmDialogService);

  editLinesColumns: ColumnDef[] = [
    { field: 'lineNumber', header: '#' },
    { field: 'source', header: 'What this is for' },
    { field: 'postsTo', header: 'Posts to' },
    { field: 'settles', header: 'Settles' },
    { field: 'project', header: 'Project' },
    { field: 'memo', header: 'Memo' },
    { field: 'amount', header: 'Amount' },
    { field: 'actions', header: 'Actions' },
  ];

  viewLinesColumns: ColumnDef[] = [
    { field: 'lineNumber', header: '#' },
    { field: 'source', header: 'What this is for' },
    { field: 'settles', header: 'Settles' },
    { field: 'memo', header: 'Memo' },
    { field: 'amount', header: 'Amount' },
  ];

  protected readonly paymentMethods = PAYMENT_METHODS;

  protected readonly bankAccountOptions = computed<BbSelectOption<number>[]>(() =>
    this.usableAccounts().map((a) => ({
      value: a.bankAccountId,
      label: `${a.accountName} (${a.accountNumber})`,
    })),
  );

  protected readonly contactOptions = computed<BbSelectOption<number>[]>(() =>
    this.contacts()
      .filter((c) => (this.direction() === 'spend' ? c.isVendor : c.isCustomer))
      .map((c) => ({
        value: c.contactId,
        label: `${c.displayName} (${c.contactCode})`,
      })),
  );

  protected readonly sourceOptions: BbSelectOption<number>[] = [];

  protected readonly direction = signal<Direction>('spend');
  protected readonly bankAccounts = signal<BankAccountOption[]>([]);
  protected readonly contacts = signal<ContactOption[]>([]);
  protected readonly subAccounts = signal<SubAccountRow[]>([]);
  protected readonly subAccountsFor = signal<number | null>(null);

  /** The contact's documents with something left to settle, both sides. */
  protected readonly openDocuments = signal<readonly OpenDocument[]>([]);
  private readonly allocationApi = inject(AllocationApiService);
  protected readonly viewing = signal<MoneyDocumentView | null>(null);
  protected readonly busy = signal(false);
  protected readonly allocating = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  protected editingId: number | null = null;

  transactionDate = localToday();
  bankAccountId: number | null = null;
  contactId: number | null = null;
  amount = '';
  paymentMethod = 2;
  referenceNo = '';
  referenceDate = '';
  memo = '';

  tdsAmount = '';
  tdsSectionCode = '';
  deductorTan = '';
  isTdsChallan = false;
  challanNo = '';
  bsrCode = '';
  challanSectionCode = '';
  challanMonth = '';
  protected readonly tdsPreview = signal<TdsQuote | null>(null);

  protected readonly lines = signal<LineForm[]>([]);
  protected readonly projects = signal<ProjectListItem[]>([]);
  protected readonly projectOptions = computed<BbSelectOption<number>[]>(() =>
    projectOptions(this.projects(), this.lines().map((l) => l.projectId)),
  );

  protected readonly sources = computed<readonly MoneySource[]>(() =>
    this.direction() === 'spend' ? SPEND_SOURCES : RECEIVE_SOURCES,
  );

  protected readonly title = computed(() =>
    this.direction() === 'spend' ? 'Spend money' : 'Receive money',
  );

  protected readonly contactLabel = computed(() =>
    this.direction() === 'spend' ? 'Paid to' : 'Received from',
  );

  private get endpoint(): string {
    return this.direction() === 'spend' ? '/api/spend-money' : '/api/receive-money';
  }

  protected readonly usableAccounts = computed(() =>
    this.bankAccounts().filter((a) => a.isActive && a.isLedgerLinked),
  );

  protected get allocated(): number {
    return this.lines().reduce((sum, l) => sum + this.money(l.amount), 0);
  }

  protected get withheld(): number {
    if (this.direction() === 'receive') {
      return this.money(this.tdsAmount);
    }
    const preview = this.tdsPreview();
    return preview?.applies ? preview.tdsAmount : 0;
  }

  protected get advanceTotal(): number {
    return this.lines()
      .filter((l) => l.ledgerSourceId === SUPPLIER_ADVANCE_SOURCE)
      .reduce((sum, l) => sum + this.money(l.amount), 0);
  }

  protected get unallocated(): number {
    return this.money(this.amount) + this.withheld - this.allocated;
  }

  protected get canPost(): boolean {
    if (this.direction() === 'spend' && this.isTdsChallan) {
      return (
        this.contactId !== null &&
        this.bankAccountId !== null &&
        this.money(this.amount) > 0 &&
        this.challanSectionCode.trim() !== '' &&
        this.challanMonth !== ''
      );
    }

    return (
      this.contactId !== null &&
      this.bankAccountId !== null &&
      this.money(this.amount) > 0 &&
      this.lines().some((l) => l.ledgerSourceId !== null) &&
      Math.abs(this.unallocated) < 0.005
    );
  }

  ngOnInit(): void {
    void this.init();
  }

  private async init(): Promise<void> {
    this.direction.set(this.route.snapshot.data['direction'] === 'receive' ? 'receive' : 'spend');

    const sources = this.direction() === 'spend' ? SPEND_SOURCES : RECEIVE_SOURCES;
    (this.sourceOptions as BbSelectOption<number>[]).length = 0;
    for (const s of sources) {
      this.sourceOptions.push({ value: s.ledgerSourceId, label: s.label });
    }

    await this.loadReferences();

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

  private async loadReferences(): Promise<void> {
    try {
      const [accounts, contacts] = await Promise.all([
        this.req<BankAccountOption[]>('GET', '/api/bank-accounts'),
        this.req<ContactOption[]>('GET', '/api/contacts'),
      ]);
      this.bankAccounts.set(accounts);
      this.contacts.set(contacts);
      this.projects.set(await loadProjects(this.http));
    } catch {
      this.show('Could not load account and contact references.', true);
    }
  }

  startCreate(): void {
    this.editingId = null;
    this.viewing.set(null);
    this.transactionDate = localToday();
    this.bankAccountId =
      this.usableAccounts().find((a) => a.isDefault)?.bankAccountId ??
      this.usableAccounts()[0]?.bankAccountId ??
      null;
    this.contactId = null;
    this.subAccounts.set([]);
    this.subAccountsFor.set(null);
    this.openDocuments.set([]);
    this.amount = '';
    this.paymentMethod = 2;
    this.referenceNo = '';
    this.referenceDate = '';
    this.memo = '';
    this.resetTds();
    this.lines.set([this.blankLine()]);
  }

  async open(documentId: number): Promise<void> {
    try {
      const document = await this.req<MoneyDocumentView>('GET', `${this.endpoint}/${documentId}`);
      if (document.status !== 'Draft') {
        this.viewing.set(document);
        this.editingId = null;
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
      this.tdsAmount = document.tdsAmount ? String(document.tdsAmount) : '';
      this.tdsSectionCode = document.tdsSectionCode ?? '';
      this.deductorTan = document.deductorTan ?? '';
      this.isTdsChallan = document.isTdsChallan ?? false;
      this.challanNo = document.challanNo ?? '';
      this.bsrCode = document.bsrCode ?? '';
      this.challanSectionCode = document.challanSectionCode ?? '';
      this.challanMonth = document.challanMonth ?? '';
      this.tdsPreview.set(null);
      this.lines.set(
        document.lines.map((l) => ({
          ledgerSourceId: l.ledgerSourceId,
          mappingTransactionTypeCode: l.mappingTransactionTypeCode,
          mappingTransactionId: l.mappingTransactionId === null ? '' : String(l.mappingTransactionId),
          amount: String(l.amount),
          lineMemo: l.lineMemo ?? '',
          projectId: l.projectId ?? null,
          isBillable: l.isBillable ?? false,
          markupPercent: l.markupPercent == null ? '' : String(l.markupPercent),
          billedInvoiceId: l.billedInvoiceId ?? null,
        })),
      );
      await this.onContact();
    } catch {
      this.show('Could not open document.', true);
    }
  }

  goBack(): void {
    const route = this.direction() === 'spend' ? '/banking/spend-money' : '/banking/receive-money';
    void this.router.navigate([route]);
  }

  contactName(contactId: number | null): string {
    if (contactId === null) return '—';
    return this.contacts().find((c) => c.contactId === contactId)?.displayName ?? `#${contactId}`;
  }

  sourceFor(ledgerSourceId: number | null): MoneySource | null {
    return this.sources().find((s) => s.ledgerSourceId === ledgerSourceId) ?? null;
  }

  subAccountFor(line: LineForm): SubAccountRow | null {
    const source = this.sourceFor(line.ledgerSourceId);
    if (source === null || this.subAccountsFor() !== this.contactId) return null;
    return this.subAccounts().find((a) => a.purpose === source.purpose) ?? null;
  }

  async onContact(): Promise<void> {
    if (this.contactId === null) {
      this.subAccounts.set([]);
      this.subAccountsFor.set(null);
      this.openDocuments.set([]);
      return;
    }
    const wanted = this.contactId;
    void this.loadOpenDocuments(wanted);
    try {
      const rows = await this.req<SubAccountRow[]>(
        'GET',
        `/api/sub-accounts?referenceType=Contact&referenceId=${wanted}`,
      );
      if (this.contactId === wanted) {
        this.subAccounts.set(rows);
        this.subAccountsFor.set(wanted);
      }
    } catch {
      this.show('The contact’s ledger accounts could not be read.', true);
    }
  }

  /**
   * What a line can settle: the contact's open documents of the kind its source
   * settles, plus whatever the line already names, so a draft reopened after its
   * bill was settled elsewhere still shows its own choice.
   */
  protected documentOptions(line: LineForm): BbSelectOption<number>[] {
    const code = line.mappingTransactionTypeCode;
    if (code === null) return [];
    const chosen = this.reference(line.mappingTransactionId);
    const options = this.openDocuments()
      .filter((d) => d.transactionTypeCode === code && (d.unallocatedAmount > 0 || d.transactionId === chosen))
      .map((d) => ({
        value: d.transactionId,
        label: `${d.documentNo} · ${d.documentDate} · ${d.unallocatedAmount.toFixed(2)} open`,
      }));
    if (chosen !== null && !options.some((o) => o.value === chosen)) {
      options.unshift({ value: chosen, label: `${code} #${chosen}` });
    }
    return options;
  }

  /** Names the document a line settles and, on an empty line, takes what it still owes. */
  protected pickDocument(line: LineForm, transactionId: number | null): void {
    line.mappingTransactionId = transactionId === null ? '' : String(transactionId);
    const document = this.openDocuments().find(
      (d) => d.transactionTypeCode === line.mappingTransactionTypeCode && d.transactionId === transactionId,
    );
    if (document && this.money(line.amount) === 0) {
      line.amount = document.unallocatedAmount.toFixed(2);
    }
    this.touch();
  }

  private async loadOpenDocuments(contactId: number): Promise<void> {
    try {
      const open = await this.allocationApi.openDocuments(contactId);
      if (this.contactId === contactId) {
        this.openDocuments.set([...open.sources, ...open.targets]);
      }
    } catch {
      if (this.contactId === contactId) {
        this.openDocuments.set([]);
      }
    }
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

  onSource(line: LineForm): void {
    const source = this.sourceFor(line.ledgerSourceId);
    line.mappingTransactionTypeCode = source?.settles ?? null;
    if (line.mappingTransactionTypeCode === null) {
      line.mappingTransactionId = '';
    }
    this.touch();
  }

  fill(line: LineForm): void {
    const rest = this.unallocated + this.money(line.amount);
    line.amount = rest > 0 ? rest.toFixed(2) : '';
    this.touch();
  }

  private get overpaymentSource(): MoneySource | null {
    return this.sourceFor(this.direction() === 'spend' ? 16 : 17);
  }

  protected get overpaymentLabel(): string {
    return this.overpaymentSource?.label ?? 'an overpayment';
  }

  addOverpayment(): void {
    const source = this.overpaymentSource;
    const rest = this.unallocated;
    if (source === null || rest <= 0) return;

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
        projectId: null,
        isBillable: false,
        markupPercent: '',
        billedInvoiceId: null,
      },
    ]);
  }

  async save(post: boolean): Promise<void> {
    if (!this.validateSave(post)) return;

    this.busy.set(true);
    try {
      const challan = this.direction() === 'spend' && this.isTdsChallan;
      const kept = challan ? [] : this.lines().filter((l) => l.ledgerSourceId !== null);

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
        tdsAmount: this.direction() === 'receive' ? this.money(this.tdsAmount) : 0,
        tdsSectionCode: this.direction() === 'receive' ? this.tdsSectionCode.trim() || null : null,
        deductorTan: this.direction() === 'receive' ? this.deductorTan.trim().toUpperCase() || null : null,
        isTdsChallan: challan,
        challanNo: challan ? this.challanNo.trim() || null : null,
        bsrCode: challan ? this.bsrCode.trim() || null : null,
        challanSectionCode: challan ? this.challanSectionCode.trim() || null : null,
        challanMonth: challan ? this.challanMonth || null : null,
        lines: kept.map((l) => ({
          ledgerSourceId: l.ledgerSourceId,
          // Type and id travel together or not at all: the database refuses
          // half a mapping, and a line with no document chosen settles nothing.
          mappingTransactionTypeCode:
            this.reference(l.mappingTransactionId) === null ? null : l.mappingTransactionTypeCode,
          mappingTransactionId: this.reference(l.mappingTransactionId),
          amount: this.money(l.amount),
          lineMemo: l.lineMemo || null,
          projectId: l.projectId,
          isBillable: this.direction() === 'spend' && l.projectId !== null && l.isBillable,
          markupPercent:
            this.direction() === 'spend' && l.projectId !== null && l.isBillable && l.markupPercent !== ''
              ? Number(l.markupPercent)
              : null,
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
        this.goBack();
      } else {
        this.editingId = id;
        this.show('Draft saved.', false);
      }
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not save the document.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  async voidDocument(row: MoneyDocumentView): Promise<void> {
    const reason = prompt(
      `Void ${row.transactionNo}? Its ledger rows are withdrawn; the document and its number stay.`,
      '',
    );
    if (reason === null) return;

    this.busy.set(true);
    try {
      await this.req('POST', `${this.endpoint}/${row.documentId}/void`, {
        reason: reason || null,
      });
      this.goBack();
    } catch (err: unknown) {
      this.show(this.reason(err, 'Could not void the document.'), true);
    } finally {
      this.busy.set(false);
    }
  }

  startAllocation(): void {
    this.allocating.set(true);
  }

  onAllocationClosed(success: boolean): void {
    this.allocating.set(false);
    if (success) {
      this.show('Allocation successful.', false);
    }
  }

  async checkTds(): Promise<void> {
    if (this.direction() !== 'spend' || this.contactId === null || this.advanceTotal <= 0) return;
    this.busy.set(true);
    try {
      const quote = await this.req<TdsQuote>(
        'POST',
        '/api/spend-money/tds-quote',
        {
          contactId: this.contactId,
          transactionDate: this.transactionDate,
          advanceGrossAmount: this.advanceTotal,
        },
      );
      this.tdsPreview.set(quote);
    } catch {
      this.show('Could not work out TDS on the advance.', true);
    } finally {
      this.busy.set(false);
    }
  }

  private resetTds(): void {
    this.tdsAmount = '';
    this.tdsSectionCode = '';
    this.deductorTan = '';
    this.isTdsChallan = false;
    this.challanNo = '';
    this.bsrCode = '';
    this.challanSectionCode = '';
    this.challanMonth = '';
    this.tdsPreview.set(null);
  }

  private blankLine(): LineForm {
    const first = this.sources()[0]?.ledgerSourceId ?? null;
    return {
      ledgerSourceId: first,
      mappingTransactionTypeCode: this.sourceFor(first)?.settles ?? null,
      mappingTransactionId: '',
      amount: '',
      lineMemo: '',
      projectId: null,
      isBillable: false,
      markupPercent: '',
      billedInvoiceId: null,
    };
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
    if (this.direction() === 'spend' && this.isTdsChallan) {
      if (this.challanSectionCode.trim() === '' || this.challanMonth === '') {
        this.show('Choose the section and the month whose deductions the challan pays.', true);
        return false;
      }
    }
    if (post && !this.canPost) {
      this.show('A document can only post when its lines match the amount that moved.', true);
      return false;
    }
    return true;
  }

  private money(val: string | number): number {
    const n = typeof val === 'number' ? val : parseFloat(val);
    return isNaN(n) ? 0 : Math.round(n * 100) / 100;
  }

  private reference(val: string): number | null {
    const trimmed = val.trim();
    if (!trimmed) return null;
    const n = parseInt(trimmed, 10);
    return isNaN(n) ? null : n;
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

/** Today on the user's own clock: `toISOString` is UTC, which is yesterday in India until 05:30. */
function localToday(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}
