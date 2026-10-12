import { ChangeDetectionStrategy } from '@angular/core';
import { ChangeDetectorRef, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { QuoteService, SalesLookupService, SaveQuoteRequest, blankGridLine } from '@bill-book/sales-core';
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
  TextInputComponent,
  CheckboxComponent,
  totalsOf,
  ProjectOption,
  ProjectOptionsService,
  CustomFieldValues,
  DocumentCustomFieldsComponent,
  compactValues,
} from '@bill-book/ui-components';
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
    ReactiveFormsModule,
    DocumentLineGridComponent,
    RouterModule,
    TextInputComponent,
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

  protected looksInterState(): boolean {
    const pos = this.form.controls.placeOfSupplyStateCode.value;
    return !!pos && pos !== '33';
  }

  protected copyBillingToShipping(event?: Event | boolean): void {
    const checked = typeof event === 'boolean' ? event : ((event?.target as HTMLInputElement)?.checked ?? true);
    if (checked) {
      this.form.controls.shippingAddress.setValue(this.form.controls.billingAddress.value);
    }
  }

  private fb = inject(FormBuilder);
  private quoteService = inject(QuoteService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  isEdit = false;
  quoteId: number | null = null;

  form = this.fb.group({
    documentDate: [new Date().toISOString().split('T')[0], Validators.required],
    validUntil: [new Date().toISOString().split('T')[0], Validators.required],
    contactId: [0, [Validators.required, Validators.min(1)]],
    contactGstin: [''],
    placeOfSupplyStateCode: [''],
    currencyCode: ['INR', Validators.required],
    exchangeRate: [1, [Validators.required, Validators.min(0.0001)]],
    billingAddress: [''],
    shippingAddress: [''],
    notes: [''],
    termsAndConditions: ['']
  });
  
  private readonly changes = inject(ChangeDetectorRef);

  lines: DocumentLine[] = [];

  /** The chosen customer as it reads on the form; the id itself is the `contactId` control. */
  readonly contactLabel = signal('');

  /** The customer and item pickers (TK-15). */
  readonly picker = new SalesPicker(inject(SalesLookupService), {
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
    const id = this.route.snapshot.paramMap.get('id');
    if (id && id !== 'new') {
      this.isEdit = true;
      this.quoteId = +id;
      this.loadQuote();
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
        contactGstin: q.contactGstin || '',
        placeOfSupplyStateCode: (q as any).placeOfSupplyStateCode || (q as any).placeOfSupplyStateId?.toString() || '',
        currencyCode: q.currencyCode,
        exchangeRate: q.exchangeRate,
        billingAddress: q.billingAddress || '',
        shippingAddress: q.shippingAddress || '',
        notes: q.notes,
        termsAndConditions: q.termsAndConditions
      });
      this.customValues.set(q.customFields ?? {});
      // Map lines if API returned them
      this.lines = (q.lines || []).map(l => ({
        itemId: l.itemId,
        description: l.description,
        hsnSacCode: l.hsnSacCode,
        accountId: l.accountId,
        taxTreatment: l.taxTreatment || 'Taxable',
        taxMasterId: l.taxMasterId,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent || 0
      } as any));
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

    if (row.meta && !this.form.controls.contactGstin.value) {
      this.form.controls.contactGstin.setValue(row.meta);
    }
  }

  save() {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

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
      notes: val.notes || undefined,
      customFields: compactValues(this.customValues()),
      termsAndConditions: val.termsAndConditions || undefined,
      lines: this.lines.map(l => ({
        itemId: l.itemId ?? undefined,
        description: l.description ?? undefined,
        hsnSacCode: l.hsnSacCode ?? undefined,
        accountId: l.accountId ?? undefined,
        taxTreatment: l.taxTreatment,
        taxMasterId: l.taxMasterId ?? undefined,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        discountPercent: l.discountPercent ?? 0
      } as any) )
    };

    if (this.isEdit && this.quoteId) {
      this.quoteService.update(this.quoteId, request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    } else {
      this.quoteService.create(request).subscribe(() => { void this.router.navigate(['../'], { relativeTo: this.route }); });
    }
  }
}


