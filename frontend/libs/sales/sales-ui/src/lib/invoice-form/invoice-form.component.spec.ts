import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder } from '@angular/forms';
import { of, throwError } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { InvoiceFormComponent } from './invoice-form.component';
import { InvoiceService, InvoiceView, SaveInvoiceRequest } from '@bill-book/sales-core';
import { DocumentLine } from '@bill-book/ui-components';

describe('InvoiceFormComponent (sales/sales-ui/invoice-form)', () => {
  let mockInvoiceService: {
    get: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    post: ReturnType<typeof vi.fn>;
    voidInvoice: ReturnType<typeof vi.fn>;
  };
  let mockRouter: Partial<Router>;
  let mockActivatedRoute: {
    snapshot: {
      paramMap: {
        get: ReturnType<typeof vi.fn>;
      };
    };
  };

  const sampleInvoiceView: InvoiceView = {
    invoiceId: 42,
    documentNo: 'INV-2026-0042',
    documentDate: '2026-08-18',
    dueDate: '2026-09-18',
    contactId: 10,
    contactName: 'Royal Jewelers',
    currencyCode: 'INR',
    exchangeRate: 1,
    status: 'Draft',
    placeOfSupplyStateId: 29,
    subTotal: 10000,
    discountAmount: 500,
    taxableAmount: 9500,
    cgstAmount: 855,
    sgstAmount: 855,
    igstAmount: 0,
    cessAmount: 0,
    roundOffAmount: 0,
    totalAmount: 11210,
    isInterState: false,
    billingAddress: '123 MG Road, Bangalore',
    shippingAddress: '123 MG Road, Bangalore',
    notes: 'Payment within 30 days',
    lines: [
      {
        invoiceDetailId: 1001,
        lineNumber: 1,
        itemId: 5,
        itemLabel: 'Diamond Ring 18K',
        hsnSacCode: '7113',
        description: '18K Diamond Solitaire Ring',
        quantity: 1,
        conversionFactor: 1,
        unitPrice: 10000,
        discountPercent: 5,
        discountAmount: 500,
        grossAmount: 10000,
        taxableAmount: 9500,
        taxTreatment: 'Taxable',
        taxMasterId: 3,
        taxAmount: 1710,
        lineTotal: 11210,
        accountId: 201,
        lineNotes: undefined,
        taxes: [
          { taxComponent: 'CGST', rate: 9, taxableAmount: 9500, amount: 855 },
          { taxComponent: 'SGST', rate: 9, taxableAmount: 9500, amount: 855 }
        ]
      }
    ]
  };

  const sampleLines: DocumentLine[] = [
    {
      detailId: 1,
      lineNumber: 1,
      itemId: 5,
      itemLabel: 'Diamond Ring 18K',
      hsnSacCode: '7113',
      description: '18K Diamond Solitaire Ring',
      warehouseId: null,
      quantity: 2,
      uomId: null,
      uomLabel: null,
      conversionFactor: 1,
      baseQuantity: 2,
      unitPrice: 5000,
      isPriceInclusive: false,
      discountPercent: 10,
      discountAmount: 1000,
      grossAmount: 10000,
      taxableAmount: 9000,
      taxTreatment: 'Taxable',
      taxMasterId: 3,
      taxGroupId: null,
      taxAmount: 1620,
      taxes: [
        { component: 'Cgst', subAccountId: 0, rate: 9, taxableAmount: 9000, amount: 810 },
        { component: 'Sgst', subAccountId: 0, rate: 9, taxableAmount: 9000, amount: 810 }
      ],
      lineType: 'Stock',
      accountId: 201,
      fixedAssetCategoryId: null,
      lineTotal: 10620,
      itemBatchId: null,
      lineNotes: null
    }
  ];

  beforeEach(() => {
    mockInvoiceService = {
      get: vi.fn().mockReturnValue(of(sampleInvoiceView)),
      create: vi.fn().mockReturnValue(of({ invoiceId: 43 })),
      update: vi.fn().mockReturnValue(of({ invoiceId: 42 })),
      post: vi.fn().mockReturnValue(of({ success: true })),
      voidInvoice: vi.fn().mockReturnValue(of({ success: true }))
    };

    mockRouter = {
      navigate: vi.fn().mockResolvedValue(true)
    };

    mockActivatedRoute = {
      snapshot: {
        paramMap: {
          get: vi.fn().mockReturnValue(null) // default is new
        }
      }
    };

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: InvoiceService, useValue: mockInvoiceService },
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute }
      ]
    });
  });

  const createComponent = (): InvoiceFormComponent => {
    return TestBed.runInInjectionContext(() => new InvoiceFormComponent());
  };

  describe('Tier 1: Feature Coverage (R4 Specification)', () => {
    it('INV-T1-01: Form initializes with default values in Create mode', () => {
      const comp = createComponent();
      comp.ngOnInit();

      expect(comp.isEdit).toBe(false);
      expect(comp.invoiceId).toBeNull();
      expect(comp.status).toBe('Draft');
      expect(comp.form.get('currencyCode')?.value).toBe('INR');
      expect(comp.form.get('exchangeRate')?.value).toBe(1);
      expect(comp.form.get('contactId')?.value).toBe(1);
      expect(comp.form.get('documentDate')?.value).toBeTruthy();
      expect(comp.context.readonly).toBe(false);
      expect(comp.context.currencyDecimals).toBe(2);
    });

    it('INV-T1-02: onLinesChange updates component lines and calculates totals', () => {
      const comp = createComponent();
      comp.onLinesChange(sampleLines);

      expect(comp.lines.length).toBe(1);
      expect(comp.lines[0].grossAmount).toBe(10000);
      expect(comp.totals.subTotal).toBe(10000);
      expect(comp.totals.discountAmount).toBe(1000);
      expect(comp.totals.cgstAmount).toBe(810);
      expect(comp.totals.sgstAmount).toBe(810);
      expect(comp.totals.totalAmount).toBe(10620);
    });

    it('INV-T1-03: Create invoice saves request payload exactly matching backend DTO', () => {
      const comp = createComponent();
      comp.ngOnInit();
      comp.form.patchValue({
        documentDate: '2026-08-19',
        dueDate: '2026-09-19',
        contactId: 10,
        currencyCode: 'INR',
        exchangeRate: 1,
        billingAddress: 'Bangalore HQ',
        notes: 'Test Notes'
      });
      comp.onLinesChange(sampleLines);

      comp.save();

      expect(mockInvoiceService.create).toHaveBeenCalledTimes(1);
      const requestArg: SaveInvoiceRequest = mockInvoiceService.create.mock.calls[0][0];

      expect(requestArg.documentDate).toBe('2026-08-19');
      expect(requestArg.dueDate).toBe('2026-09-19');
      expect(requestArg.contactId).toBe(10);
      expect(requestArg.currencyCode).toBe('INR');
      expect(requestArg.exchangeRate).toBe(1);
      expect(requestArg.billingAddress).toBe('Bangalore HQ');
      expect(requestArg.notes).toBe('Test Notes');
      expect(requestArg.lines.length).toBe(1);
      expect(requestArg.lines[0]).toEqual({
        itemId: 5,
        description: '18K Diamond Solitaire Ring',
        hsnSacCode: '7113',
        accountId: 201,
        taxTreatment: 'Taxable',
        taxMasterId: 3,
        quantity: 2,
        unitPrice: 5000,
        discountPercent: 10
      });
    });

    it('INV-T1-04: Edit mode loads invoice by ID and maps lines into DocumentLine model', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();

      expect(comp.isEdit).toBe(true);
      expect(comp.invoiceId).toBe(42);
      expect(mockInvoiceService.get).toHaveBeenCalledWith(42);
      expect(comp.form.get('documentDate')?.value).toBe('2026-08-18');
      expect(comp.form.get('dueDate')?.value).toBe('2026-09-18');
      expect(comp.form.get('contactId')?.value).toBe(10);
      expect(comp.lines.length).toBe(1);
      expect(comp.lines[0].itemLabel).toBe('Diamond Ring 18K');
      expect(comp.lines[0].lineTotal).toBe(11210);
    });

    it('INV-T1-05: Update invoice calls invoiceService.update with invoice ID and payload', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();

      comp.save();

      expect(mockInvoiceService.update).toHaveBeenCalledTimes(1);
      expect(mockInvoiceService.update).toHaveBeenCalledWith(42, expect.any(Object));
    });

    it('INV-T1-06: Post invoice transitions status to Posted and disables editing', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();

      comp.postInvoice();

      expect(mockInvoiceService.post).toHaveBeenCalledWith(42);
      expect(comp.status).toBe('Posted');
      expect(comp.context.readonly).toBe(true);
      expect(comp.form.disabled).toBe(true);
      expect(comp.posting).toBe(false);
    });

    it('INV-T1-07: Void invoice prompts for reason, calls service, and transitions status to Void', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      vi.spyOn(window, 'prompt').mockReturnValue('Order cancelled by customer');

      const comp = createComponent();
      comp.ngOnInit();

      comp.voidInvoice();

      expect(mockInvoiceService.voidInvoice).toHaveBeenCalledWith(42, { reason: 'Order cancelled by customer' });
      expect(comp.status).toBe('Void');
      expect(comp.context.readonly).toBe(true);
      expect(comp.form.disabled).toBe(true);
      expect(comp.voiding).toBe(false);
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('INV-T2-01: Invalid form prevents submission and aborts create API call', () => {
      const comp = createComponent();
      comp.ngOnInit();

      // Clear required documentDate and set invalid exchangeRate
      comp.form.patchValue({
        documentDate: '',
        exchangeRate: 0
      });

      comp.save();
      expect(mockInvoiceService.create).not.toHaveBeenCalled();
    });

    it('INV-T2-02: Non-draft invoice loaded (e.g. Posted) automatically disables form', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const postedView: InvoiceView = { ...sampleInvoiceView, status: 'Posted' };
      mockInvoiceService.get.mockReturnValue(of(postedView));

      const comp = createComponent();
      comp.ngOnInit();

      expect(comp.status).toBe('Posted');
      expect(comp.context.readonly).toBe(true);
      expect(comp.form.disabled).toBe(true);
    });

    it('INV-T2-03: Cancelling void prompt aborts voiding operation without API call', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      vi.spyOn(window, 'prompt').mockReturnValue(null); // User clicked Cancel

      const comp = createComponent();
      comp.ngOnInit();

      comp.voidInvoice();
      expect(mockInvoiceService.voidInvoice).not.toHaveBeenCalled();
      expect(comp.status).toBe('Draft');
    });

    it('INV-T2-04: Handling API error during save resets saving flag to false', () => {
      mockInvoiceService.create.mockReturnValue(throwError(() => new Error('Server Error 500')));
      const comp = createComponent();
      comp.ngOnInit();

      comp.save();
      expect(comp.saving).toBe(false);
    });

    it('INV-T2-05: Handling API error during post or void resets loading flags', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      mockInvoiceService.post.mockReturnValue(throwError(() => new Error('Post Failed')));
      mockInvoiceService.voidInvoice.mockReturnValue(throwError(() => new Error('Void Failed')));
      vi.spyOn(window, 'prompt').mockReturnValue('Reason');

      const comp = createComponent();
      comp.ngOnInit();

      comp.postInvoice();
      expect(comp.posting).toBe(false);

      comp.voidInvoice();
      expect(comp.voiding).toBe(false);
    });
  });

  describe('Tier 3: Cross-Feature Interactions', () => {
    it('INV-T3-01: goBack triggers relative router navigation', () => {
      const comp = createComponent();
      comp.goBack();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['../'], { relativeTo: mockActivatedRoute as any });
    });

    it('INV-T3-02: Dynamic line calculation with inter-state taxes and discounts', () => {
      const comp = createComponent();
      comp.context = { ...comp.context, isInterState: true };

      const interStateLines: DocumentLine[] = [
        {
          ...sampleLines[0],
          taxes: [
            { component: 'Igst', subAccountId: 0, rate: 18, taxableAmount: 9000, amount: 1620 }
          ]
        }
      ];

      comp.onLinesChange(interStateLines);
      expect(comp.totals.subTotal).toBe(10000);
      expect(comp.totals.igstAmount).toBe(1620);
      expect(comp.totals.totalAmount).toBe(10620);
    });
  });

  describe('Tier 4: Real-World Sales Lifecycle Workflow', () => {
    it('INV-T4-01: Full invoice creation -> line items mapping -> submission -> navigation', () => {
      const comp = createComponent();
      comp.ngOnInit();

      // Step 1: User fills header fields
      comp.form.patchValue({
        documentDate: '2026-08-19',
        dueDate: '2026-09-02',
        contactId: 4,
        currencyCode: 'INR',
        exchangeRate: 1,
        billingAddress: '77 Brigade Road, Bangalore',
        notes: 'Thank you for your business!'
      });

      // Step 2: User adds line items
      comp.onLinesChange(sampleLines);
      expect(comp.totals.totalAmount).toBe(10620);

      // Step 3: User saves invoice
      comp.save();

      expect(mockInvoiceService.create).toHaveBeenCalledTimes(1);
      expect(mockRouter.navigate).toHaveBeenCalledWith(['../'], { relativeTo: mockActivatedRoute as any });
    });
  });
});
