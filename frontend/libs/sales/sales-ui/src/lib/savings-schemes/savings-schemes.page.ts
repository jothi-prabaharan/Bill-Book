import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FormatSettingsService } from '@bill-book/currency-format';
import { SalesLookupService } from '@bill-book/sales-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  MoneyInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';
import { SalesPicker } from '../sales-picker';
import {
  BONUS_OPTIONS,
  BankAccountOption,
  InvoiceOption,
  STATUS_OPTIONS,
  SavingsScheme,
  SchemeDraft,
  SchemeEnrolment,
  actionsFor,
  bonusOf,
  emptyScheme,
  redeemableNow,
  schemeDraftOf,
  schemeProblem,
  schemeRequest,
  statusLabel,
  statusTag,
} from './savings-schemes.model';

const BASE = '/api/savings-schemes';

function today(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/**
 * Jewellery savings schemes (TK-141): enrolments on one tab, scheme
 * definitions on the other. Taking an instalment, redeeming and refunding are
 * done from an enrolment, and each is a real receipt, allocation or refund in
 * Accounting. Lists stack above their forms, so at ~360px everything is one
 * column.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-savings-schemes',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    BranchDateInputComponent,
    CheckboxComponent,
    LookupDialogComponent,
    MessageBoxComponent,
    MoneyInputComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
  ],
  templateUrl: './savings-schemes.page.html',
  styleUrl: './savings-schemes.page.scss',
})
export class SavingsSchemesPage implements OnInit {
  private readonly http = inject(HttpClient);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly tab = signal<'enrolments' | 'schemes'>('enrolments');
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);

  // ---- Schemes ----
  protected readonly schemes = signal<SavingsScheme[]>([]);
  protected readonly editingScheme = signal<SavingsScheme | 'new' | null>(null);
  protected schemeDraft: SchemeDraft = emptyScheme();
  protected readonly bonusOptions: BbSelectOption<string>[] = BONUS_OPTIONS.map((o) => ({ value: o.value, label: o.label }));
  protected readonly schemeColumns: ColumnDef[] = [
    { field: 'code', header: 'Code', width: '7rem' },
    { field: 'name', header: 'Name' },
    { field: 'instalmentAmount', header: 'Instalment', align: 'right', dataType: 'money', width: '8rem' },
    { field: 'months', header: 'Months', align: 'right', width: '5rem' },
    { field: 'bonusAmount', header: 'Bonus', align: 'right', dataType: 'money', width: '7rem' },
    { field: 'activeEnrolments', header: 'Members', align: 'right', width: '6rem' },
    { field: 'isActive', header: 'Open', isTemplate: true, width: '5rem' },
  ];

  // ---- Enrolments ----
  protected readonly enrolments = signal<SchemeEnrolment[]>([]);
  protected readonly selected = signal<SchemeEnrolment | null>(null);
  protected readonly enrolling = signal(false);
  protected statusFilter = '';
  protected readonly statusOptions: BbSelectOption<string>[] = STATUS_OPTIONS.map((o) => ({ value: o.value, label: o.label }));
  protected enrolSchemeId: number | null = null;
  protected enrolContactId: number | null = null;
  protected readonly enrolContactLabel = signal('');
  protected enrolStart: string | null = today();
  protected readonly enrolmentColumns: ColumnDef[] = [
    { field: 'enrolmentNo', header: 'Number', width: '9rem' },
    { field: 'schemeName', header: 'Scheme' },
    { field: 'contactId', header: 'Customer', width: '7rem' },
    { field: 'instalmentsPaid', header: 'Paid', isTemplate: true, width: '6rem' },
    { field: 'paidPrincipal', header: 'Paid in', align: 'right', dataType: 'money', width: '8rem' },
    { field: 'nextDueDate', header: 'Next due', isTemplate: true, width: '8rem' },
    { field: 'status', header: 'Status', isTemplate: true, width: '7rem' },
  ];

  // ---- Actions on one enrolment ----
  protected readonly action = signal<'pay' | 'redeem' | 'refund' | null>(null);
  protected readonly bankAccounts = signal<BankAccountOption[]>([]);
  protected readonly invoices = signal<InvoiceOption[]>([]);
  protected bankAccountId: number | null = null;
  protected actionDate: string | null = today();
  protected paymentMethod = 'Cash';
  protected referenceNo = '';
  protected invoiceId: number | null = null;
  protected closeEarly = false;
  protected readonly paymentMethods: BbSelectOption<string>[] = [
    { value: 'Cash', label: 'Cash' },
    { value: 'Upi', label: 'UPI' },
    { value: 'Card', label: 'Card' },
    { value: 'BankTransfer', label: 'Bank transfer' },
    { value: 'Cheque', label: 'Cheque' },
  ];

  protected readonly picker = new SalesPicker(inject(SalesLookupService), {
    customer: (row: LookupRow) => {
      this.enrolContactId = row.id;
      this.enrolContactLabel.set(SalesPicker.label(row));
    },
    item: () => undefined,
  });

  protected readonly schemeChoices = computed<BbSelectOption<number>[]>(() =>
    this.schemes()
      .filter((s) => s.isActive)
      .map((s) => ({ value: s.savingsSchemeId, label: `${s.code} · ${s.name} · ${this.formats.formatMoney(s.instalmentAmount)} × ${s.months}` })),
  );

  protected readonly bankChoices = computed<BbSelectOption<number>[]>(() =>
    this.bankAccounts().map((b) => ({ value: b.bankAccountId, label: b.accountName })),
  );

  protected readonly invoiceChoices = computed<BbSelectOption<number>[]>(() =>
    this.invoices().map((i) => ({
      value: i.invoiceId,
      label: `${i.documentNo ?? 'Invoice ' + i.invoiceId} · ${this.formats.formatDate(i.documentDate)} · owes ${this.formats.formatMoney(i.outstanding)}`,
    })),
  );

  protected readonly actions = computed(() => {
    const e = this.selected();
    return e ? actionsFor(e) : { pay: false, redeem: false, closeEarly: false, refund: false };
  });

  protected readonly statusLabel = statusLabel;
  protected readonly statusTag = statusTag;
  protected readonly redeemableNow = redeemableNow;

  protected get draftBonus(): number {
    const d = this.schemeDraft;
    return bonusOf(d.bonusType, d.bonusValue, d.instalmentAmount, d.months);
  }

  ngOnInit(): void {
    void this.loadSchemes();
    void this.loadEnrolments();
  }

  // ---- Schemes ----

  protected async loadSchemes(): Promise<void> {
    try {
      this.schemes.set(await firstValueFrom(this.http.get<SavingsScheme[]>(BASE)));
    } catch (error) {
      this.fail(error, 'The schemes could not be loaded.');
    }
  }

  protected newScheme(): void {
    this.schemeDraft = emptyScheme();
    this.editingScheme.set('new');
  }

  protected openScheme(s: SavingsScheme): void {
    this.schemeDraft = schemeDraftOf(s);
    this.editingScheme.set(s);
  }

  protected async saveScheme(): Promise<void> {
    const problem = schemeProblem(this.schemeDraft);
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    const editing = this.editingScheme();
    await this.run(async () => {
      const body = schemeRequest(this.schemeDraft);
      if (editing && editing !== 'new') {
        await firstValueFrom(this.http.put(`${BASE}/${editing.savingsSchemeId}`, body));
      } else {
        await firstValueFrom(this.http.post(BASE, body));
      }

      this.editingScheme.set(null);
      await this.loadSchemes();
    }, 'Scheme saved. Changes apply to customers who join from now on.');
  }

  // ---- Enrolments ----

  protected async loadEnrolments(): Promise<void> {
    try {
      const query = this.statusFilter ? `?status=${this.statusFilter}` : '';
      this.enrolments.set(await firstValueFrom(this.http.get<SchemeEnrolment[]>(`${BASE}/enrolments${query}`)));
    } catch (error) {
      this.fail(error, 'The enrolments could not be loaded.');
    }
  }

  protected startEnrol(): void {
    this.enrolSchemeId = null;
    this.enrolContactId = null;
    this.enrolContactLabel.set('');
    this.enrolStart = today();
    this.enrolling.set(true);
  }

  protected async enrol(): Promise<void> {
    if (!this.enrolSchemeId || !this.enrolContactId || !this.enrolStart) {
      this.messages.set([{ tone: 'error', text: 'Choose the scheme, the customer and the start date.' }]);
      return;
    }

    await this.run(async () => {
      const result = await firstValueFrom(
        this.http.post<{ id: number }>(`${BASE}/enrolments`, {
          savingsSchemeId: this.enrolSchemeId,
          contactId: this.enrolContactId,
          startDate: this.enrolStart,
        }),
      );
      this.enrolling.set(false);
      await this.loadEnrolments();
      await this.openEnrolment({ schemeEnrolmentId: result.id } as SchemeEnrolment);
    }, 'Customer enrolled.');
  }

  protected async openEnrolment(row: SchemeEnrolment): Promise<void> {
    try {
      this.selected.set(await firstValueFrom(this.http.get<SchemeEnrolment>(`${BASE}/enrolments/${row.schemeEnrolmentId}`)));
      this.action.set(null);
    } catch (error) {
      this.fail(error, 'The enrolment could not be opened.');
    }
  }

  protected async begin(kind: 'pay' | 'redeem' | 'refund'): Promise<void> {
    const e = this.selected();
    if (!e) return;

    this.actionDate = today();
    this.referenceNo = '';
    this.paymentMethod = 'Cash';
    this.invoiceId = null;
    this.closeEarly = false;
    this.action.set(kind);

    try {
      if (kind === 'redeem') {
        this.invoices.set(await firstValueFrom(this.http.get<InvoiceOption[]>(`${BASE}/enrolments/${e.schemeEnrolmentId}/invoices`)));
      } else if (!this.bankAccounts().length) {
        const accounts = await firstValueFrom(this.http.get<BankAccountOption[]>(`${BASE}/bank-accounts`));
        this.bankAccounts.set(accounts);
      }

      this.bankAccountId = this.bankAccounts().find((b) => b.isDefault)?.bankAccountId ?? this.bankAccounts()[0]?.bankAccountId ?? null;
    } catch (error) {
      this.fail(error, 'The choices could not be loaded.');
    }
  }

  protected async confirm(): Promise<void> {
    const e = this.selected();
    const kind = this.action();
    if (!e || !kind || !this.actionDate) return;

    if (kind !== 'redeem' && !this.bankAccountId) {
      this.messages.set([{ tone: 'error', text: 'Choose the bank or cash account.' }]);
      return;
    }

    if (kind === 'redeem' && !this.invoiceId) {
      this.messages.set([{ tone: 'error', text: 'Choose the invoice.' }]);
      return;
    }

    const id = e.schemeEnrolmentId;
    await this.run(async () => {
      if (kind === 'pay') {
        await firstValueFrom(this.http.post(`${BASE}/enrolments/${id}/instalments`, {
          bankAccountId: this.bankAccountId,
          paymentDate: this.actionDate,
          paymentMethod: this.paymentMethod,
          referenceNo: this.referenceNo.trim() || null,
        }));
      } else if (kind === 'redeem') {
        await firstValueFrom(this.http.post(`${BASE}/enrolments/${id}/redeem`, {
          invoiceId: this.invoiceId,
          redemptionDate: this.actionDate,
          closeEarly: this.closeEarly,
        }));
      } else {
        await firstValueFrom(this.http.post(`${BASE}/enrolments/${id}/refund`, {
          bankAccountId: this.bankAccountId,
          refundDate: this.actionDate,
          paymentMethod: this.paymentMethod,
          referenceNo: this.referenceNo.trim() || null,
        }));
      }

      this.action.set(null);
      await this.loadEnrolments();
      await this.openEnrolment(e);
    }, kind === 'pay' ? 'Instalment taken.' : kind === 'redeem' ? 'Redeemed against the invoice.' : 'Refund paid.');
  }

  protected date(value: string | null | undefined): string {
    return value ? this.formats.formatDate(value) : '—';
  }

  private async run(work: () => Promise<void>, success: string): Promise<void> {
    this.busy.set(true);
    this.messages.set([]);
    try {
      await work();
      this.messages.set([{ tone: 'success', text: success }]);
    } catch (error) {
      this.fail(error, 'That could not be done.');
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown, fallback: string): void {
    const text =
      error instanceof HttpErrorResponse && typeof error.error?.message === 'string' ? error.error.message : fallback;
    this.messages.set([{ tone: 'error', text }]);
  }
}
