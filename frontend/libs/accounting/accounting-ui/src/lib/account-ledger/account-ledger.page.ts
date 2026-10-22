import { HttpClient, HttpParams } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BbSelectOption,
  BranchDateInputComponent,
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  MoneyCellComponent,
  RegisterDatePipe,
  SelectComponent,
  UiMessage,
  rangeForPreset,
  toIsoDate,
} from '@bill-book/ui-components';
import { combineLatest } from 'rxjs';
import {
  GeneralLedgerReportResult,
  GeneralLedgerSummaryResult,
  LedgerGridRow,
  SummaryGridRow,
  isProfitAndLoss,
  ledgerEquation,
  ledgerRows,
  sideLetter,
  summaryRows,
} from './account-ledger.models';

type FilterName = 'accountId' | 'from' | 'to' | 'contactId' | 'projectId' | 'isZeroBalanceShown';

interface AccountOption {
  accountId: number;
  accountCode: string;
  accountName: string;
  depth?: number;
}

interface ContactOption {
  contactId: number;
  contactCode: string;
  displayName: string;
}

interface ProjectOption {
  projectId: number;
  projectCode: string;
  projectName: string;
}

/**
 * The General ledger (TK-216): one account's postings over a period, with the
 * balance it opened at, a running balance down the page and the balance it closed
 * at, or, with no account chosen, every account's opening balance, debits,
 * credits and closing balance for the same period.
 *
 * Both dates are required, so there is no "all dates": the page opens on the
 * financial year to date and runs straight away. A link from the trial balance or
 * from the summary carries the account and the period in the address, so a ledger
 * can be sent to someone and reloaded. Every balance is on the account's normal
 * side, as the server gives it. A missing or out-of-order date shows at its field;
 * a refusal or a failure shows in the message box.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-account-ledger-page',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    BranchDateInputComponent,
    CheckboxComponent,
    SelectComponent,
    MessageBoxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    MoneyCellComponent,
    RegisterDatePipe,
  ],
  templateUrl: './account-ledger.page.html',
  styleUrl: './account-ledger.page.scss',
})
export class AccountLedgerPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly formats = inject(FormatSettingsService);
  private readonly destroyRef = inject(DestroyRef);

  /** Phone width, where each posting is a small card of its own rather than a row of columns. */
  protected readonly narrow = signal(false);

  protected readonly filters = new FormGroup({
    accountId: new FormControl<number | null>(null),
    from: new FormControl<string | null>(rangeForPreset('this-financial-year').from, Validators.required),
    to: new FormControl<string | null>(toIsoDate(new Date()), Validators.required),
    contactId: new FormControl<number | null>(null),
    projectId: new FormControl<number | null>(null),
    isZeroBalanceShown: new FormControl(false, { nonNullable: true }),
  });

  protected readonly accounts = signal<AccountOption[]>([]);
  protected readonly contacts = signal<ContactOption[]>([]);
  protected readonly projects = signal<ProjectOption[]>([]);
  protected readonly choicesLoaded = signal(false);

  protected readonly ledger = signal<GeneralLedgerReportResult | null>(null);
  protected readonly summary = signal<GeneralLedgerSummaryResult | null>(null);
  protected readonly busy = signal(false);
  private readonly failure = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<FilterName, string>>>({});

  /** The account chosen: its ledger. None: every account's summary. Follows the address as well as the picker. */
  protected readonly chosen = signal<number | null>(null);

  protected readonly accountOptions = computed<BbSelectOption<number>[]>(() =>
    this.accounts().map((account) => {
      const depth = account.depth ?? 0;
      const prefix = '  '.repeat(depth) + (depth > 0 ? '↳ ' : '');
      return { value: account.accountId, label: `${prefix}${account.accountCode} — ${account.accountName}` };
    }),
  );

  protected readonly contactOptions = computed<BbSelectOption<number>[]>(() =>
    this.contacts().map((c) => ({ value: c.contactId, label: `${c.contactCode} — ${c.displayName}` })),
  );

  protected readonly projectOptions = computed<BbSelectOption<number>[]>(() =>
    this.projects().map((p) => ({ value: p.projectId, label: `${p.projectCode} — ${p.projectName}` })),
  );

  protected readonly ledgerGridRows = computed<LedgerGridRow[]>(() => {
    const report = this.ledger();
    return report ? ledgerRows(report) : [];
  });

  protected readonly summaryGridRows = computed<SummaryGridRow[]>(() => {
    const report = this.summary();
    return report ? summaryRows(report) : [];
  });

  /** The ledger's columns; the balance is headed with the side it is on. */
  protected readonly ledgerColumns = computed<ColumnDef[]>(() => {
    const side = this.ledger()?.normalSide;
    const width = '7rem';
    return [
      { field: 'ledgerDate', header: 'Date', width: '6rem' },
      { field: 'document', header: 'Document', width: '11rem' },
      { field: 'contactName', header: 'Contact', width: '9rem' },
      { field: 'description', header: 'Description' },
      { field: 'debit', header: 'Debit', numeric: true, width },
      { field: 'credit', header: 'Credit', numeric: true, width },
      { field: 'balance', header: side ? `Balance (${sideLetter(side)})` : 'Balance', numeric: true, width },
    ];
  });

  protected readonly summaryColumns: ColumnDef[] = [
    { field: 'accountName', header: 'Account' },
    { field: 'openingBalance', header: 'Opening', numeric: true, width: '10.5rem' },
    { field: 'debit', header: 'Debits', numeric: true, width: '9.5rem' },
    { field: 'credit', header: 'Credits', numeric: true, width: '9.5rem' },
    { field: 'closingBalance', header: 'Closing', numeric: true, width: '10.5rem' },
  ];

  protected readonly ledgerRowClass = (row: LedgerGridRow): string =>
    row.kind === 'opening' ? 'row--subtotal' : row.kind === 'closing' ? 'row--total' : '';

  protected readonly summaryRowClass = (row: SummaryGridRow): string => (row.kind === 'total' ? 'row--total' : '');

  protected readonly sideLetter = sideLetter;

  /** The identity the closing balance satisfies, in words. */
  protected readonly equation = computed(() => {
    const report = this.ledger();
    return report ? ledgerEquation(report, (value) => this.formats.formatMoney(value)) : '';
  });

  /** A note for an income or expense account, which opens from the start of its financial year. */
  protected readonly yearNote = computed(() => {
    const report = this.ledger();
    return report && isProfitAndLoss(report.accountTypeId)
      ? "An income or expense account opens at what was posted since the financial year began, so its closing balance is the Trial balance's."
      : '';
  });

  protected readonly messages = computed<UiMessage[]>(() => {
    const failure = this.failure();
    return failure ? [{ tone: 'error', text: failure }] : [];
  });

  ngOnInit(): void {
    if (typeof window !== 'undefined' && window.matchMedia) {
      const phone = window.matchMedia('(max-width: 640px)');
      const follow = (): void => this.narrow.set(phone.matches);
      follow();
      phone.addEventListener('change', follow);
      this.destroyRef.onDestroy(() => phone.removeEventListener('change', follow));
    }

    for (const name of Object.keys(this.filters.controls) as FilterName[]) {
      const control: AbstractControl = this.filters.controls[name];
      control.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.clearServerError(name));
    }

    // Choosing an account puts it in the address, so the page can be linked and reloaded.
    this.filters.controls.accountId.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((id) => {
      this.chosen.set(id);
      void this.chooseAccount();
    });

    // The account and the period can arrive in the address: the trial balance and the
    // summary link straight here, and a ledger worth reading is a ledger worth sending.
    combineLatest([this.route.paramMap, this.route.queryParamMap])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(([params, query]) => this.apply(params, query));

    void this.loadChoices();
  }

  /** The words under a field: the server's refusal, else a missing required date once touched. */
  protected err(name: FilterName, label: string): string {
    const server = this.serverErrors()[name];
    if (server) {
      return server;
    }

    const control = this.filters.controls[name];
    if (!control.touched || !control.errors) {
      return '';
    }

    return control.errors['required'] ? `${label} is required.` : `Check the ${label.toLowerCase()}.`;
  }

  /** The period a link to an account carries, so the ledger opens on the dates the summary was run for. */
  protected period(): { from: string | null; to: string | null } {
    const { from, to } = this.filters.getRawValue();
    return { from, to };
  }

  /** Runs for the account and period now in the filters. */
  async run(): Promise<void> {
    this.filters.markAllAsTouched();
    if (this.filters.invalid) {
      return;
    }

    const value = this.filters.getRawValue();
    const ticket = ++this.runTicket;
    this.busy.set(true);
    this.failure.set(null);
    this.serverErrors.set({});

    try {
      let params = new HttpParams().set('from', value.from as string).set('to', value.to as string);

      if (value.accountId !== null) {
        if (value.contactId !== null) {
          params = params.set('contactId', value.contactId);
        }
        if (value.projectId !== null) {
          params = params.set('projectId', value.projectId);
        }

        const result = await this.get<GeneralLedgerReportResult>(`/api/ledger/accounts/${value.accountId}`, params);
        if (ticket === this.runTicket) {
          this.ledger.set(result);
          this.summary.set(null);
        }
      } else {
        if (value.isZeroBalanceShown) {
          params = params.set('isZeroBalanceShown', true);
        }

        const result = await this.get<GeneralLedgerSummaryResult>('/api/ledger/summary', params);
        if (ticket === this.runTicket) {
          this.summary.set(result);
          this.ledger.set(null);
        }
      }
    } catch (err: unknown) {
      if (ticket === this.runTicket) {
        this.showFailure(err);
      }
    } finally {
      if (ticket === this.runTicket) {
        this.busy.set(false);
      }
    }
  }

  /** Each run is numbered, so a slower answer to an earlier one never replaces a later one. */
  private runTicket = 0;

  private async chooseAccount(): Promise<void> {
    const { accountId, from, to, contactId, projectId } = this.filters.getRawValue();
    await this.router.navigate(accountId === null ? ['/accounting/ledger'] : ['/accounting/ledger', accountId], {
      queryParams: { from, to, contactId, projectId },
      replaceUrl: true,
    });
  }

  /** Puts what the address names into the filters, without announcing it as a change, and runs. */
  private apply(params: ParamMap, query: ParamMap): void {
    const id = (raw: string | null): number | null => {
      const value = Number(raw);
      return raw !== null && Number.isFinite(value) && value > 0 ? value : null;
    };

    const controls = this.filters.controls;
    this.filters.patchValue(
      {
        accountId: id(params.get('accountId')),
        from: query.get('from') ?? controls.from.value,
        to: query.get('to') ?? controls.to.value,
        contactId: id(query.get('contactId')),
        projectId: id(query.get('projectId')),
      },
      { emitEvent: false },
    );
    this.chosen.set(controls.accountId.value);

    void this.run();
  }

  private async loadChoices(): Promise<void> {
    const [accounts, contacts, projects] = await Promise.all([
      this.get<AccountOption[]>('/api/accounts').catch(() => [] as AccountOption[]),
      this.get<ContactOption[]>('/api/contacts').catch(() => [] as ContactOption[]),
      this.get<ProjectOption[]>('/api/projects').catch(() => [] as ProjectOption[]),
    ]);
    this.accounts.set(accounts);
    this.contacts.set(contacts);
    this.projects.set(projects);
    this.choicesLoaded.set(true);
  }

  private get<T>(url: string, params?: HttpParams): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url, { params }).subscribe({ next: resolve, error: reject }),
    );
  }

  /** A field's refusal goes to that field; anything else to the message box. */
  private showFailure(err: unknown): void {
    const failure = err as {
      status?: number;
      error?: { message?: string; errors?: Record<string, string[]> } | null;
    };

    if (failure?.status === 403) {
      this.failure.set('You do not have access to the general ledger on this branch.');
      return;
    }

    if (failure?.status === 404) {
      this.failure.set('That account is not on this branch.');
      return;
    }

    const fieldErrors: Partial<Record<FilterName, string>> = {};
    for (const [key, words] of Object.entries(failure?.error?.errors ?? {})) {
      const name = (key.charAt(0).toLowerCase() + key.slice(1)) as FilterName;
      if (name in this.filters.controls && words?.[0]) {
        fieldErrors[name] = words[0];
      }
    }

    if (Object.keys(fieldErrors).length > 0) {
      this.serverErrors.set(fieldErrors);
      return;
    }

    this.failure.set(failure?.error?.message || 'The general ledger could not be run. Please try again.');
  }

  private clearServerError(name: FilterName): void {
    if (this.serverErrors()[name]) {
      const { [name]: _cleared, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }
}
