import {
  BbSelectOption,
  CheckboxComponent,
  ColumnDef,
  ConfirmDialogService,
  DataGridCellTemplateDirective,
  DataGridComponent,
  EmailInputComponent,
  IconButtonComponent,
  InfoTooltipComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService, Currency, ADD_ONS, BUSINESS_TYPES, isAddOnAllowed, SessionContextService } from '@bill-book/auth';

interface Organization {
  orgId: string;
  orgCode: string;
  name: string;
  businessType?: string;
  addOns: string;
  baseCurrency: string;
  financialYearStartMonth: number;
  gstin: string | null;
  pan: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateId: number | null;
  postalCode: string | null;
  countryId: number;
  phoneNumber: string | null;
  mobileNumber: string | null;
  email: string | null;
  website: string | null;
  status: string;
  isFirst: boolean;
  isTrial: boolean;
  expiryDate: string | null;
}

interface State {
  stateId: number;
  stateCode: string;
  stateName: string;
}

type OrganizationForm = Omit<
  Organization,
  'orgId' | 'status' | 'isFirst' | 'isTrial' | 'expiryDate'
>;

/**
 * Settings › Branches. Each branch is a complete set of books — its own items,
 * contacts, stock, chart of accounts and numbering — sharing the head office's
 * database and separated by its organization id.
 *
 * Creating one is a small provisioning, not an insert: the books have to be
 * written before the branch is usable, so a new branch stays "setting up" until
 * every service has seeded it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-organizations-page',
  standalone: true,
  imports: [
    InfoTooltipComponent,
    CheckboxComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    IconButtonComponent,
    FormsModule,
    TextInputComponent,
    SelectComponent,
    EmailInputComponent,
  ],
  templateUrl: './organizations.page.html',
  styleUrl: './organizations.page.scss',
})
export class OrganizationsPage implements OnInit {

  /** Month numbers, which is what the branch stores. */
  protected readonly monthOptions: BbSelectOption<number>[] = Array.from(
    { length: 12 },
    (_, at) => ({ value: at + 1, label: `Month ${at + 1}` }),
  );

  

    protected readonly businessTypeOptions: BbSelectOption<string>[] = BUSINESS_TYPES.map((b) => ({ value: b.value, label: b.label }));

  protected readonly allAddOns = ADD_ONS;

  protected isAddOnAllowed(addOn: string): boolean {
    return isAddOnAllowed(this.form?.businessType, addOn);
  }

  protected hasAddOn(addOn: string): boolean {
    if (!this.form || !this.form.addOns) return false;
    return this.form.addOns.split(',').map(s => s.trim()).includes(addOn);
  }

  protected toggleAddOn(addOn: string, enabled: boolean): void {
    if (!this.form) return;
    const current = new Set((this.form.addOns || '').split(',').map(s => s.trim()).filter(s => s && s !== 'None'));
    if (enabled) {
      current.add(addOn);
    } else {
      current.delete(addOn);
    }
    this.form.addOns = current.size > 0 ? Array.from(current).join(', ') : 'None';
  }

  protected readonly currencyOptions = computed<BbSelectOption<string>[]>(() =>
    this.currencies().map((currency) => ({
      value: currency.code,
      label: `${currency.code} - ${currency.name}`,
    })),
  );

  protected readonly stateOptions = computed<BbSelectOption<number>[]>(() =>
    this.states().map((state) => ({
      value: state.stateId,
      label: `${state.stateCode} — ${state.stateName}`,
    })),
  );
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly session = inject(SessionContextService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  /** India. Replace with the head office's country once that is on the token. */
  private readonly defaultCountryId = 1;

  protected readonly isPaidPlan = computed(() => {
    const tier = this.session.context()?.planTier;
    return tier !== undefined && tier !== 'Trial';
  });

  protected readonly canAddBranch = computed(() => {
    if (this.rows().length === 0) return true;
    return this.isPaidPlan();
  });

  protected readonly rows = signal<Organization[]>([]);
  protected readonly states = signal<State[]>([]);
  protected readonly currencies = signal<Currency[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly editingId = signal<string | null>(null);

  protected readonly currentOrgId = signal<string>(localStorage.getItem('bb.orgId') ?? '');

  form: OrganizationForm = this.blank();

  columns: ColumnDef[] = [
    { field: 'orgCode', header: 'Code' },
    { field: 'name', header: 'Branch' },
    { field: 'city', header: 'City' },
    { field: 'stateId', header: 'State' },
    { field: 'gstin', header: 'GSTIN' },
    { field: 'baseCurrency', header: 'Currency' },
    { field: 'status', header: 'Status' },
    { field: 'actions', header: 'Actions' },
  ];

  ngOnInit(): void {
    void this.load();
    void this.loadStates();
    void this.loadCurrencies();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.rows.set(await this.get<Organization[]>('/api/organizations'));
    } catch {
      this.fail('Could not load branches.');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadStates(): Promise<void> {
    try {
      this.states.set(
        await this.get<State[]>(`/api/master/countries/${this.defaultCountryId}/states`),
      );
    } catch {
      // The GSTIN-versus-state check runs server-side regardless.
    }
  }

  private async loadCurrencies(): Promise<void> {
    try {
      this.currencies.set(await this.get<Currency[]>('/api/master/currencies'));
    } catch {
      // Ignore
    }
  }

  startAdd(): void {
    if (!this.canAddBranch()) {
      this.fail('Adding additional branches is a paid feature. Please upgrade your subscription to add more branches.');
      return;
    }
    this.editingId.set('');
    this.form = this.blank();
  }

  startEdit(row: Organization): void {
    this.editingId.set(row.orgId);
    this.form = {
      orgCode: row.orgCode,
      name: row.name,
      businessType: row.businessType,
      addOns: row.addOns,
      baseCurrency: row.baseCurrency,
      financialYearStartMonth: row.financialYearStartMonth,
      gstin: row.gstin,
      pan: row.pan,
      addressLine1: row.addressLine1,
      addressLine2: row.addressLine2,
      city: row.city,
      stateId: row.stateId,
      postalCode: row.postalCode,
      countryId: row.countryId,
      phoneNumber: row.phoneNumber,
      mobileNumber: row.mobileNumber,
      email: row.email,
      website: row.website,
    };
  }

  async save(): Promise<void> {
    const id = this.editingId();
    if (
      !this.hasText(this.form.orgCode) ||
      !this.hasText(this.form.name) ||
      !this.hasText(this.form.baseCurrency)
    ) {
      this.fail('Branch code, name and base currency are required.');
      return;
    }

    const body: OrganizationForm = {
      ...this.form,
      orgCode: this.form.orgCode.trim(),
      name: this.form.name.trim(),
      baseCurrency: this.form.baseCurrency.trim().toUpperCase(),
    };

    await this.run(async () => {
      if (id === '') {
        await this.send('POST', '/api/organizations', body);
      } else {
        await this.send('PUT', `/api/organizations/${id}`, body);
      }
      this.editingId.set(null);
    }, id === '' ? 'Branch created and its books set up.' : 'Branch saved.');
  }

  /** Re-runs the seeding for a branch that was created but never finished. */
  async retrySeeding(row: Organization): Promise<void> {
    await this.run(
      () => this.send('POST', `/api/organizations/${row.orgId}/retry-seeding`, {}),
      'Branch set up.',
    );
  }

  async suspend(row: Organization): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: 'Suspend Branch',
      message: `Suspend branch "${row.name}"? Users will not be able to switch to this branch.`,
      confirmText: 'Suspend',
      tone: 'danger',
    });
    if (!ok) return;

    await this.run(
      () => this.send('DELETE', `/api/organizations/${row.orgId}`, {}),
      'Branch suspended.',
    );
  }

  /**
   * Moves into another branch. The whole app follows, because a branch is a
   * separate set of books — so the page is reloaded rather than left showing
   * data from the branch just left.
   */
  async switchTo(row: Organization): Promise<void> {
    this.busy.set(true);
    try {
      await this.auth.switchOrganization(row.orgId);
      window.location.reload();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'Could not switch to that branch.');
      this.busy.set(false);
    }
  }

  /** The base currency is fixed once the branch exists — every posting converted to it. */
  protected get currencyLocked(): boolean {
    const id = this.editingId();
    return id !== null && id !== '';
  }

  protected stateNameOf(stateId: number | null): string {
    if (stateId === null) {
      return '—';
    }

    return this.states().find((s) => s.stateId === stateId)?.stateName ?? String(stateId);
  }

  private blank(): OrganizationForm {
    return {
      orgCode: '',
      name: '',
      businessType: 'Retail',
      addOns: 'None',
      baseCurrency: 'INR',
      financialYearStartMonth: 4,
      gstin: null,
      pan: null,
      addressLine1: null,
      addressLine2: null,
      city: null,
      stateId: null,
      postalCode: null,
      countryId: this.defaultCountryId,
      phoneNumber: null,
      mobileNumber: null,
      email: null,
      website: null,
    };
  }

  private async run(action: () => Promise<unknown>, ok: string | null): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await action();
      if (ok) {
        this.message.set(ok);
        this.messageIsError.set(false);
      }
      await this.load();
    } catch (err: unknown) {
      const anyErr = err as { error?: { message?: string } };
      this.fail(anyErr?.error?.message ?? 'That did not work.');
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }

  private hasText(value: string | null | undefined): boolean {
    return (value ?? '').trim().length > 0;
  }

  private get<T>(url: string): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http.get<T>(url).subscribe({ next: resolve, error: reject }),
    );
  }

  private send<T = unknown>(
    method: 'POST' | 'PUT' | 'PATCH' | 'DELETE',
    url: string,
    body: unknown,
  ): Promise<T> {
    return new Promise((resolve, reject) =>
      this.http
        .request<T>(method, url, { body })
        .subscribe({ next: resolve as (value: T) => void, error: reject }),
    );
  }
}





