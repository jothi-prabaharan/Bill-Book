import { ChangeDetectionStrategy } from '@angular/core';
import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  CreatePageComponent,
  NumberFieldComponent,
  ExchangeRateFieldComponent,
  MoreDetailsComponent,
  FormFieldComponent,
  addScannedItem,
  ScanComponent,
  ApprovalPanelComponent,
  BbSelectOption,
  CheckboxComponent,
  CustomFieldValues,
  BranchDateInputComponent,
  DocumentCustomFieldsComponent,
  DocumentLine,
  DocumentLineContext,
  DocumentLineGridComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  ProjectOption,
  ProjectOptionsService,
  SelectComponent,
  TaxGroupOption,
  TextInputComponent,
  TextareaComponent,
  compactValues,
  recalculate,
  totalsOf,
  UiMessage,
  EditHistoryComponent,
  LabelData,
  LabelKind,
  LabelPrintDialog,
  GST_STATE_OPTIONS,
  TRANSACTION_CURRENCY_OPTIONS,
  resolveOrgStateCode,
  resolveOrgCurrency,
} from '@bill-book/ui-components';
import { AuthService } from '@bill-book/auth';
import { goodsReceiptLabelKind, goodsReceiptLabels } from '../goods-receipt-form/goods-receipt-labels';
import {
  ItemOption,
  VendorOption,
  BillService,
  BillView,
  GoodsReceiptService,
  ItcEligibility,
  PurchaseLookupService,
  SaveBillLineRequest,
  SaveBillRequest,
  OrgAddressOption,
  formatOrgAddress,
  formatOrgAddressOptionLabel,
} from '@bill-book/purchase-core';
import { matchScannedItem } from '@bill-book/scan-core';

const QTY_SCALE = 1_000_000;
const PAISE = 100;
const RATE_SCALE = 10_000;

type Picker = 'none' | 'vendor' | 'item' | 'receipt';

/**
 * Enter a vendor bill.
 *
 * **Whether a receipt is attached changes what posting does**, and the form says
 * so rather than leaving it to be discovered: against a receipt the goods are
 * already on the shelf, so the bill clears the clearing account and moves
 * nothing; without one it does both jobs.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-bill-form',
  standalone: true,
  imports: [
    CreatePageComponent,
    NumberFieldComponent,
    ExchangeRateFieldComponent,
    MoreDetailsComponent,
    FormFieldComponent,
    ScanComponent,
    EditHistoryComponent,
    DocumentCustomFieldsComponent,
    ApprovalPanelComponent,
    CommonModule,
    FormsModule,
    RouterModule,
    DocumentLineGridComponent,
    LookupDialogComponent,
    BranchDateInputComponent,
    TextInputComponent,
    TextareaComponent,
    CheckboxComponent,
    SelectComponent,
    MessageBoxComponent,
    LabelPrintDialog,
  ],
  templateUrl: './bill-form.page.html',
  styleUrl: './bill-form.page.scss',
})
export class BillFormPage {
  /** Labels for the bill's item lines, one per unit billed (TK-159). */
  protected readonly labelsOpen = signal<{ labels: LabelData[]; kind: LabelKind } | null>(null);

  protected openLabels(): void {
    const labels = goodsReceiptLabels(
      this.lines().map((line) => ({
        itemId: line.itemId,
        itemLabel: line.itemLabel,
        description: line.description,
        acceptedQuantity: line.quantity / QTY_SCALE,
      })),
    );
    this.labelsOpen.set({ labels, kind: goodsReceiptLabelKind(labels) });
  }

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

  private readonly bills = inject(BillService);
  protected readonly formats = inject(FormatSettingsService);
  private readonly receipts = inject(GoodsReceiptService);
  private readonly lookups = inject(PurchaseLookupService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  protected readonly billId = signal<number | null>(null);
  protected readonly documentNo = signal<string | null>(null);
  protected readonly status = signal('Draft');
  protected readonly daysOverdue = signal(0);

  protected readonly documentDate = signal(this.today());
  protected readonly contactId = signal<number | null>(null);
  protected readonly contactLabel = signal('');
  protected readonly contactGstin = signal('');
  protected readonly goodsReceiptId = signal<number | null>(null);
  protected readonly goodsReceiptNo = signal('');
  protected readonly vendorBillNo = signal('');
  protected readonly vendorBillDate = signal(this.today());
  protected readonly dueDate = signal('');
  protected readonly placeOfSupplyStateCode = signal(this.getOrgStateCode());
  protected readonly currencyCode = signal(this.getOrgCurrency());
  protected readonly exchangeRate = signal(1);
  protected readonly billingAddress = signal('');
  protected readonly shippingAddress = signal('');
  protected readonly organizationAddresses = signal<OrgAddressOption[]>([]);
  protected readonly selectedBillingOrgId = signal<string | null>(null);
  protected readonly selectedShippingOrgId = signal<string | null>(null);
  protected readonly sameAsBilling = signal(true);
  protected readonly vendorDetailsOpen = signal(true);
  protected readonly shippingAddressOpen = signal(false);

  protected toggleVendorDetails(): void {
    this.vendorDetailsOpen.update((v) => !v);
  }

  protected toggleShippingAddress(): void {
    this.shippingAddressOpen.update((v) => !v);
  }

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
      ? 'Choose organization branch address…'
      : 'No organization address available'
  );

  protected readonly shippingAddressPlaceholder = computed(() =>
    this.shippingAddressOptions().length > 0
      ? 'Choose organization branch address…'
      : 'No organization address available'
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
    const mapped: OrgAddressOption[] = (orgs ?? []).map((o) => ({
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

    const currentOrgId = this.auth.activeOrgId();
    const currentOrg = mapped.find((o) => o.orgId === currentOrgId) ?? mapped[0];

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

  protected readonly notes = signal('');
  protected readonly termsAndConditions = signal('');
  protected readonly isInterState = signal(false);

  protected copyBillingToShipping(event?: Event | boolean): void {
    const checked = typeof event === 'boolean' ? event : ((event?.target as HTMLInputElement)?.checked ?? true);
    this.onSameAsBillingChange(checked);
  }

  /** The vendor charged no GST and this business pays it (TK-115, D-30). */
  protected readonly isReverseCharge = signal(false);

  /** TDS deducted at post (TK-117), shown once the bill is posted. */
  protected readonly tds = signal<{ section: string | null; base: number; amount: number; note: string | null } | null>(null);

  /** Input tax credit choices for a line (TK-115, D-31). */
  protected readonly eligibilityOptions: BbSelectOption<ItcEligibility>[] = [
    { value: 'Eligible', label: 'Claimable' },
    { value: 'Ineligible', label: 'Not claimable' },
    { value: 'Blocked', label: 'Blocked (section 17(5))' },
  ];

  protected readonly lines = signal<readonly DocumentLine[]>([]);
  protected readonly totals = computed(() => totalsOf(this.lines()));
  protected readonly taxGroups = signal<readonly TaxGroupOption[]>([]);

  /** Receipt line ids, positional with `lines`. Required on a bill against a receipt. */
  private receiptLineIds: (number | null)[] = [];

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

  /**
   * Expense accounts the last save took past the approved budget (TK-109).
   * A new bill's arrive through navigation state, because saving it moves to
   * the bill's own route and a new instance of this page.
   */
  protected readonly budgetWarnings = signal<string[]>(
    (this.router.getCurrentNavigation()?.extras.state?.['budgetWarnings'] as string[] | undefined) ?? [],
  );

  protected readonly picker = signal<Picker>('none');
  protected readonly pickerRows = signal<readonly LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  private pickingLine = -1;

  /** The picker was opened by a scan that matched no single item (TK-162). */
  private pickingFromScan = false;

  /** The search the item picker opens on: the scanned code, or empty. */
  protected readonly pickerTerm = signal('');
  private searchToken = 0;

  private readonly allowFreeTextLines = signal(true);
  private readonly discountBeforeTax = signal(true);
  private readonly discountLevel = signal<'Line' | 'Header' | 'Both'>('Line');

  protected readonly readonlyDoc = computed(
    () => this.status() === 'Posted' || this.status() === 'Void',
  );

  protected readonly isNew = computed(() => this.billId() === null);
  protected readonly canPost = computed(() => !this.isNew() && !this.readonlyDoc());
  protected readonly canDraft = computed(() => this.isNew() || this.status() === 'Draft');

  protected readonly againstReceipt = computed(() => this.goodsReceiptId() !== null);

  protected readonly gridContext = computed<DocumentLineContext>(() => ({
    isInterState: this.isInterState(),
    allowFreeTextLines: this.allowFreeTextLines(),
    discountBeforeTax: this.discountBeforeTax(),
    discountLevel: this.discountLevel(),
    readonly: this.readonlyDoc() || this.againstReceipt(),
    currencyDecimals: 2,
  }));

  /** The branch's open projects, for tagging a line (TK-105). Empty for a user who cannot read them. */
  protected readonly projectOptions = signal<readonly ProjectOption[]>([]);
  private readonly projectSource = inject(ProjectOptionsService);

  constructor() {
    void this.projectSource.load().then((options) => this.projectOptions.set(options));
    void this.loadReferenceData();

    const id = this.route.snapshot.paramMap.get('id');
    if (id !== null && id !== 'new') {
      void this.load(Number(id));
    } else {
      void this.loadOrganizationAddresses(false);
    }
  }

  private async loadReferenceData(): Promise<void> {
    try {
      const settings = await this.lookups.settings();
      this.allowFreeTextLines.set(settings.allowFreeTextLines);
      this.discountBeforeTax.set(settings.discountBeforeTax);
      this.discountLevel.set(settings.discountLevel);
    } catch {
      // Defaults already set.
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
      const view: BillView = await this.bills.get(id);

      this.billId.set(view.billId);
      this.documentNo.set(view.documentNo);
      this.status.set(view.status);
      this.daysOverdue.set(view.daysOverdue);
      this.documentDate.set(view.documentDate);
      this.contactId.set(view.contactId);
      this.contactLabel.set(
        view.contactName
          ? `${view.contactCode ?? ''} ${view.contactName}`.trim()
          : String(view.contactId),
      );
      this.contactGstin.set(view.contactGstin ?? '');
      this.goodsReceiptId.set(view.goodsReceiptId ?? null);
      this.goodsReceiptNo.set(view.goodsReceiptNo ?? '');
      this.vendorBillNo.set(view.vendorBillNo);
      this.vendorBillDate.set(view.vendorBillDate);
      this.dueDate.set(view.dueDate);
      this.currencyCode.set(view.currencyCode ?? 'INR');
      this.exchangeRate.set(view.exchangeRate ?? 1);
      this.billingAddress.set(view.billingAddress ?? '');
      this.shippingAddress.set(view.shippingAddress ?? '');
      void this.loadOrganizationAddresses(true);
      this.notes.set(view.notes ?? '');
      this.termsAndConditions.set(view.termsAndConditions ?? '');
      this.customValues.set(view.customFields ?? {});
      this.isInterState.set(view.isInterState);
      this.isReverseCharge.set(view.isReverseCharge ?? false);
      this.tds.set(
        view.tdsAmount || view.tdsSectionCode
          ? { section: view.tdsSectionCode ?? null, base: view.tdsBase ?? 0, amount: view.tdsAmount ?? 0, note: view.tdsNote ?? null }
          : null,
      );

      this.receiptLineIds = view.lines.map((l) => l.goodsReceiptDetailId ?? null);

      this.lines.set(
        view.lines.map((line) => ({
          detailId: line.billDetailId,
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
          isBillable: line.isBillable ?? false,
          markupPercent: line.markupPercent ?? null,
          lineTotal: Math.round(line.lineTotal * PAISE),
          itemBatchId: line.itemBatchId ?? null,
          lineNotes: line.lineNotes ?? null,
          itcEligibility: line.itcEligibility ?? 'Eligible',
        })),
      );
    } catch {
      this.error.set('This bill could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected onLinesChange(lines: readonly DocumentLine[]): void {
    this.lines.set(lines);
  }

  /** Sets one line's input tax credit eligibility (TK-115). */
  protected setEligibility(index: number, eligibility: ItcEligibility): void {
    this.lines.set(this.lines().map((line, i) => (i === index ? { ...line, itcEligibility: eligibility } : line)));
  }

  // ---- Vendor Combobox --------------------------------------------------

  protected readonly vendorSearchTerm = signal('');
  protected readonly vendorDropdownOpen = signal(false);
  protected readonly vendorOptions = signal<VendorOption[]>([]);
  protected readonly vendorSearchLoading = signal(false);

  protected async openVendorDropdown(): Promise<void> {
    if (this.readonlyDoc()) return;
    this.vendorDropdownOpen.set(true);
    if (this.vendorOptions().length === 0) {
      await this.filterVendors(this.vendorSearchTerm());
    }
  }

  protected closeVendorDropdown(): void {
    setTimeout(() => {
      this.vendorDropdownOpen.set(false);
    }, 200);
  }

  protected async onVendorSearch(term: string): Promise<void> {
    this.vendorSearchTerm.set(term);
    this.vendorDropdownOpen.set(true);
    await this.filterVendors(term);
  }

  private async filterVendors(term: string): Promise<void> {
    this.vendorSearchLoading.set(true);
    try {
      const list = await this.lookups.vendors(term);
      this.vendorOptions.set(list);
    } catch {
      this.vendorOptions.set([]);
    } finally {
      this.vendorSearchLoading.set(false);
    }
  }

  protected selectVendor(vendor: VendorOption): void {
    this.contactId.set(vendor.contactId);
    this.contactLabel.set(`${vendor.contactCode} ${vendor.displayName}`.trim());
    if (vendor.gstin) {
      this.contactGstin.set(vendor.gstin);
    }
    this.clearReceipt();
    this.vendorDropdownOpen.set(false);
    this.vendorSearchTerm.set('');
  }

  protected clearVendor(): void {
    if (this.readonlyDoc()) return;
    this.contactId.set(null);
    this.contactLabel.set('');
    this.contactGstin.set('');
    this.vendorSearchTerm.set('');
    this.clearReceipt();
  }

  // ---- Pickers ----------------------------------------------------------

  protected openVendorPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('vendor');
    void this.runSearch('');
  }

  protected openReceiptPicker(): void {
    if (this.readonlyDoc()) {
      return;
    }

    this.picker.set('receipt');
    void this.runSearch('');
  }

  /** Detaching goes back to a bill that brings the goods in itself. */
  protected clearReceipt(): void {
    this.goodsReceiptId.set(null);
    this.goodsReceiptNo.set('');
    this.receiptLineIds = this.lines().map(() => null);
  }

  /** A scanned code (TK-162): its item goes on the lines, or the item picker opens on it. */
  protected async onScan(code: string): Promise<void> {
    if (this.readonlyDoc() || this.againstReceipt()) {
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
    if (this.readonlyDoc() || this.againstReceipt()) {
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
      let rows: LookupRow[];

      if (this.picker() === 'vendor') {
        rows = (await this.lookups.vendors(term)).map((vendor) => ({
          id: vendor.contactId,
          code: vendor.contactCode,
          name: vendor.displayName,
          meta: vendor.gstin,
        }));
      } else if (this.picker() === 'receipt') {
        rows = (await this.lookups.billableReceipts(term, this.contactId())).map(
          (receipt) => ({
            id: receipt.goodsReceiptId,
            code: receipt.documentNo,
            name: receipt.contactName ?? '',
            meta: `${receipt.documentDate}${
              receipt.vendorDeliveryNoteNo ? ` · DN ${receipt.vendorDeliveryNoteNo}` : ''
            }`,
          }),
        );
      } else {
        rows = (await this.lookups.items(term)).map((item) => ({
          id: item.itemId,
          code: item.itemCode,
          name: item.itemName,
          meta: item.inventoryUomCode,
        }));
      }

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
    const which = this.picker();

    if (which === 'vendor') {
      this.contactId.set(row.id);
      this.contactLabel.set(`${row.code} ${row.name}`.trim());

      if (row.meta && !this.contactGstin()) {
        this.contactGstin.set(row.meta);
      }

      this.clearReceipt();
    } else if (which === 'receipt') {
      this.goodsReceiptId.set(row.id);
      this.goodsReceiptNo.set(row.code);
      void this.pullReceiptLines(row.id);
    } else {
      if (this.pickingFromScan) {
        this.addScanned(row);
      } else {
        this.applyItem(this.pickingLine, row);
      }
    }

    this.closePicker();
  }

  /**
   * Copies the receipt's accepted lines onto the bill.
   *
   * **Quantities and prices come from the receipt and the grid is then locked.**
   * A bill against a receipt has to agree with it to the paise, because the
   * server refuses a price that differs — purchase price variance is not
   * implemented — so letting the figures be edited here would only produce a
   * refusal at the end.
   */
  private async pullReceiptLines(receiptId: number): Promise<void> {
    try {
      const receipt = await this.receipts.get(receiptId);
      const accepted = receipt.lines.filter((line) => line.acceptedQuantity > 0);

      if (accepted.length === 0) {
        this.notice.set('Everything on that receipt was rejected, so there is nothing to bill.');
        this.clearReceipt();
        return;
      }

      this.receiptLineIds = accepted.map((line) => line.goodsReceiptDetailId);

      this.lines.set(
        accepted.map((line, index) =>
          recalculate(
            {
              detailId: 0,
              lineNumber: index + 1,
              itemId: line.itemId ?? null,
              itemLabel: line.itemLabel ?? null,
              hsnSacCode: line.hsnSacCode ?? null,
              description: line.description ?? null,
              warehouseId: line.warehouseId ?? null,
              quantity: Math.round(line.acceptedQuantity * QTY_SCALE),
              uomId: line.uomId ?? null,
              uomLabel: null,
              conversionFactor: Math.round(line.conversionFactor * QTY_SCALE),
              baseQuantity: Math.round(line.acceptedQuantity * QTY_SCALE),
              unitPrice: Math.round(line.unitPrice * PAISE),
              isPriceInclusive: line.isPriceInclusive,
              discountPercent: line.discountPercent ?? null,
              discountAmount: Math.round(line.discountAmount * PAISE),
              grossAmount: 0,
              taxableAmount: 0,
              taxTreatment: line.taxTreatment as DocumentLine['taxTreatment'],
              taxMasterId: line.taxMasterId ?? null,
              taxGroupId: line.taxGroupId ?? null,
              taxAmount: 0,
              taxes: line.taxes.map((tax) => ({
                component: tax.taxComponent as 'Cgst' | 'Sgst' | 'Igst' | 'Cess',
                subAccountId: tax.subAccountId,
                rate: Math.round(tax.rate * RATE_SCALE),
                taxableAmount: 0,
                amount: 0,
              })),
              lineType: line.lineType as DocumentLine['lineType'],
              accountId: line.accountId ?? null,
              fixedAssetCategoryId: line.fixedAssetCategoryId ?? null,
              projectId: line.projectId ?? null,
              lineTotal: 0,
              itemBatchId: line.itemBatchId ?? null,
              lineNotes: line.lineNotes ?? null,
            },
            this.gridContext(),
          ),
        ),
      );

      if (!this.contactGstin() && receipt.contactGstin) {
        this.contactGstin.set(receipt.contactGstin);
      }
    } catch {
      this.error.set('That receipt could not be read, so its lines were not copied.');
      this.clearReceipt();
    }
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
    switch (this.picker()) {
      case 'vendor':
        return 'Choose a vendor';
      case 'receipt':
        return 'Choose a goods receipt';
      default:
        return 'Choose an item';
    }
  }

  protected pickerPlaceholder(): string {
    switch (this.picker()) {
      case 'vendor':
        return 'Search vendors by code or name';
      case 'receipt':
        return 'Search posted receipts';
      default:
        return 'Search items by code or name';
    }
  }

  protected pickerEmptyText(): string {
    switch (this.picker()) {
      case 'vendor':
        return 'No vendors match.';
      case 'receipt':
        return 'No posted receipts for this vendor. Only a posted receipt has put '
          + 'anything into the clearing account for a bill to clear.';
      default:
        return 'No items match.';
    }
  }

  // ---- Actions ----------------------------------------------------------

  protected async save(): Promise<void> {
    const contactId = this.contactId();
    if (contactId === null) {
      this.error.set('Choose the vendor before saving.');
      return;
    }

    if (!this.vendorBillNo().trim()) {
      this.error.set("Enter the vendor's own bill number — the credit is claimed against it.");
      return;
    }

    if (!this.dueDate()) {
      this.error.set('Choose a due date for the bill.');
      return;
    }

    if (!this.contactGstin() && !this.placeOfSupplyStateCode()?.trim()) {
      this.error.set('Enter a place of supply (2-digit state code) for the bill.');
      return;
    }

    const priced = this.lines().filter(l => (l.quantity ?? 0) > 0);
    if (priced.length === 0) {
      this.error.set('Add at least one line item with a quantity greater than zero to the bill.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      const request = this.toRequest(contactId);
      const id = this.billId();

      if (id === null) {
        const created = await this.bills.create(request);
        await this.router.navigate(['../', created.billId], {
          relativeTo: this.route,
          state: { budgetWarnings: created.warnings ?? [] },
        });
      } else {
        const saved = await this.bills.update(id, request);
        await this.load(id);
        this.budgetWarnings.set(saved?.warnings ?? []);
        this.notice.set(saved?.message ?? 'Saved.');
      }
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected async post(): Promise<void> {
    const id = this.billId();
    if (id === null) {
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.notice.set(null);

    try {
      await this.bills.post(id);
      await this.load(id);
      this.notice.set(
        this.againstReceipt()
          ? 'Posted. Goods Received Not Invoiced is cleared and the vendor is owed.'
          : 'Posted. Stock is in and the vendor is owed.',
      );
    } catch (err) {
      this.error.set(this.messageFrom(err));
    } finally {
      this.saving.set(false);
    }
  }

  private toRequest(contactId: number): SaveBillRequest {
    const lines: SaveBillLineRequest[] = this.lines()
      .map((line, index) => ({ line, index }))
      .filter(({ line }) => (line.quantity ?? 0) > 0)
      .map(({ line, index }) => ({
      itemId: line.itemId,
      goodsReceiptDetailId: this.receiptLineIds[index] ?? null,
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
      isBillable: line.isBillable ?? false,
      markupPercent: line.isBillable ? (line.markupPercent ?? null) : null,
      itemBatchId: line.itemBatchId,
      lineNotes: line.lineNotes,
      itcEligibility: line.itcEligibility ?? 'Eligible',
    }));

    return {
      documentDate: this.documentDate(),
      contactId,
      goodsReceiptId: this.goodsReceiptId(),
      vendorBillNo: this.vendorBillNo().trim(),
      vendorBillDate: this.vendorBillDate(),
      dueDate: this.dueDate() || null,
      contactGstin: this.contactGstin() || null,
      placeOfSupplyStateCode: this.placeOfSupplyStateCode() || null,
      billingAddress: this.billingAddress() || null,
      shippingAddress: this.shippingAddress() || null,
      currencyCode: this.currencyCode() || 'INR',
      exchangeRate: this.exchangeRate() || 1,
      landedCostAmount: 0,
      isReverseCharge: this.isReverseCharge(),
      notes: this.notes() || null,
      termsAndConditions: this.termsAndConditions() || null,
      customFields: compactValues(this.customValues()),
      lines,
    };
  }

  private messageFrom(err: unknown): string {
    const errObj = err as {
      error?: {
        message?: string;
        title?: string;
        detail?: string;
        errors?: Record<string, string[]>;
      };
      statusText?: string;
      message?: string;
    } | null;

    const body = errObj?.error;
    if (body?.message) {
      return body.message;
    }
    if (body?.detail) {
      return body.detail;
    }
    if (body?.errors && typeof body.errors === 'object') {
      const messages = Object.values(body.errors).flat().filter(Boolean);
      if (messages.length > 0) {
        return messages.join(' ');
      }
    }
    if (body?.title) {
      return body.title;
    }
    return errObj?.message ?? 'The bill could not be saved.';
  }

  private today(): string {
    return new Date().toISOString().slice(0, 10);
  }
}


