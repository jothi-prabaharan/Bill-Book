import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  MaintenanceContractApiService,
  AmcContract,
  AmcVisit,
  BILLING_FREQUENCIES,
  CONTRACT_STATUSES,
  COVERAGES,
  ContractStatus,
  RecordVisit,
  SaveContract,
  VISIT_KINDS,
  daysLeft,
  takesChanges,
  termsEditable,
} from '@bill-book/maintenance-contract-core';
import { readApiFailure } from '@bill-book/api-client';
import { IfCanDirective } from '@bill-book/auth';
import { FacilityApiService } from '@bill-book/facility-core';
import {
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  DateInputComponent,
  MessageBoxComponent,
  MoneyInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  UiMessage,
} from '@bill-book/ui-components';

/**
 * Maintenance › AMC contracts (S8, TK-68): who maintains what, until when, and
 * the visits made under each contract. A draft is activated once its assets
 * are listed, and an asset may be under only one active contract at a time.
 * A renewal reminder goes to the contract's reminder address before it ends.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-maintenance-contract-contracts-page',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    TextInputComponent,
    DateInputComponent,
    MoneyInputComponent,
    NumberInputComponent,
    SelectComponent,
    MessageBoxComponent,
    IfCanDirective,
  ],
  templateUrl: './contracts.page.html',
  styleUrl: '../maintenance-contract-page.scss',
})
export class ContractsPage implements OnInit {
  private readonly api = inject(MaintenanceContractApiService);
  private readonly facility = inject(FacilityApiService);

  protected readonly rows = signal<AmcContract[]>([]);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly busy = signal(false);
  protected readonly editingId = signal<number | null | undefined>(undefined);
  protected readonly editingStatus = signal<ContractStatus>('Draft');
  protected readonly open = signal<AmcContract | null>(null);
  protected readonly visits = signal<AmcVisit[]>([]);

  protected readonly vendorOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly assetOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly coveredOptions = computed(() => {
    const covered = this.open()?.facilityAssetIds ?? [];
    return this.assetOptions().filter((o) => covered.includes(o.value));
  });

  protected readonly termsLocked = computed(() => !termsEditable(this.editingStatus()));

  protected status: ContractStatus | null = null;
  protected form: SaveContract = ContractsPage.blank();
  protected assetToAdd: number | null = null;
  protected visit: RecordVisit = ContractsPage.blankVisit();
  protected terminating = false;
  protected reason = '';

  protected readonly statuses = [...CONTRACT_STATUSES];
  protected readonly frequencies = [...BILLING_FREQUENCIES];
  protected readonly coverages = [...COVERAGES];
  protected readonly visitKinds = [...VISIT_KINDS];

  protected readonly columns: ColumnDef[] = [
    { field: 'contractNo', header: 'Contract' },
    { field: 'vendor', header: 'Vendor' },
    { field: 'endDate', header: 'Ends' },
    { field: 'assets', header: 'Assets' },
    { field: 'contractStatus', header: 'Status' },
    { field: 'actions', header: '' },
  ];

  ngOnInit(): void {
    void this.start();
  }

  protected vendor(id: number): string {
    return this.vendorOptions().find((o) => o.value === id)?.label ?? `Contact ${id}`;
  }

  protected asset(id: number | null): string {
    return id === null ? '—' : (this.assetOptions().find((o) => o.value === id)?.label ?? `Asset ${id}`);
  }

  protected label(status: ContractStatus): string {
    return CONTRACT_STATUSES.find((s) => s.value === status)?.label ?? status;
  }

  protected ends(row: AmcContract): string {
    if (row.contractStatus !== 'Active') return row.endDate;
    const left = daysLeft(row.endDate, ContractsPage.today());
    return left <= row.renewalReminderDays ? `${row.endDate} (${left} days left)` : row.endDate;
  }

  protected canChange(row: AmcContract): boolean {
    return takesChanges(row.contractStatus);
  }

  protected startAdd(): void {
    this.form = ContractsPage.blank();
    this.editingStatus.set('Draft');
    this.editingId.set(null);
  }

  protected edit(row: AmcContract): void {
    const { amcContractId, contractStatus, terminationReason, visitsMade, ...rest } = row;
    void terminationReason;
    void visitsMade;
    this.form = { ...rest, facilityAssetIds: [...rest.facilityAssetIds] };
    this.editingStatus.set(contractStatus);
    this.editingId.set(amcContractId);
  }

  protected addAsset(): void {
    if (this.assetToAdd !== null && !this.form.facilityAssetIds.includes(this.assetToAdd)) {
      this.form.facilityAssetIds = [...this.form.facilityAssetIds, this.assetToAdd];
    }
    this.assetToAdd = null;
  }

  protected removeAsset(id: number): void {
    this.form.facilityAssetIds = this.form.facilityAssetIds.filter((a) => a !== id);
  }

  protected async view(row: AmcContract): Promise<void> {
    this.editingId.set(undefined);
    this.terminating = false;
    this.visit = ContractsPage.blankVisit();
    await this.refreshOpen(row.amcContractId);
  }

  protected async load(): Promise<void> {
    try {
      this.rows.set(await this.api.contracts(this.status));
    } catch (error) {
      this.fail(error);
    }
  }

  protected async save(): Promise<void> {
    await this.run('The contract is saved.', async () => {
      await this.api.save(this.editingId() ?? null, this.form);
      this.editingId.set(undefined);
    });
  }

  protected async activate(): Promise<void> {
    const row = this.open();
    if (row) await this.run('The contract is active.', () => this.api.activate(row.amcContractId));
  }

  protected async terminate(): Promise<void> {
    const row = this.open();
    if (!row) return;
    await this.run('The contract is terminated.', async () => {
      await this.api.terminate(row.amcContractId, this.reason);
      this.terminating = false;
      this.reason = '';
    });
  }

  protected async recordVisit(): Promise<void> {
    const row = this.open();
    if (!row) return;
    await this.run(this.visit.raiseWorkOrder ? 'The visit is recorded and a work order raised.' : 'The visit is recorded.', async () => {
      await this.api.recordVisit(row.amcContractId, this.visit);
      this.visit = ContractsPage.blankVisit();
    });
  }

  private async refreshOpen(id: number): Promise<void> {
    try {
      const [contract, visits] = await Promise.all([this.api.contract(id), this.api.visits(id)]);
      this.open.set(contract);
      this.visits.set(visits);
    } catch (error) {
      this.fail(error);
    }
  }

  private async run(success: string, work: () => Promise<unknown>): Promise<void> {
    this.busy.set(true);
    try {
      await work();
      this.messages.set([{ tone: 'success', text: success }]);
      const row = this.open();
      if (row) await this.refreshOpen(row.amcContractId);
      await this.load();
    } catch (error) {
      this.fail(error);
    } finally {
      this.busy.set(false);
    }
  }

  private async start(): Promise<void> {
    await this.load();
    await Promise.allSettled([
      this.api.vendors().then((vendors) =>
        this.vendorOptions.set(vendors.map((v) => ({ value: v.contactId, label: `${v.displayName} (${v.contactCode})` })))),
      this.facility.assets({}).then((assets) =>
        this.assetOptions.set(assets.filter((a) => a.assetStatus !== 'Disposed').map((a) => ({ value: a.facilityAssetId, label: `${a.assetTag} ${a.name}` })))),
    ]);
  }

  private fail(error: unknown): void {
    const failure = readApiFailure(error);
    this.messages.set([{ tone: 'error', text: failure.text, detail: failure.detail }]);
  }

  private static today(): string {
    return new Date().toISOString().slice(0, 10);
  }

  private static blank(): SaveContract {
    const start = ContractsPage.today();
    const end = new Date(Date.parse(start) + 364 * 86_400_000).toISOString().slice(0, 10);
    return {
      contractNo: '', vendorContactId: 0, startDate: start, endDate: end, contractValue: 0, billingFrequency: 'Annual',
      visitsPerYear: 4, amcCoverage: 'NonComprehensive', renewalReminderDays: 30, reminderEmail: null, remarks: null, facilityAssetIds: [],
    };
  }

  private static blankVisit(): RecordVisit {
    return { visitDate: ContractsPage.today(), visitKind: 'Scheduled', facilityAssetId: null, remarks: null, raiseWorkOrder: false };
  }
}
