import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, inject, signal } from '@angular/core';

import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

import {
  CreatePageComponent,
  NumberFieldComponent,
  ExchangeRateFieldComponent,
  MoreDetailsComponent,
  FormFieldComponent,
  UiMessage,
  ScanComponent,
  BbSelectOption,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  BranchDateInputComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  QuantityInputComponent,
  CheckboxComponent,
  SelectComponent,
  AddressSummaryComponent,
  TextareaComponent,
  ApprovalPanelComponent,
  CustomFieldValues,
  DocumentCustomFieldsComponent,
  compactValues,
  GST_STATE_OPTIONS,
  TRANSACTION_CURRENCY_OPTIONS,
  resolveOrgStateCode,
  resolveOrgCurrency,
} from '@bill-book/ui-components';
import { AuthService } from '@bill-book/auth';

import {
  ItemOption,
  BillService,
  DEBIT_NOTE_REASONS,
  DebitNoteService,
  DebitNoteView,
  PurchaseLookupService,
  SaveDebitNoteLineRequest,
  SaveDebitNoteRequest,
  OrgAddressOption,
  formatOrgAddress,
  formatOrgAddressOptionLabel,
} from '@bill-book/purchase-core';
import { matchScannedItem } from '@bill-book/scan-core';
import { debitScanned } from './debit-note-scan';

type Picker = 'none' | 'vendor' | 'bill';

/**
 * One line of the note, tied to the bill line it reverses.
 *
 * **Not the shared line grid.** A debit note does not build lines — it picks
 * them off a bill and says how much of each goes back, at the price the bill
 * charged. The grid's job is composing a document from items and prices, and
 * offering it here would invite editing figures that have to match the bill.
 */
interface ReturnLine {
  billDetailId: number;
  itemId: number | null;
  label: string;
  /** What the bill charged, per unit. Fixed. */
  unitPrice: number;
  /** How much of the bill line is still returnable. */
  available: number;
  /** How much this note sends back. Zero leaves the line off. */
  quantity: number;
  taxGroupId: number | null;
  taxTreatment: string;
  lineType: string;
  uomId: number | null;
  conversionFactor: number;
  warehouseId: number | null;
  itemBatchId: number | null;
}

/**
 * Raise a debit note.
 *
 * **The reason decides whether stock moves**, and the form says which is which
 * rather than leaving it to be discovered after posting: only a purchase return
 * sends goods back. A price correction or a post-purchase discount reduces what
 * is owed while the goods stay on the shelf.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-debit-note-form',
  standalone: true,
  imports: [
    CreatePageComponent,
    NumberFieldComponent,
    ExchangeRateFieldComponent,
    MoreDetailsComponent,
    FormFieldComponent,
    ScanComponent,
    DocumentCustomFieldsComponent,
    ApprovalPanelComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
    CommonModule,
    FormsModule,
    RouterModule,
    LookupDialogComponent,
    BranchDateInputComponent,
    QuantityInputComponent,
    CheckboxComponent,
    SelectComponent,
    AddressSummaryComponent,
    TextareaComponent,
    MessageBoxComponent,
  ],
  templateUrl: './debit-note-form.page.html',
  styleUrl: './debit-note-form.page.scss',
})
export class DebitNoteFormPage {
  /** The document's custom field values (TK-112). */
  protected readonly customValues = signal<CustomFieldValues>({});


  /** A getter for the same reason: `reasons` is declared below this. */
  protected get reasonOptions(): BbSelectOption<string>[] {
    return this.reasons.map((reason) => ({ value: reason.code, label: reason.label }));
  }
  private readonly auth = inject(AuthService);
  protected readonly currencyOptions = TRANSACTION_CURRENCY_OPTIONS;
  protected readonly stateOptions = GST_STATE_OPTIONS;

  private getOrgStateCode(): string {
    const activeOrg = this.auth.organizations().find((o) => o.orgId === this.auth.activeOrgId());
    return resolveOrgStateCode(activeOrg?.gstin);
  }

  private getOrgCurrency(): string {
    const activeOrg = this.auth.organizations().find((o) => o.orgId === this.auth.activeOrgId());
    return resolveOrgCurrency(activeOrg?.baseCurrency);
  }

  private readonly notes = inject(DebitNoteService);
  private readonly bills = inject(BillService);
  private readonly lookups = inject(PurchaseLookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly reasons = DEBIT_NOTE_REASONS;

  protected readonly debitNoteId = signal<number | null>(null);
  protected readonly documentNo = signal<string | null>(null);
  protected readonly status = signal('Draft');

  protected readonly documentDate = signal(this.today());
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactLabel = signal('');
  protected readonly contactGstin = signal('');
  protected readonly billId = signal<number | null>(null);
  protected readonly billLabel = signal('');
  protected readonly reasonCode = signal('PurchaseReturn');
  protected readonly placeOfSupplyStateCode = signal(this.getOrgStateCode());
  protected readonly billingAddress = signal('');
  protected readonly shippingAddress = signal('');
  protected readonly organizationAddresses = signal<OrgAddressOption[]>([]);
  protected readonly selectedBillingOrgId = signal<string | null>(null);
  protected readonly selectedShippingOrgId = signal<string | null>(null);
  protected readonly sameAsBilling = signal(true);

  protected readonly billingAddressOptions = computed<BbSelectOption<string>[]>(() =>
    this.organizationAddresses().map((a) => ({
      value: a.orgId,
      label: a.label,
    }))
  );

  protected readonly shippingAddressOptions = computed<BbSelectOption<string>[]>(() =>
    this.organizationAddresses().map((a) => ({
      value: a.orgId,
      label: a.label,
    }))
  );

  protected readonly billingAddressPlaceholder = computed(() =>
    this.billingAddressOptions().length > 0
      ? 'Choose branch address…'
      : 'No branch address available'
  );

  protected readonly shippingAddressPlaceholder = computed(() =>
    this.shippingAddressOptions().length > 0
      ? 'Choose branch address…'
      : 'No branch address available'
  );

  private async loadOrganizationAddresses(preserveExisting = false): Promise<void> {
    let orgs = this.auth.organizations();
    if (!orgs || orgs.length === 0) {
      try {
        orgs = await this.auth.accessibleOrganizations();
      } catch {
        orgs = [];
      }
    }
    const currentOrgId = this.auth.activeOrgId();
    // In purchase transactions, only the active branch's address is listed
    const branchOrgs = currentOrgId
      ? (orgs ?? []).filter((o) => o.orgId === currentOrgId)
      : (orgs && orgs.length > 0 ? [orgs[0]] : []);

    const mapped: OrgAddressOption[] = branchOrgs.map((o) => ({
      orgId: o.orgId,
      orgName: o.orgName,
      addressLine1: o.addressLine1,
      addressLine2: o.addressLine2,
      city: o.city,
      stateId: o.stateId,
      postalCode: o.postalCode,
      gstin: o.gstin,
      formattedAddress: formatOrgAddress(o),
      label: formatOrgAddressOptionLabel(o),
    }));
    this.organizationAddresses.set(mapped);

    if (mapped.length === 0) return;

    const currentOrg = mapped[0];

    if (!preserveExisting || !this.billingAddress()) {
      if (currentOrg) {
        this.selectedBillingOrgId.set(currentOrg.orgId);
        this.billingAddress.set(currentOrg.formattedAddress);
      }
    } else {
      const matched = mapped.find((o) => o.formattedAddress === this.billingAddress() || o.orgId === currentOrgId);
      if (matched) {
        this.selectedBillingOrgId.set(matched.orgId);
      }
    }

    if (!preserveExisting || !this.shippingAddress()) {
      if (this.sameAsBilling()) {
        this.shippingAddress.set(this.billingAddress());
        this.selectedShippingOrgId.set(this.selectedBillingOrgId());
      } else if (currentOrg) {
        this.selectedShippingOrgId.set(currentOrg.orgId);
        this.shippingAddress.set(currentOrg.formattedAddress);
      }
    } else {
      const matched = mapped.find((o) => o.formattedAddress === this.shippingAddress());
      if (matched) {
        this.selectedShippingOrgId.set(matched.orgId);
      }
    }
  }

  protected onBillingAddressChange(orgId: string | null): void {
    this.selectedBillingOrgId.set(orgId);
    if (!orgId) return;
    const org = this.organizationAddresses().find((o) => o.orgId === orgId);
    if (org) {
      this.billingAddress.set(org.formattedAddress);
      if (this.sameAsBilling()) {
        this.shippingAddress.set(org.formattedAddress);
        this.selectedShippingOrgId.set(orgId);
      }
    }
  }

  protected onShippingAddressChange(orgId: string | null): void {
    this.selectedShippingOrgId.set(orgId);
    if (!orgId) return;
    const org = this.organizationAddresses().find((o) => o.orgId === orgId);
    if (org) {
      this.shippingAddress.set(org.formattedAddress);
    }
  }

  protected onSameAsBillingChange(checked: boolean): void {
    this.sameAsBilling.set(checked);
    if (checked) {
      this.shippingAddress.set(this.billingAddress());
      this.selectedShippingOrgId.set(this.selectedBillingOrgId());
    }
  }

  protected readonly selectedBillingAddress = computed(() => {
    const orgId = this.selectedBillingOrgId();
    if (orgId) {
      const match = this.organizationAddresses().find((o) => o.orgId === orgId);
      if (match) {
        return {
          title: match.orgName,
          badge: match.orgId === this.auth.activeOrgId() ? 'Current Branch' : 'Branch',
          lines: match.formattedAddress,
          email: null,
          phone: null,
          gstin: match.gstin || null,
        };
      }
    }
    const raw = this.billingAddress();
    if (raw && raw.trim()) {
      return {
        title: 'Billing premises',
        badge: null,
        lines: raw,
        email: null,
        phone: null,
        gstin: null,
      };
    }
    return null;
  });

  protected readonly selectedShippingAddress = computed(() => {
    if (this.sameAsBilling()) {
      const billing = this.selectedBillingAddress();
      if (!billing) return null;
      return {
        ...billing,
        badge: 'Same as Billing',
      };
    }
    const orgId = this.selectedShippingOrgId();
    if (orgId) {
      const match = this.organizationAddresses().find((o) => o.orgId === orgId);
      if (match) {
        return {
          title: match.orgName,
          badge: match.orgId === this.auth.activeOrgId() ? 'Current Branch' : 'Branch',
          lines: match.formattedAddress,
          email: null,
          phone: null,
          gstin: match.gstin || null,
        };
      }
    }
    const raw = this.shippingAddress();
    if (raw && raw.trim()) {
      return {
        title: 'Delivery destination',
        badge: null,
        lines: raw,
        email: null,
        phone: null,
        gstin: null,
      };
    }
    return null;
  });

  protected readonly currencyCode = signal(this.getOrgCurrency());
  protected readonly exchangeRate = signal(1);
  protected readonly isInterState = signal(false);
  protected readonly voidOpen = signal(false);
  protected readonly voidReason = signal('');
  protected readonly notes_ = signal('');

  protected readonly lines = signal<readonly ReturnLine[]>([]);

  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly canDraft = computed(() => this.isNew() || this.status() === 'Draft');
  protected readonly canPost = computed(() => !this.isNew() && !this.readonlyDoc());

  protected readonly messages = computed<UiMessage[]>(() => {
    const list: UiMessage[] = [];
    const err = this.error();
    const not = this.notice();
    if (err) list.push({ tone: 'error', text: err });
    if (not) list.push({ tone: 'success', text: not });
    return list;
  });

  protected copyBillingToShipping(): void {
    this.onSameAsBillingChange(true);
  }

  protected zeroAll(): void {
    this.lines.set(
      this.lines().map((line) => ({ ...line, quantity: 0 })),
    );
  }

  protected readonly picker = signal<Picker>('none');
  protected readonly pickerRows = signal<readonly LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  private searchToken = 0;

  protected readonly readonlyDoc = computed(
    () => this.status() === 'Posted' || this.status() === 'Void',
  );

  protected readonly isNew = computed(() => this.debitNoteId() === null);

  /** Whether the chosen reason sends goods back. Drives the whole warning. */
  protected readonly movesStock = computed(
    () =>
      this.reasons.find((r) => r.code === this.reasonCode())?.movesStock ?? false,
  );

  protected readonly reasonHint = computed(
    () => this.reasons.find((r) => r.code === this.reasonCode())?.hint ?? '',
  );

  /** Only lines with something on them are sent. */
  protected readonly activeLines = computed(() =>
    this.lines().filter((line) => line.quantity > 0),
  );

  protected readonly total = computed(() =>
    this.activeLines().reduce(
      (sum, line) => sum + line.quantity * line.unitPrice,
      0,
    ),
  );

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id !== null && id !== 'new') {
      void this.load(Number(id));
    } else {
      void this.loadOrganizationAddresses(false);
    }
  }

  protected async load(id: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const view: DebitNoteView = await this.notes.get(id);

      this.debitNoteId.set(view.debitNoteId);
      this.documentNo.set(view.documentNo);
      this.status.set(view.status);
      this.documentDate.set(view.documentDate);
      this.contactId.set(view.contactId);
      this.contactLabel.set(
        view.contactName
          ? `${view.contactCode ?? ''} ${view.contactName}`.trim()
          : String(view.contactId),
      );
      this.contactGstin.set(view.contactGstin ?? '');
      void this.loadOrganizationAddresses(true);
      this.billId.set(view.billId);
      this.billLabel.set(view.billNo ?? String(view.billId));
      this.reasonCode.set(view.reasonCode);
      this.placeOfSupplyStateCode.set(
        view.placeOfSupplyStateId ? String(view.placeOfSupplyStateId).padStart(2, '0') : '',
      );
      this.currencyCode.set(view.currencyCode ?? 'INR');
      this.exchangeRate.set(view.exchangeRate ?? 1);
      this.isInterState.set(view.isInterState);
      this.notes_.set(view.notes ?? '');
      this.voidReason.set(view.voidReason ?? '');
      this.customValues.set(view.customFields ?? {});

      this.lines.set(
        view.lines.map((line) => ({
          billDetailId: line.billDetailId,
          itemId: line.itemId ?? null,
          label: line.itemLabel ?? line.description ?? `Line ${line.lineNumber}`,
          unitPrice: line.unitPrice,
          available: line.quantity,
          quantity: line.quantity,
          taxGroupId: line.taxGroupId ?? null,
          taxTreatment: line.taxTreatment,
          lineType: line.lineType,
          uomId: line.uomId ?? null,
          conversionFactor: line.conversionFactor,
          warehouseId: line.warehouseId ?? null,
          itemBatchId: line.itemBatchId ?? null,
        })),
      );
    } catch {
      this.error.set('This debit note could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  // ---- Pickers ----------------------------------------------------------

  protected openVendorPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('vendor');
    void this.runSearch('');
  }

  protected openBillPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('bill');
    void this.runSearch('');
  }

  protected onPickerSearch(term: string): void {
    void this.runSearch(term);
  }

  private async runSearch(term: string): Promise<void> {
    const token = ++this.searchToken;
    this.pickerLoading.set(true);

    try {
      const rows: LookupRow[] =
        this.picker() === 'vendor'
          ? (await this.lookups.vendors(term)).map((vendor) => ({
              id: vendor.contactId,
              code: vendor.contactCode,
              name: vendor.displayName,
              meta: vendor.gstin,
            }))
          : (await this.lookups.creditableBills(term, this.contactId())).map(
              (bill) => ({
                id: bill.billId,
                code: bill.documentNo,
                name: bill.contactName ?? '',
                meta: `their ${bill.vendorBillNo} · ${bill.documentDate}`,
              }),
            );

      if (token === this.searchToken) {
        this.pickerRows.set(rows);
      }
    } catch {
      if (token === this.searchToken) {
        this.pickerRows.set([]);
      }
    } finally {
      if (token === this.searchToken) {
        this.pickerLoading.set(false);
      }
    }
  }

  protected onPickerChoose(row: LookupRow): void {
    if (this.picker() === 'vendor') {
      this.contactId.set(row.id);
      this.contactLabel.set(`${row.code} ${row.name}`.trim());

      this.contactGstin.set(row.meta ?? '');

      // The bill belonged to the previous vendor.
      this.billId.set(null);
      this.billLabel.set('');
      this.lines.set([]);
    } else {
      this.billId.set(row.id);
      this.billLabel.set(row.code);
      void this.pullBillLines(row.id);
    }

    this.closePicker();
  }

  /**
   * Copies the bill's lines, each showing how much is still returnable.
   *
   * A line already fully returned is left out: offering it would only produce a
   * refusal, and the server counts the running returned quantity precisely so
   * the same goods cannot be credited twice.
   */
  private async pullBillLines(billId: number): Promise<void> {
    try {
      const bill = await this.bills.get(billId);

      const returnable = bill.lines
        .map((line) => ({
          billDetailId: line.billDetailId,
          itemId: line.itemId ?? null,
          label: line.itemLabel ?? line.description ?? `Line ${line.lineNumber}`,
          unitPrice: line.unitPrice,
          available: line.quantity - line.returnedQuantity,
          quantity: 0,
          taxGroupId: line.taxGroupId ?? null,
          taxTreatment: line.taxTreatment,
          lineType: line.lineType,
          uomId: line.uomId ?? null,
          conversionFactor: line.conversionFactor,
          warehouseId: line.warehouseId ?? null,
          itemBatchId: line.itemBatchId ?? null,
        }))
        .filter((line) => line.available > 0);

      if (returnable.length === 0) {
        this.notice.set('Every line on that bill has already been returned in full.');
        this.lines.set([]);
        return;
      }

      this.lines.set(returnable);

      if (bill.contactGstin) {
        this.contactGstin.set(bill.contactGstin);
      }
      void this.loadOrganizationAddresses(true);
      if (bill.billingAddress && !this.billingAddress()) {
        this.billingAddress.set(bill.billingAddress);
      }
      if (bill.shippingAddress && !this.shippingAddress()) {
        this.shippingAddress.set(bill.shippingAddress);
      }
      if (bill.currencyCode) {
        this.currencyCode.set(bill.currencyCode);
      }
      if (bill.exchangeRate) {
        this.exchangeRate.set(bill.exchangeRate);
      }
      this.isInterState.set(bill.isInterState);
    } catch {
      this.error.set('That bill could not be read, so its lines were not copied.');
    }
  }

  protected onQuantityChange(index: number, value: number): void {
    this.lines.set(
      this.lines().map((line, at) =>
        at === index
          ? {
              ...line,
              quantity: Math.min(Math.max(value || 0, 0), line.available),
            }
          : line,
      ),
    );
  }

  /**
   * A scanned code (TK-162). A debit note's lines are its bill's, so a scan
   * sends one more unit back on the bill line for that item, up to what it
   * still has to return, and never adds a line.
   */
  protected async onScan(code: string): Promise<void> {
    if (this.readonlyDoc() || this.lines().length === 0) {
      return;
    }
    const wanted = code.trim();
    if (!wanted) {
      return;
    }

    let match: ItemOption | null = null;
    try {
      match = matchScannedItem(wanted, await this.lookups.items(wanted));
    } catch {
      match = null;
    }

    this.notice.set(null);
    if (!match) {
      this.notice.set(`No single item matches ${wanted}.`);
      return;
    }

    const { lines, outcome } = debitScanned(this.lines(), match.itemId);
    this.lines.set(lines);
    if (outcome === 'notOnBill') {
      this.notice.set(`${match.itemCode} is not on this bill.`);
    } else if (outcome === 'full') {
      this.notice.set(`All of ${match.itemCode} on this bill is already on the note.`);
    }
  }

  /** Returning everything still outstanding, which is the common case. */
  protected returnAll(): void {
    this.lines.set(
      this.lines().map((line) => ({ ...line, quantity: line.available })),
    );
  }

  protected closePicker(): void {
    this.picker.set('none');
    this.pickerRows.set([]);
  }

  protected pickerTitle(): string {
    return this.picker() === 'vendor' ? 'Choose a vendor' : 'Choose a bill';
  }

  protected pickerPlaceholder(): string {
    return this.picker() === 'vendor'
      ? 'Search vendors by code or name'
      : 'Search posted bills';
  }

  protected pickerEmptyText(): string {
    return this.picker() === 'vendor'
      ? 'No vendors match.'
      : 'No posted bills for this vendor. A draft bill owes nothing yet — correct '
        + 'the bill itself instead.';
  }

  // ---- Actions ----------------------------------------------------------

  protected async save(): Promise<void> {
    const contactId = this.contactId();
    const billId = this.billId();

    if (contactId === null || billId === null) {
      this.error.set('Choose the vendor and the bill this corrects.');
      return;
    }

    if (this.activeLines().length === 0) {
      this.error.set('Put a quantity on at least one line.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      const request = this.toRequest(contactId, billId);
      const id = this.debitNoteId();

      if (id === null) {
        const created = await this.notes.create(request);
        await this.router.navigate(['../', created.debitNoteId], {
          relativeTo: this.route,
        });
      } else {
        await this.notes.update(id, request);
        await this.load(id);
        this.notice.set('Saved.');
      }
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async post(): Promise<void> {
    const id = this.debitNoteId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      await this.notes.post(id);
      await this.load(id);
      this.notice.set(
        this.movesStock()
          ? 'Posted. The goods have gone back and the vendor is owed less.'
          : 'Posted. The vendor is owed less; the goods stayed.',
      );
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async voidDebitNote(): Promise<void> {
    const id = this.debitNoteId();
    if (id === null || !this.voidReason().trim()) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      await this.notes.void(id, { reason: this.voidReason().trim() });
      await this.load(id);
      this.voidOpen.set(false);
      this.notice.set('Debit note voided.');
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  private toRequest(contactId: number, billId: number): SaveDebitNoteRequest {
    const lines: SaveDebitNoteLineRequest[] = this.activeLines().map((line) => ({
      billDetailId: line.billDetailId,
      itemId: line.itemId,
      quantity: line.quantity,
      uomId: line.uomId,
      conversionFactor: line.conversionFactor,
      unitPrice: line.unitPrice,
      isPriceInclusive: false,
      discountPercent: null,
      discountAmount: 0,
      taxTreatment: line.taxTreatment,
      taxGroupId: line.taxGroupId,
      lineType: line.lineType,
      warehouseId: line.warehouseId,
      itemBatchId: line.itemBatchId,
      lineNotes: null,
    }));

    return {
      documentDate: this.documentDate(),
      contactId,
      billId,
      reasonCode: this.reasonCode(),
      contactGstin: this.contactGstin() || null,
      placeOfSupplyStateCode: this.placeOfSupplyStateCode() || null,
      currencyCode: this.currencyCode() || 'INR',
      exchangeRate: this.exchangeRate() || 1,
      notes: this.notes_() || null,
      customFields: compactValues(this.customValues()),
      lines,
    };
  }

  private messageFrom(err: unknown): string {
    const body = (err as { error?: { message?: string } } | null)?.error;
    return body?.message ?? 'The debit note could not be saved.';
  }

  columns: ColumnDef[] = [
    { field: 'label', header: 'Item' },
    { field: 'available', header: 'Available' },
    { field: 'quantity', header: 'Quantity' },
    { field: 'unitPrice', header: 'Price' },
    { field: 'value', header: 'Value' }
  ];

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}




