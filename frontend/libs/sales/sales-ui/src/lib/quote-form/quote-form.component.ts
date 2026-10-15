import { ChangeDetectionStrategy } from '@angular/core';
import { ChangeDetectorRef, Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  QuoteService,
  SalesLookupService,
  SaveQuoteRequest,
  blankGridLine,
  toGridLine,
  toApiLine,
  ContactAddressOption,
  formatContactAddress,
  formatAddressOptionLabel,
} from '@bill-book/sales-core';
import {
  CreatePageComponent,
  NumberFieldComponent,
  ExchangeRateFieldComponent,
  MoreDetailsComponent,
  BranchDateInputComponent,
  DocumentLine,
  DocumentLineContext,
  DocumentLineGridComponent,
  FormFieldComponent,
  LookupDialogComponent,
  ScanComponent,
  addScannedItem,
  LookupRow,
  TextareaComponent,
  CheckboxComponent,
  totalsOf,
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
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { SalesPicker } from '../sales-picker';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-quote-form',
  standalone: true,
  imports: [
    CreatePageComponent,
    NumberFieldComponent,
    ExchangeRateFieldComponent,
    MoreDetailsComponent,
    DocumentCustomFieldsComponent,
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    DocumentLineGridComponent,
    RouterModule,
    SelectComponent,
    AddressSummaryComponent,
    TextareaComponent,
    CheckboxComponent,
    BranchDateInputComponent,
    FormFieldComponent,
    LookupDialogComponent,
    ScanComponent,
  ],
  templateUrl: './quote-form.component.html',
  styleUrls: ['./quote-form.component.scss']
})
export class QuoteFormComponent implements OnInit {
  /** The document's custom field values (TK-112). */
  protected readonly customValues = signal<CustomFieldValues>({});
  protected readonly documentNo = signal<string | null>(null);
  protected readonly saving = signal<boolean>(false);

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

  protected looksInterState(): boolean {
    const pos = this.form.controls.placeOfSupplyStateCode.value;
    return !!pos && pos !== this.getOrgStateCode();
  }

  protected copyBillingToShipping(event?: Event | boolean): void {
    const checked = typeof event === 'boolean' ? event : ((event?.target as HTMLInputElement)?.checked ?? true);
    this.onSameAsBillingChange(checked);
  }

  private fb = inject(FormBuilder);
  private quoteService = inject(QuoteService);
  private printTemplatesService = inject(PrintTemplateService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  quoteId: number | null = null;

  /** Available print templates for this transaction type */
  protected readonly printTemplates = signal<PrintTemplateChoice[]>([]);

  form = this.fb.group({
    documentDate: [new Date().toISOString().split('T')[0], Validators.required],
    validUntil: [new Date().toISOString().split('T')[0], Validators.required],
    contactId: [0, [Validators.required, Validators.min(1)]],
    printTemplateId: [null as number | null],
    contactGstin: [''],
    placeOfSupplyStateCode: [this.getOrgStateCode()],
    currencyCode: [this.getOrgCurrency(), Validators.required],
    exchangeRate: [1, [Validators.required, Validators.min(0.0001)]],
    billingAddress: [''],
    shippingAddress: [''],
    notes: [''],
    termsAndConditions: ['']
  });
  
  private readonly changes = inject(ChangeDetectorRef);
  private readonly lookups = inject(SalesLookupService);

  lines: DocumentLine[] = [blankGridLine(1)];

  /** The chosen customer as it reads on the form; the id itself is the `contactId` control. */
  readonly contactLabel = signal('');

  /** The customer and item pickers (TK-15). */
  readonly picker = new SalesPicker(this.lookups, {
    customer: (row) => this.chooseCustomer(row),
    item: (index, row) => {
      this.lines = SalesPicker.withItem(this.lines, index, row, this.context);
    },
    scanned: (row) => {
      this.lines = addScannedItem(this.lines, row, this.context, blankGridLine);
      // The answer arrives after a lookup, outside any template event.
      this.changes.markForCheck();
    },
  });

  readonly contactAddresses = signal<ContactAddressOption[]>([]);
  readonly selectedBillingAddressId = signal<number | null>(null);
  readonly selectedShippingAddressId = signal<number | null>(null);
  readonly sameAsBilling = signal(true);

  readonly billingAddressOptions = computed<BbSelectOption<number>[]>(() =>
    this.contactAddresses().map((a) => ({
      value: a.contactAddressId,
      label: formatAddressOptionLabel(a),
    }))
  );

  readonly shippingAddressOptions = computed<BbSelectOption<number>[]>(() =>
    this.contactAddresses().map((a) => ({
      value: a.contactAddressId,
      label: formatAddressOptionLabel(a),
    }))
  );

  readonly billingAddressPlaceholder = computed(() => {
    if (!this.form.controls.contactId.value) {
      return 'Choose customer first…';
    }
    return this.billingAddressOptions().length > 0
      ? 'Choose from customer\'s addresses…'
      : 'No addresses saved for this customer';
  });

  readonly shippingAddressPlaceholder = computed(() => {
    if (!this.form.controls.contactId.value) {
      return 'Choose customer first…';
    }
    return this.shippingAddressOptions().length > 0
      ? 'Choose from customer\'s addresses…'
      : 'No addresses saved for this customer';
  });

  private async loadContactAddresses(contactId: number, preserveExisting = false): Promise<void> {
    const summary = await this.lookups.contactAddresses?.(contactId);
    if (!summary) {
      this.contactAddresses.set([]);
      return;
    }
    this.contactAddresses.set(summary.addresses);

    const contactGstin = summary.gstin || this.form.controls.contactGstin.value || '';
    if (!preserveExisting || !this.form.controls.contactGstin.value) {
      this.form.controls.contactGstin.setValue(contactGstin);
    }

    if (summary.addresses.length === 0) return;

    if (!preserveExisting || !this.form.controls.billingAddress.value) {
      const defaultBilling = summary.addresses.find((a) => a.isDefaultBilling) ?? summary.addresses[0];
      if (defaultBilling) {
        this.selectedBillingAddressId.set(defaultBilling.contactAddressId);
        this.form.controls.billingAddress.setValue(formatContactAddress(defaultBilling));
      }
    } else {
      const matched = summary.addresses.find((a) => formatContactAddress(a) === this.form.controls.billingAddress.value);
      if (matched) {
        this.selectedBillingAddressId.set(matched.contactAddressId);
      }
    }

    if (!preserveExisting || !this.form.controls.shippingAddress.value) {
      if (this.sameAsBilling()) {
        this.form.controls.shippingAddress.setValue(this.form.controls.billingAddress.value);
        this.selectedShippingAddressId.set(this.selectedBillingAddressId());
      } else {
        const defaultShipping = summary.addresses.find((a) => a.isDefaultShipping) ?? summary.addresses[0];
        if (defaultShipping) {
          this.selectedShippingAddressId.set(defaultShipping.contactAddressId);
          this.form.controls.shippingAddress.setValue(formatContactAddress(defaultShipping));
        }
      }
    } else {
      const matched = summary.addresses.find((a) => formatContactAddress(a) === this.form.controls.shippingAddress.value);
      if (matched) {
        this.selectedShippingAddressId.set(matched.contactAddressId);
      }
    }
    this.changes.markForCheck();
  }

  onBillingAddressChange(addressId: number | null): void {
    this.selectedBillingAddressId.set(addressId);
    if (addressId === null) return;
    const addr = this.contactAddresses().find((a) => a.contactAddressId === addressId);
    if (addr) {
      const formatted = formatContactAddress(addr);
      this.form.controls.billingAddress.setValue(formatted);
      if (this.sameAsBilling()) {
        this.form.controls.shippingAddress.setValue(formatted);
        this.selectedShippingAddressId.set(addressId);
      }
    }
  }

  onShippingAddressChange(addressId: number | null): void {
    this.selectedShippingAddressId.set(addressId);
    if (addressId === null) return;
    const addr = this.contactAddresses().find((a) => a.contactAddressId === addressId);
    if (addr) {
      this.form.controls.shippingAddress.setValue(formatContactAddress(addr));
    }
  }

  onSameAsBillingChange(checked: boolean): void {
    this.sameAsBilling.set(checked);
    if (checked) {
      this.form.controls.shippingAddress.setValue(this.form.controls.billingAddress.value);
      this.selectedShippingAddressId.set(this.selectedBillingAddressId());
    }
  }

  readonly selectedBillingAddress = computed(() => {
    const id = this.selectedBillingAddressId();
    if (id !== null) {
      const match = this.contactAddresses().find((a) => a.contactAddressId === id);
      if (match) {
        return {
          title: this.contactLabel() || match.label || 'Billing Address',
          badge: match.isDefaultBilling ? 'Default Billing' : (match.kind || null),
          lines: formatContactAddress(match),
          email: null,
          phone: null,
          gstin: this.form.controls.contactGstin.value || null,
        };
      }
    }
    const raw = this.form.controls.billingAddress.value;
    if (raw && raw.trim()) {
      return {
        title: this.contactLabel() || 'Billing Address',
        badge: null,
        lines: raw,
        email: null,
        phone: null,
        gstin: this.form.controls.contactGstin.value || null,
      };
    }
    return null;
  });

  readonly selectedShippingAddress = computed(() => {
    if (this.sameAsBilling()) {
      const billing = this.selectedBillingAddress();
      if (!billing) return null;
      return {
        ...billing,
        badge: 'Same as Billing',
      };
    }
    const id = this.selectedShippingAddressId();
    if (id !== null) {
      const match = this.contactAddresses().find((a) => a.contactAddressId === id);
      if (match) {
        return {
          title: this.contactLabel() || match.label || 'Shipping Address',
          badge: match.isDefaultShipping ? 'Default Shipping' : (match.kind || null),
          lines: formatContactAddress(match),
          email: null,
          phone: null,
          gstin: this.form.controls.contactGstin.value || null,
        };
      }
    }
    const raw = this.form.controls.shippingAddress.value;
    if (raw && raw.trim()) {
      return {
        title: this.contactLabel() || 'Shipping Address',
        badge: null,
        lines: raw,
        email: null,
        phone: null,
        gstin: this.form.controls.contactGstin.value || null,
      };
    }
    return null;
  });

  context: DocumentLineContext = {
    isInterState: false,
    currencyDecimals: 2,
    allowFreeTextLines: true,
    discountBeforeTax: true,
    discountLevel: 'Line',
    readonly: false
  };

  get totals() {
    return totalsOf(this.lines);
  }

  /** The branch's open projects, for tagging a line (TK-105). Empty for a user who cannot read them. */
  protected readonly projectOptions = signal<readonly ProjectOption[]>([]);
  private readonly projectSource = inject(ProjectOptionsService);

  ngOnInit() {
    void this.projectSource.load().then((options) => this.projectOptions.set(options));
    void this.loadPrintTemplates();
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEdit = true;
      this.quoteId = +id;
      this.loadQuote();
    }
  }

  private async loadPrintTemplates(): Promise<void> {
    try {
      const choices = await this.printTemplatesService.choices('QTE');
      this.printTemplates.set(choices);
      if (!this.isEdit) {
        const defaultTmpl = choices.find((t) => t.isDefault) ?? choices[0];
        if (defaultTmpl && !this.form.controls.printTemplateId.value) {
          this.form.controls.printTemplateId.setValue(defaultTmpl.printTemplateId);
        }
      }
    } catch {
      // ignore
    }
  }

  loadQuote() {
    if (!this.quoteId) return;
    this.quoteService.get(this.quoteId).subscribe(q => {
      this.documentNo.set((q as any).documentNo ?? (q as any).quoteNumber ?? null);
      this.contactLabel.set(SalesPicker.savedLabel(null, q.contactName, q.contactId));
      this.form.patchValue({
        documentDate: q.documentDate,
        validUntil: q.validUntil,
        contactId: q.contactId,
        printTemplateId: (q as any).printTemplateId ?? null,
        contactGstin: q.contactGstin || '',
        placeOfSupplyStateCode: (q as any).placeOfSupplyStateCode || (q as any).placeOfSupplyStateId?.toString() || '',
        currencyCode: q.currencyCode,
        exchangeRate: q.exchangeRate,
        billingAddress: q.billingAddress || '',
        shippingAddress: q.shippingAddress || '',
        notes: q.notes,
        termsAndConditions: q.termsAndConditions
      });
      if (q.contactId) {
        void this.loadContactAddresses(q.contactId, true);
      }
      this.customValues.set(q.customFields ?? {});
      // Map lines through the scale boundary. Straight in, a ₹100 line reads as zero.
      this.lines = (q.lines && q.lines.length > 0)
        ? q.lines.map((l, i) => toGridLine(l, i + 1))
        : [blankGridLine(1)];
      this.changes.markForCheck();
    });
  }

  onLinesChange(newLines: readonly DocumentLine[]) {
    this.lines = [...newLines];
  }

  /** A scanned code (TK-161): its item goes on the lines, or the picker opens on it. */
  onScan(code: string) {
    if (this.context.readonly) {
      return;
    }
    void this.picker.scanItem(code);
  }

  onPickItem(index: number) {
    void this.picker.openItem(index);
  }

  openCustomerPicker() {
    void this.picker.openCustomer();
  }

  /** The customer, chosen by name. Their GSTIN fills the field only when it is still empty. */
  private chooseCustomer(row: LookupRow) {
    this.contactLabel.set(SalesPicker.label(row));
    this.form.controls.contactId.setValue(row.id);
    this.form.controls.contactId.markAsTouched();

    this.form.controls.contactGstin.setValue(row.meta ?? '');
    void this.loadContactAddresses(row.id);
  }

  save() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    const priced = this.lines.filter(l => (l.quantity ?? 0) > 0);
    if (priced.length === 0) {
      return;
    }

    this.saving.set(true);

    const val = this.form.value;
    const request: SaveQuoteRequest = {
      documentDate: val.documentDate!,
      validUntil: val.validUntil!,
      contactId: val.contactId!,
      contactGstin: val.contactGstin || undefined,
      placeOfSupplyStateCode: val.placeOfSupplyStateCode || undefined,
      currencyCode: val.currencyCode!,
      exchangeRate: val.exchangeRate!,
      billingAddress: val.billingAddress || undefined,
      shippingAddress: val.shippingAddress || undefined,
      printTemplateId: val.printTemplateId ?? undefined,
      notes: val.notes || undefined,
      customFields: compactValues(this.customValues()),
      termsAndConditions: val.termsAndConditions || undefined,
      lines: priced.map(l => {
        const api = toApiLine(l);
        return {
          itemId: api.itemId ?? undefined,
          description: api.description ?? undefined,
          hsnSacCode: api.hsnSacCode ?? undefined,
          accountId: api.accountId ?? undefined,
          taxTreatment: api.taxTreatment ?? 'Taxable',
          taxMasterId: api.taxMasterId ?? undefined,
          quantity: api.quantity ?? 0,
          unitPrice: api.unitPrice ?? 0,
          discountPercent: api.discountPercent ?? 0,
          lineNotes: api.lineNotes ?? undefined,
        };
      })
    };

    if (this.isEdit && this.quoteId) {
      this.quoteService.update(this.quoteId, request).subscribe({
        next: () => { void this.router.navigate(['../'], { relativeTo: this.route }); },
        error: () => { this.saving.set(false); }
      });
    } else {
      this.quoteService.create(request).subscribe({
        next: () => { void this.router.navigate(['../'], { relativeTo: this.route }); },
        error: () => { this.saving.set(false); }
      });
    }
  }
}


