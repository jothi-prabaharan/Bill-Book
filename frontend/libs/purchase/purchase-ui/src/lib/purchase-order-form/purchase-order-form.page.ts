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
  addScannedItem,
  ScanComponent,
  BranchDateInputComponent,
  DocumentLine,
  DocumentLineContext,
  DocumentLineGridComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  TaxGroupOption,
  TextInputComponent,
  TextareaComponent,
  CheckboxComponent,
  recalculate,
  totalsOf,
  UiMessage,
  ApprovalPanelComponent,
  ProjectOption,
  ProjectOptionsService,
  CustomFieldValues,
  DocumentCustomFieldsComponent,
  compactValues,
  SelectComponent,
  AddressSummaryComponent,
  BbSelectOption,
  GST_STATE_OPTIONS,
  TRANSACTION_CURRENCY_OPTIONS,
  resolveOrgStateCode,
  resolveOrgCurrency,
} from '@bill-book/ui-components';
import { AuthService } from '@bill-book/auth';
import { PrintTemplateChoice, PrintTemplateService } from '@bill-book/master-core';
import {
  ItemOption,
  PurchaseLookupService,
  PurchaseOrderService,
  PurchaseOrderView,
  SavePurchaseOrderLineRequest,
  SavePurchaseOrderRequest,
  OrgAddressOption,
  formatOrgAddress,
  formatOrgAddressOptionLabel,
} from '@bill-book/purchase-core';
import { matchScannedItem } from '@bill-book/scan-core';

/**
 * Quantity carries six decimals in the grid, prices and amounts whole paise.
 * The API speaks decimals, so the two scales are crossed exactly here and
 * nowhere else.
 */
const QTY_SCALE = 1_000_000;
const PAISE = 100;
const RATE_SCALE = 10_000;

/** Which lookup, if any, is currently open. */
type Picker = 'none' | 'vendor' | 'item';

/**
 * Raise or amend a purchase order.
 *
 * **There is no reservation control on this form, and that is the point.** A
 * sales order form has one because confirming it holds stock back; ordering
 * from a vendor holds nothing, because the goods are not here. What this page
 * adds beside the shared line grid is the expected delivery date — see
 * `docs/Purchase.md` §9a.
 *
 * The pickers are the host's job by design: `bb-document-line-grid` and
 * `bb-lookup-dialog` both fetch nothing, so this page owns every HTTP call and
 * they stay testable without a server.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-purchase-order-form',
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
    CommonModule,
    FormsModule,
    RouterModule,
    DocumentLineGridComponent,
    LookupDialogComponent,
    BranchDateInputComponent,
    TextInputComponent,
    SelectComponent,
    AddressSummaryComponent,
    TextareaComponent,
    CheckboxComponent,
    MessageBoxComponent,
  ],
  templateUrl: './purchase-order-form.page.html',
  styleUrl: './purchase-order-form.page.scss',
})
export class PurchaseOrderFormPage {
  /** The document's custom field values (TK-112). */
  protected readonly customValues = signal<CustomFieldValues>({});

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

  private readonly orders = inject(PurchaseOrderService);
  private readonly lookups = inject(PurchaseLookupService);
  private readonly printTemplatesService = inject(PrintTemplateService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly purchaseOrderId = signal<number | null>(null);
  protected readonly documentNo = signal<string | null>(null);
  protected readonly status = signal('Draft');
  protected readonly fulfilmentStatus = signal('Open');
  protected readonly receiptCount = signal(0);

  /** Available print templates for this transaction type */
  protected readonly printTemplates = signal<PrintTemplateChoice[]>([]);
  protected readonly printTemplateId = signal<number | null>(null);

  protected readonly documentDate = signal(this.today());
  protected readonly expectedDate = signal<string>('');
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactLabel = signal<string>('');
  protected readonly contactGstin = signal('');
  protected readonly placeOfSupplyStateCode = signal(this.getOrgStateCode());
  protected readonly currencyCode = signal(this.getOrgCurrency());
  protected readonly exchangeRate = signal(1);
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

  protected readonly notes = signal('');
  protected readonly termsAndConditions = signal('');
  protected readonly isInterState = signal(false);

  protected copyBillingToShipping(event?: Event | boolean): void {
    const checked = typeof event === 'boolean' ? event : ((event?.target as HTMLInputElement)?.checked ?? true);
    this.onSameAsBillingChange(checked);
  }

  protected readonly lines = signal<readonly DocumentLine[]>([]);
  protected readonly totals = computed(() => totalsOf(this.lines()));
  protected readonly taxGroups = signal<readonly TaxGroupOption[]>([]);

  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly messages = computed<UiMessage[]>(() => {
    const list: UiMessage[] = [];
    if (this.error()) list.push({ tone: 'error', text: this.error()! });
    if (this.notice()) list.push({ tone: 'success', text: this.notice()! });
    return list;
  });

  // ---- Lookups ----------------------------------------------------------

  protected readonly picker = signal<Picker>('none');
  protected readonly pickerRows = signal<readonly LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  /** Which line the item picker was opened for. */
  private pickingLine = -1;

  /** The picker was opened by a scan that matched no single item (TK-162). */
  private pickingFromScan = false;

  /** The search the item picker opens on: the scanned code, or empty. */
  protected readonly pickerTerm = signal('');

  /** Guards against an earlier search landing after a later one. */
  private searchToken = 0;

  // ---- Branch settings --------------------------------------------------

  private readonly allowFreeTextLines = signal(true);
  private readonly discountBeforeTax = signal(true);
  private readonly discountLevel = signal<'Line' | 'Header' | 'Both'>('Line');

  protected readonly readonlyDoc = computed(
    () => this.status() === 'Posted' || this.status() === 'Void',
  );

  protected readonly isNew = computed(() => this.purchaseOrderId() === null);
  protected readonly canDraft = computed(() => this.isNew() || this.status() === 'Draft');

  protected readonly canApprove = computed(() => this.status() === 'Draft');

  protected readonly canConfirm = computed(
    () => this.status() === 'Draft' || this.status() === 'ReadyToPost',
  );

  protected readonly gridContext = computed<DocumentLineContext>(() => ({
    isInterState: this.isInterState(),
    allowFreeTextLines: this.allowFreeTextLines(),
    discountBeforeTax: this.discountBeforeTax(),
    discountLevel: this.discountLevel(),
    readonly: this.readonlyDoc(),
    currencyDecimals: 2,
  }));

  /** The branch's open projects, for tagging a line (TK-105). Empty for a user who cannot read them. */
  protected readonly projectOptions = signal<readonly ProjectOption[]>([]);
  private readonly projectSource = inject(ProjectOptionsService);

  constructor() {
    void this.projectSource.load().then((options) => this.projectOptions.set(options));
    void this.loadReferenceData();
    void this.loadPrintTemplates();

    const id = this.route.snapshot.paramMap.get('id');
    if (id !== null && id !== 'new') {
      void this.load(Number(id));
    } else {
      void this.loadOrganizationAddresses(false);
    }
  }

  private async loadPrintTemplates(): Promise<void> {
    try {
      const choices = await this.printTemplatesService.choices('POR');
      this.printTemplates.set(choices);
      if (this.isNew()) {
        const defaultTmpl = choices.find((t) => t.isDefault) ?? choices[0];
        if (defaultTmpl && !this.printTemplateId()) {
          this.printTemplateId.set(defaultTmpl.printTemplateId);
        }
      }
    } catch {
      // ignore
    }
  }

  /**
   * The branch's settings and the rates a line may use.
   *
   * Failures here are not fatal: the settings fall back to the shipped defaults
   * and the rate list stays empty, which leaves the grid usable and the tax
   * column blank rather than turning the whole screen into an error. The server
   * recomputes every figure on save regardless.
   */
  private async loadReferenceData(): Promise<void> {
    try {
      const settings = await this.lookups.settings();
      this.allowFreeTextLines.set(settings.allowFreeTextLines);
      this.discountBeforeTax.set(settings.discountBeforeTax);
      this.discountLevel.set(settings.discountLevel);
    } catch {
      // Defaults already set on the signals.
    }

    try {
      const rates = await this.lookups.purchaseTaxRates();
      this.taxGroups.set(
        rates.map((rate) => ({
          taxGroupId: rate.taxGroupId,
          taxMasterId: rate.taxMasterId,
          label: `${rate.taxName} (${rate.totalRate}%)`,
          cgstRate: rate.cgstRate,
          sgstRate: rate.sgstRate,
          igstRate: rate.igstRate,
          cessRate: rate.cessRate,
        })),
      );
    } catch {
      this.taxGroups.set([]);
    }
  }

  protected async load(id: number): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const view: PurchaseOrderView = await this.orders.get(id);

      this.purchaseOrderId.set(view.purchaseOrderId);
      this.documentNo.set(view.documentNo);
      this.status.set(view.status);
      this.fulfilmentStatus.set(view.fulfilmentStatus);
      this.receiptCount.set(view.receiptCount);
      this.documentDate.set(view.documentDate);
      this.expectedDate.set(view.expectedDate ?? '');
      this.contactId.set(view.contactId);
      this.contactLabel.set(
        view.contactName
          ? `${view.contactCode ?? ''} ${view.contactName}`.trim()
          : String(view.contactId),
      );
      this.contactGstin.set(view.contactGstin ?? '');
      this.printTemplateId.set(view.printTemplateId ?? null);
      this.currencyCode.set(view.currencyCode ?? 'INR');
      this.exchangeRate.set(view.exchangeRate ?? 1);
      this.billingAddress.set(view.billingAddress ?? '');
      this.shippingAddress.set(view.shippingAddress ?? '');
      void this.loadOrganizationAddresses(true);
      this.notes.set(view.notes ?? '');
      this.customValues.set(view.customFields ?? {});
      this.termsAndConditions.set(view.termsAndConditions ?? '');
      this.isInterState.set(view.isInterState);

      this.lines.set(
        view.lines.map((line) => ({
          detailId: line.purchaseOrderDetailId,
          lineNumber: line.lineNumber,
          itemId: line.itemId ?? null,
          itemLabel: line.itemLabel ?? null,
          hsnSacCode: line.hsnSacCode ?? null,
          description: line.description ?? null,
          warehouseId: line.warehouseId ?? null,
          quantity: Math.round(line.quantity * QTY_SCALE),
          uomId: line.uomId ?? null,
          uomLabel: null,
          conversionFactor: Math.round(line.conversionFactor * QTY_SCALE),
          baseQuantity: Math.round(line.baseQuantity * QTY_SCALE),
          unitPrice: Math.round(line.unitPrice * PAISE),
          isPriceInclusive: line.isPriceInclusive,
          discountPercent: line.discountPercent ?? null,
          discountAmount: Math.round(line.discountAmount * PAISE),
          grossAmount: Math.round(line.grossAmount * PAISE),
          taxableAmount: Math.round(line.taxableAmount * PAISE),
          taxTreatment: line.taxTreatment as DocumentLine['taxTreatment'],
          taxMasterId: line.taxMasterId ?? null,
          taxGroupId: line.taxGroupId ?? null,
          taxAmount: Math.round(line.taxAmount * PAISE),
          taxes: line.taxes.map((tax) => ({
            component: tax.taxComponent as 'Cgst' | 'Sgst' | 'Igst' | 'Cess',
            subAccountId: tax.subAccountId,
            rate: Math.round(tax.rate * RATE_SCALE),
            taxableAmount: Math.round(tax.taxableAmount * PAISE),
            amount: Math.round(tax.amount * PAISE),
          })),
          lineType: line.lineType as DocumentLine['lineType'],
          accountId: line.accountId ?? null,
          fixedAssetCategoryId: line.fixedAssetCategoryId ?? null,
          projectId: line.projectId ?? null,
          lineTotal: Math.round(line.lineTotal * PAISE),
          itemBatchId: line.itemBatchId ?? null,
          lineNotes: line.lineNotes ?? null,
        })),
      );
    } catch {
      this.error.set('This purchase order could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected onLinesChange(lines: readonly DocumentLine[]): void {
    this.lines.set(lines);
  }

  // ---- Vendor picker ----------------------------------------------------

  protected openVendorPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('vendor');
    void this.runSearch('');
  }

  // ---- Item picker ------------------------------------------------------

  /** The grid asks; this page answers. The grid does not know how to fetch. */
  /** A scanned code (TK-162): its item goes on the lines, or the item picker opens on it. */
  protected async onScan(code: string): Promise<void> {
    if (this.readonlyDoc()) {
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

    if (match) {
      this.addScanned({ id: match.itemId, code: match.itemCode, name: match.itemName, meta: match.inventoryUomCode });
      return;
    }

    this.pickingFromScan = true;
    this.pickingLine = -1;
    this.pickerTerm.set(wanted);
    this.picker.set('item');
    void this.runSearch(wanted);
  }

  /** One more of a scanned item on its line, or a new line of one. */
  private addScanned(row: LookupRow): void {
    this.lines.set(addScannedItem(this.lines(), row, this.gridContext()));
  }

  protected onPickItem(lineIndex: number): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.pickingLine = lineIndex;
    this.pickingFromScan = false;
    this.pickerTerm.set('');
    this.picker.set('item');
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
          : (await this.lookups.items(term)).map((item) => ({
              id: item.itemId,
              code: item.itemCode,
              name: item.itemName,
              meta: item.inventoryUomCode,
            }));

      // A slower earlier search must not overwrite a newer one's results.
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
    } else {
      if (this.pickingFromScan) {
        this.addScanned(row);
      } else {
        this.applyItem(this.pickingLine, row);
      }
    }

    this.closePicker();
  }

  private applyItem(lineIndex: number, row: LookupRow): void {
    this.lines.set(
      this.lines().map((line, at) =>
        at === lineIndex
          ? recalculate(
              { ...line, itemId: row.id, itemLabel: `${row.code} - ${row.name}` },
              this.gridContext(),
            )
          : line,
      ),
    );
  }

  protected closePicker(): void {
    this.picker.set('none');
    this.pickerRows.set([]);
    this.pickingLine = -1;
    this.pickingFromScan = false;
    this.pickerTerm.set('');
  }

  protected pickerTitle(): string {
    return this.picker() === 'vendor' ? 'Choose a vendor' : 'Choose an item';
  }

  protected pickerPlaceholder(): string {
    return this.picker() === 'vendor'
      ? 'Search vendors by code or name'
      : 'Search items by code or name';
  }

  protected pickerEmptyText(): string {
    return this.picker() === 'vendor'
      ? 'No vendors match. A contact has to be marked as a vendor to appear here.'
      : 'No items match.';
  }

  // ---- Actions ----------------------------------------------------------

  protected async save(): Promise<void> {
    const contactId = this.contactId();
    if (contactId === null) {
      this.error.set('Choose the vendor before saving.');
      return;
    }

    const priced = this.lines().filter((line) => line.quantity > 0);
    if (priced.length === 0) {
      this.error.set('A purchase order needs at least one line with a quantity on it.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      const request = this.toRequest(contactId);
      const id = this.purchaseOrderId();

      if (id === null) {
        const created = await this.orders.create(request);
        await this.router.navigate(['../', created.purchaseOrderId], {
          relativeTo: this.route,
        });
      } else {
        await this.orders.update(id, request);
        await this.load(id);
        this.notice.set('Saved.');
      }
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected approve(): Promise<void> {
    return this.act((id) => this.orders.approve(id), 'Approved and ready to issue.');
  }

  protected confirm(): Promise<void> {
    return this.act((id) => this.orders.confirm(id), 'Issued to the vendor.');
  }

  protected async voidOrder(): Promise<void> {
    const reason = this.voidReason().trim();
    if (!reason) {
      this.error.set('Say why this order is being voided.');
      return;
    }

    await this.act((id) => this.orders.void(id, { reason }), 'Order voided.');
    this.voidOpen.set(false);
  }

  protected readonly voidOpen = signal(false);
  protected readonly voidReason = signal('');

  private async act(
    call: (id: number) => Promise<void>,
    done: string,
  ): Promise<void> {
    const id = this.purchaseOrderId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      await call(id);
      await this.load(id);
      this.notice.set(done);
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  /**
   * Money and quantities go back to the server as decimals. The grid holds
   * paise and six-decimal quantities, so this is the one place the scales are
   * crossed — and totals are not sent at all, because the server computes them.
   */
  private toRequest(contactId: number): SavePurchaseOrderRequest {
    const lines: SavePurchaseOrderLineRequest[] = this.lines()
      .filter((line) => line.quantity > 0)
      .map((line) => ({
      itemId: line.itemId,
      description: line.description,
      hsnSacCode: line.hsnSacCode,
      warehouseId: line.warehouseId,
      quantity: line.quantity / QTY_SCALE,
      uomId: line.uomId,
      conversionFactor: line.conversionFactor / QTY_SCALE,
      unitPrice: line.unitPrice / PAISE,
      isPriceInclusive: line.isPriceInclusive,
      discountPercent: line.discountPercent,
      discountAmount: line.discountAmount / PAISE,
      taxTreatment: line.taxTreatment,
      taxGroupId: line.taxGroupId,
      lineType: line.lineType,
      accountId: line.accountId,
      fixedAssetCategoryId: line.fixedAssetCategoryId,
      projectId: line.projectId,
      itemBatchId: line.itemBatchId,
      lineNotes: line.lineNotes,
    }));

    return {
      documentDate: this.documentDate(),
      contactId,
      expectedDate: this.expectedDate() || null,
      contactGstin: this.contactGstin() || null,
      placeOfSupplyStateCode: this.placeOfSupplyStateCode() || null,
      billingAddress: this.billingAddress() || null,
      shippingAddress: this.shippingAddress() || null,
      printTemplateId: this.printTemplateId(),
      currencyCode: this.currencyCode() || 'INR',
      exchangeRate: this.exchangeRate() || 1,
      notes: this.notes() || null,
      customFields: compactValues(this.customValues()),
      termsAndConditions: this.termsAndConditions() || null,
      lines,
    };
  }

  /**
   * The server's own words when it sent any — every refusal it returns is
   * something the person at the screen can act on, so replacing them with a
   * generic message throws away the only useful part.
   */
  private messageFrom(err: unknown): string {
    const body = (err as { error?: { message?: string } } | null)?.error;
    return body?.message ?? 'The purchase order could not be saved.';
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}


