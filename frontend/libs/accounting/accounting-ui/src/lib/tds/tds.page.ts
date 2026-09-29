import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  FileInputComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';
import { financialYearLabel, financialYearOf, monthLabel, quarterOf, recentFinancialYears } from './tds-math';

export interface TdsSectionRow {
  tdsSectionId: number;
  sectionCode: string;
  natureOfPayment: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  rateIndividual: number;
  rateOther: number;
  rateNoPan: number;
  singleThreshold: number | null;
  aggregateThreshold: number | null;
  thresholdBasis: 'WholeAmount' | 'ExcessOnly';
  isActive: boolean;
  isUsed: boolean;
}

export interface TdsEntryRow {
  tdsEntryId: number;
  direction: 'Deducted' | 'Suffered';
  kind: 'Credit' | 'Advance' | 'Receipt';
  transactionTypeCode: string;
  transactionId: number;
  documentNo: string | null;
  documentDate: string;
  contactId: number;
  contactName: string | null;
  sectionCode: string | null;
  pan: string | null;
  tan: string | null;
  amount: number;
  tdsBase: number;
  rate: number;
  certificateNo: string | null;
  tdsAmount: number;
  challanSpendMoneyId: number | null;
  challanNo: string | null;
  note: string | null;
}

export interface TdsOutstandingRow {
  sectionCode: string;
  month: string;
  tdsAmount: number;
  deductions: number;
  dueDate: string;
}

interface Form26qCheck {
  challans: number;
  deductions: number;
  warnings: string[];
}

interface Form26asSummary {
  form26asImportId: number;
  financialYear: number;
  pan: string | null;
  fileName: string;
  importedAt: string | null;
  matchedCount: number;
  differenceCount: number;
  missingInBooksCount: number;
  missingIn26asCount: number;
  creditAtRisk: number;
}

interface Form26asLineRow {
  form26asLineId: number;
  matchState: 'Matched' | 'AmountDiffers' | 'MissingInBooks' | 'MissingIn26as';
  deductorTan: string;
  deductorName: string | null;
  sectionCode: string | null;
  transactionDate: string | null;
  statementAmountPaid: number | null;
  statementTds: number | null;
  booksTds: number | null;
  receiveMoneyId: number | null;
  documentNo: string | null;
  note: string | null;
}

interface Form26asDetail extends Form26asSummary {
  lines: Form26asLineRow[];
}

type Tab = 'sections' | 'register' | 'due' | 'return' | 'statement';

const MATCH_LABELS: Record<Form26asLineRow['matchState'], string> = {
  Matched: 'Matched',
  AmountDiffers: 'Amount differs',
  MissingInBooks: 'In 26AS, not in books',
  MissingIn26as: 'In books, not in 26AS',
};

/**
 * Accounting › TDS (TK-117): the section master, the register of what was
 * deducted and what customers withheld, what is due to be deposited, the 26Q
 * data for a quarter, and the Form 26AS reconciliation.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-tds-page',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    CheckboxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    DateInputComponent,
    FileInputComponent,
    MessageBoxComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
  ],
  templateUrl: './tds.page.html',
  styleUrl: './tds.page.scss',
})
export class TdsPage implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly tab = signal<Tab>('sections');
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  protected readonly sections = signal<TdsSectionRow[]>([]);
  protected readonly editing = signal<number | 'new' | null>(null);
  protected form = this.blankSection();

  protected readonly years = recentFinancialYears(this.today());
  protected year = financialYearOf(this.today());
  protected direction: 'Deducted' | 'Suffered' = 'Deducted';
  protected readonly register = signal<TdsEntryRow[]>([]);
  protected readonly registerTotal = computed(() => this.register().reduce((sum, r) => sum + r.tdsAmount, 0));

  protected readonly outstanding = signal<TdsOutstandingRow[]>([]);

  protected quarter = quarterOf(this.today());
  protected readonly check = signal<Form26qCheck | null>(null);

  protected readonly statements = signal<Form26asSummary[]>([]);
  protected readonly statement = signal<Form26asDetail | null>(null);

  /** What the server or a rule said, for the message box. */
  protected readonly messages = computed<UiMessage[]>(() => {
    const text = this.message();
    return text ? [{ tone: this.messageIsError() ? 'error' : 'success', text }] : [];
  });

  protected readonly yearOptions: BbSelectOption<number>[] = this.years.map((y) => ({ value: y, label: financialYearLabel(y) }));
  protected readonly directionOptions: BbSelectOption<string>[] = [
    { value: 'Deducted', label: 'TDS we deducted' },
    { value: 'Suffered', label: 'TDS customers withheld' },
  ];
  protected readonly quarterOptions: BbSelectOption<number>[] = [
    { value: 1, label: 'Q1 · April to June' },
    { value: 2, label: 'Q2 · July to September' },
    { value: 3, label: 'Q3 · October to December' },
    { value: 4, label: 'Q4 · January to March' },
  ];

  protected readonly sectionColumns: ColumnDef[] = [
    { field: 'sectionCode', header: 'Section' },
    { field: 'natureOfPayment', header: 'Nature of payment' },
    { field: 'rateIndividual', header: 'Individual', numeric: true },
    { field: 'rateOther', header: 'Others', numeric: true },
    { field: 'rateNoPan', header: 'No PAN', numeric: true },
    { field: 'threshold', header: 'Threshold', sortable: false, filterable: false },
    { field: 'effectiveFrom', header: 'In force' },
    { field: 'actions', header: '', sortable: false, filterable: false },
  ];

  protected readonly registerColumns: ColumnDef[] = [
    { field: 'documentDate', header: 'Date' },
    { field: 'documentNo', header: 'Document' },
    { field: 'contactName', header: 'Contact' },
    { field: 'sectionCode', header: 'Section' },
    { field: 'amount', header: 'Amount', numeric: true },
    { field: 'tdsBase', header: 'Taxed on', numeric: true },
    { field: 'tdsAmount', header: 'TDS', numeric: true },
    { field: 'challanNo', header: 'Deposited', filterable: false },
  ];

  protected readonly outstandingColumns: ColumnDef[] = [
    { field: 'month', header: 'Month' },
    { field: 'sectionCode', header: 'Section' },
    { field: 'deductions', header: 'Deductions', numeric: true },
    { field: 'tdsAmount', header: 'To deposit', numeric: true },
    { field: 'dueDate', header: 'Due' },
  ];

  protected readonly statementColumns: ColumnDef[] = [
    { field: 'matchState', header: 'State' },
    { field: 'deductorName', header: 'Deductor' },
    { field: 'transactionDate', header: 'Date' },
    { field: 'statementTds', header: '26AS TDS', numeric: true },
    { field: 'booksTds', header: 'Books TDS', numeric: true },
    { field: 'documentNo', header: 'Receipt' },
  ];

  protected readonly sectionRowClass = (row: TdsSectionRow): string => (row.isActive ? '' : 'row--muted');
  protected readonly outstandingRowClass = (row: TdsOutstandingRow): string => (this.overdue(row) ? 'row--danger' : '');

  protected readonly yearLabel = financialYearLabel;
  protected readonly monthLabel = monthLabel;
  protected matchLabel(state: string): string {
    return MATCH_LABELS[state as Form26asLineRow['matchState']] ?? state;
  }

  ngOnInit(): void {
    void this.loadSections();
  }

  protected async show(tab: Tab): Promise<void> {
    this.tab.set(tab);
    this.message.set(null);
    switch (tab) {
      case 'sections':
        return this.loadSections();
      case 'register':
        return this.loadRegister();
      case 'due':
        return this.loadOutstanding();
      case 'return':
        return this.loadCheck();
      case 'statement':
        return this.loadStatements();
    }
  }

  // ---- Sections -----------------------------------------------------------

  protected async loadSections(): Promise<void> {
    await this.run(async () => this.sections.set(await this.req<TdsSectionRow[]>('GET', '/api/tds/sections')), 'Could not load the TDS sections.');
  }

  protected startNew(): void {
    this.form = this.blankSection();
    this.editing.set('new');
  }

  protected startEdit(row: TdsSectionRow): void {
    this.form = {
      sectionCode: row.sectionCode,
      natureOfPayment: row.natureOfPayment,
      effectiveFrom: row.effectiveFrom,
      effectiveTo: row.effectiveTo ?? '',
      rateIndividual: row.rateIndividual,
      rateOther: row.rateOther,
      rateNoPan: row.rateNoPan,
      singleThreshold: row.singleThreshold,
      aggregateThreshold: row.aggregateThreshold,
      excessOnly: row.thresholdBasis === 'ExcessOnly',
      isActive: row.isActive,
    };
    this.editing.set(row.tdsSectionId);
  }

  protected validSection(): boolean {
    return this.form.sectionCode.trim().length > 0 && this.form.natureOfPayment.trim().length > 0 && this.form.effectiveFrom.length > 0;
  }

  protected async saveSection(): Promise<void> {
    if (!this.validSection()) {
      this.say('Enter the section code, the nature of payment and the date it applies from.', true);
      return;
    }

    const body = {
      sectionCode: this.form.sectionCode.trim(),
      natureOfPayment: this.form.natureOfPayment.trim(),
      effectiveFrom: this.form.effectiveFrom,
      effectiveTo: this.form.effectiveTo || null,
      rateIndividual: this.form.rateIndividual ?? 0,
      rateOther: this.form.rateOther ?? 0,
      rateNoPan: this.form.rateNoPan ?? 0,
      singleThreshold: this.form.singleThreshold || null,
      aggregateThreshold: this.form.aggregateThreshold || null,
      thresholdBasis: this.form.excessOnly ? 'ExcessOnly' : 'WholeAmount',
      isActive: this.form.isActive,
    };

    const id = this.editing();
    await this.run(async () => {
      if (id === 'new') {
        await this.req('POST', '/api/tds/sections', body);
      } else {
        await this.req('PUT', `/api/tds/sections/${id}`, body);
      }
      this.editing.set(null);
      this.say('Saved.', false);
      this.sections.set(await this.req<TdsSectionRow[]>('GET', '/api/tds/sections'));
    }, 'Could not save the section.');
  }

  protected threshold(row: TdsSectionRow): string {
    const parts: string[] = [];
    if (row.singleThreshold != null) {
      parts.push(`${this.formats.formatMoney(row.singleThreshold)} a document`);
    }
    if (row.aggregateThreshold != null) {
      parts.push(`${this.formats.formatMoney(row.aggregateThreshold)} a year${row.thresholdBasis === 'ExcessOnly' ? ', excess only' : ''}`);
    }
    return parts.length === 0 ? 'None' : parts.join(' · ');
  }

  // ---- Register -----------------------------------------------------------

  protected async loadRegister(): Promise<void> {
    await this.run(
      async () => this.register.set(await this.req<TdsEntryRow[]>('GET', `/api/tds/register?financialYear=${this.year}&direction=${this.direction}`)),
      'Could not load the TDS register.',
    );
  }

  protected documentLink(row: TdsEntryRow): string | null {
    switch (row.transactionTypeCode) {
      case 'BIL':
        return `/purchase/bills/${row.transactionId}`;
      case 'SPM':
        return '/banking/spend-money';
      case 'RCM':
        return '/banking/receive-money';
      default:
        return null;
    }
  }

  // ---- Deposits due -------------------------------------------------------

  protected async loadOutstanding(): Promise<void> {
    await this.run(async () => this.outstanding.set(await this.req<TdsOutstandingRow[]>('GET', '/api/tds/outstanding')), 'Could not load what is due.');
  }

  protected overdue(row: TdsOutstandingRow): boolean {
    return row.dueDate < this.today();
  }

  // ---- 26Q ----------------------------------------------------------------

  protected async loadCheck(): Promise<void> {
    await this.run(
      async () => this.check.set(await this.req<Form26qCheck>('GET', `/api/tds/26q/check?financialYear=${this.year}&quarter=${this.quarter}`)),
      'Could not check the quarter.',
    );
  }

  protected async download26q(): Promise<void> {
    await this.run(async () => {
      const blob = await new Promise<Blob>((resolve, reject) =>
        this.http
          .get(`/api/tds/26q?financialYear=${this.year}&quarter=${this.quarter}`, { responseType: 'blob' })
          .subscribe({ next: resolve, error: reject }),
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `26Q-${financialYearLabel(this.year)}-Q${this.quarter}.zip`;
      link.click();
      URL.revokeObjectURL(url);
    }, 'Could not download the 26Q data.');
  }

  // ---- 26AS ---------------------------------------------------------------

  protected async loadStatements(): Promise<void> {
    await this.run(async () => {
      this.statements.set(await this.req<Form26asSummary[]>('GET', '/api/tds/26as'));
      const first = this.statements()[0];
      this.statement.set(first ? await this.req<Form26asDetail>('GET', `/api/tds/26as/${first.form26asImportId}`) : null);
    }, 'Could not load the Form 26AS imports.');
  }

  protected async openStatement(id: number): Promise<void> {
    await this.run(async () => this.statement.set(await this.req<Form26asDetail>('GET', `/api/tds/26as/${id}`)), 'Could not open that import.');
  }

  protected async upload(files: readonly File[]): Promise<void> {
    const file = files[0];
    if (!file) {
      return;
    }

    const body = new FormData();
    body.append('file', file, file.name);
    await this.run(async () => {
      const created = await this.req<{ form26asImportId: number }>('POST', '/api/tds/26as', body);
      this.statements.set(await this.req<Form26asSummary[]>('GET', '/api/tds/26as'));
      await this.openStatement(created.form26asImportId);
    }, 'Could not import that file.');
  }

  protected async removeStatement(row: Form26asSummary): Promise<void> {
    if (!confirm(`Delete the import of ${row.fileName}? The file and its matching are removed; your books are not touched.`)) {
      return;
    }

    await this.run(async () => {
      await this.req('DELETE', `/api/tds/26as/${row.form26asImportId}`);
      await this.loadStatements();
    }, 'Could not delete that import.');
  }

  // ---- Plumbing -----------------------------------------------------------

  private blankSection() {
    return {
      sectionCode: '',
      natureOfPayment: '',
      effectiveFrom: this.today(),
      effectiveTo: '',
      rateIndividual: 0 as number | null,
      rateOther: 0 as number | null,
      rateNoPan: 20 as number | null,
      singleThreshold: null as number | null,
      aggregateThreshold: null as number | null,
      excessOnly: false,
      isActive: true,
    };
  }

  private async run(work: () => Promise<void>, fallback: string): Promise<void> {
    this.busy.set(true);
    try {
      await work();
    } catch (err: unknown) {
      const failure = err as { error?: { message?: string } };
      this.say(failure?.error?.message ?? fallback, true);
    } finally {
      this.busy.set(false);
    }
  }

  private say(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private req<T>(method: string, url: string, body?: unknown): Promise<T> {
    return new Promise((resolve, reject) => this.http.request<T>(method, url, { body }).subscribe({ next: resolve, error: reject }));
  }
}
