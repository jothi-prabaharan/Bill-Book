import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';

import { QuoteFormComponent } from './quote-form/quote-form.component';
import { SalesOrderFormComponent } from './sales-order-form/sales-order-form.component';
import { CreditNoteFormComponent } from './credit-note-form/credit-note-form.component';
import { DeliveryChallanFormComponent } from './delivery-challan-form/delivery-challan-form.component';

import {
  QuoteService,
  SalesOrderService,
  CreditNoteService,
  DeliveryChallanService,
  LedgerService,
  OutstandingBalance,
  SaveQuoteRequest,
  SaveSalesOrderRequest,
  SaveCreditNoteRequest,
  SaveDeliveryChallanRequest
} from '@bill-book/sales-core';
import { AllocationRow, DocumentLine } from '@bill-book/ui-components';

describe('Sales Secondary Form Components (Quote, SalesOrder, CreditNote, DeliveryChallan)', () => {
  let mockRouter: Partial<Router>;
  let mockActivatedRoute: {
    snapshot: {
      paramMap: {
        get: ReturnType<typeof vi.fn>;
      };
    };
  };

  let mockQuoteService: {
    get: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
  };

  let mockSalesOrderService: {
    get: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
  };

  let mockCreditNoteService: {
    get: ReturnType<typeof vi.fn>;
    save: ReturnType<typeof vi.fn>;
  };

  let mockLedgerService: {
    outstandingBalances: ReturnType<typeof vi.fn>;
  };

  let mockDeliveryChallanService: {
    get: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
  };

  const sampleLine: DocumentLine = {
    detailId: 1,
    lineNumber: 1,
    itemId: 10,
    itemLabel: 'Gold Bar 24K',
    hsnSacCode: '7108',
    description: '10g 24K Gold Bar',
    warehouseId: 1,
    quantity: 1,
    uomId: 1,
    uomLabel: 'g',
    conversionFactor: 1,
    baseQuantity: 1,
    unitPrice: 75000,
    isPriceInclusive: false,
    discountPercent: 0,
    discountAmount: 0,
    grossAmount: 75000,
    taxableAmount: 75000,
    taxTreatment: 'Taxable',
    taxMasterId: 1,
    taxGroupId: null,
    taxAmount: 2250,
    taxes: [
      { component: 'Cgst', subAccountId: 0, rate: 1.5, taxableAmount: 75000, amount: 1125 },
      { component: 'Sgst', subAccountId: 0, rate: 1.5, taxableAmount: 75000, amount: 1125 }
    ],
    lineType: 'Stock',
    accountId: 101,
    fixedAssetCategoryId: null,
    lineTotal: 77250,
    itemBatchId: null,
    lineNotes: null
  };

  const allocationRow = (
    transactionId: number,
    outstanding: number,
    allocated: number,
  ): AllocationRow => ({
    transactionId,
    documentNo: `INV-2026-${transactionId}`,
    documentDate: '2026-08-10',
    totalAmount: outstanding,
    outstandingAmount: outstanding,
    allocatedAmount: allocated,
  });

  beforeEach(() => {
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

    mockQuoteService = {
      get: vi.fn().mockReturnValue(of({
        quoteId: 21,
        documentDate: '2026-08-18',
        validUntil: '2026-09-18',
        contactId: 5,
        currencyCode: 'INR',
        exchangeRate: 1,
        lines: []
      })),
      create: vi.fn().mockReturnValue(of({ quoteId: 21 })),
      update: vi.fn().mockReturnValue(of({ quoteId: 21 }))
    };

    mockSalesOrderService = {
      get: vi.fn().mockReturnValue(of({
        salesOrderId: 31,
        documentDate: '2026-08-18',
        deliveryDate: '2026-08-25',
        contactId: 5,
        currencyCode: 'INR',
        exchangeRate: 1,
        lines: []
      })),
      create: vi.fn().mockReturnValue(of({ salesOrderId: 31 })),
      update: vi.fn().mockReturnValue(of({ salesOrderId: 31 }))
    };

    mockCreditNoteService = {
      get: vi.fn().mockReturnValue(of({
        creditNoteId: 51,
        documentDate: '2026-08-18',
        invoiceId: 42,
        contactId: 5,
        reasonCode: 1,
        currencyCode: 'INR',
        exchangeRate: 1,
        lines: []
      })),
      save: vi.fn().mockReturnValue(of({ creditNoteId: 51 }))
    };

    mockLedgerService = {
      outstandingBalances: vi.fn().mockReturnValue(of<OutstandingBalance[]>([
        {
          contactId: 5,
          transactionTypeCode: 'INV',
          transactionId: 42,
          documentNo: 'INV-2026-001',
          documentDate: '2026-08-10',
          dueDate: '2026-09-10',
          totalAmount: 77250,
          paidAmount: 0,
          outstandingAmount: 77250
        },
        {
          contactId: 5,
          transactionTypeCode: 'SPM',
          transactionId: 77,
          documentNo: 'RCV-2026-004',
          documentDate: '2026-08-12',
          totalAmount: 1000,
          paidAmount: 0,
          outstandingAmount: -1000
        }
      ]))
    };

    mockDeliveryChallanService = {
      get: vi.fn().mockReturnValue(of({
        deliveryChallanId: 61,
        documentDate: '2026-08-18',
        contactId: 5,
        challanType: 1,
        vehicleNo: 'KA-01-AB-1234',
        dispatchDate: '2026-08-18',
        currencyCode: 'INR',
        exchangeRate: 1,
        lines: []
      })),
      create: vi.fn().mockReturnValue(of({ deliveryChallanId: 61 })),
      update: vi.fn().mockReturnValue(of({ deliveryChallanId: 61 }))
    };

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
        { provide: QuoteService, useValue: mockQuoteService },
        { provide: SalesOrderService, useValue: mockSalesOrderService },
        { provide: CreditNoteService, useValue: mockCreditNoteService },
        { provide: LedgerService, useValue: mockLedgerService },
        { provide: DeliveryChallanService, useValue: mockDeliveryChallanService }
      ]
    });
  });

  // =========================================================================
  // SECTION 1: QUOTE FORM
  // =========================================================================
  describe('1. QuoteFormComponent (R4 Specification)', () => {
    it('QOT-T1-01: Form initializes with valid defaults in Create mode', () => {
      const comp = TestBed.runInInjectionContext(() => new QuoteFormComponent());
      comp.ngOnInit();

      expect(comp.isEdit).toBe(false);
      expect(comp.quoteId).toBeNull();
      expect(comp.form.get('validUntil')?.value).toBeTruthy();
      expect(comp.form.get('currencyCode')?.value).toBe('INR');
      expect(comp.form.get('contactId')?.value).toBe(1);
    });

    it('QOT-T1-02: Create quote saves SaveQuoteRequest DTO and navigates back', () => {
      const comp = TestBed.runInInjectionContext(() => new QuoteFormComponent());
      comp.ngOnInit();
      comp.form.patchValue({
        documentDate: '2026-08-18',
        validUntil: '2026-09-18',
        contactId: 5,
        contactGstin: '29ABCDE1234F1Z5',
        currencyCode: 'INR',
        exchangeRate: 1,
        termsAndConditions: 'Validity 30 days'
      });
      comp.onLinesChange([sampleLine]);
      expect(comp.totals.subTotal).toBe(75000);
      expect(comp.totals.totalAmount).toBe(77250);

      comp.save();

      expect(mockQuoteService.create).toHaveBeenCalledTimes(1);
      const req: SaveQuoteRequest = mockQuoteService.create.mock.calls[0][0];
      expect(req.documentDate).toBe('2026-08-18');
      expect(req.validUntil).toBe('2026-09-18');
      expect(req.contactId).toBe(5);
      expect(req.termsAndConditions).toBe('Validity 30 days');
      expect(req.lines.length).toBe(1);
      expect(req.lines[0].itemId).toBe(10);
      expect(mockRouter.navigate).toHaveBeenCalledWith(['../'], { relativeTo: mockActivatedRoute as any });
    });

    it('QOT-T1-03: Edit quote loads existing quote by ID and updates', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('21');
      const comp = TestBed.runInInjectionContext(() => new QuoteFormComponent());
      comp.ngOnInit();

      expect(comp.isEdit).toBe(true);
      expect(comp.quoteId).toBe(21);
      expect(mockQuoteService.get).toHaveBeenCalledWith(21);

      comp.save();
      expect(mockQuoteService.update).toHaveBeenCalledWith(21, expect.any(Object));
    });
  });

  // =========================================================================
  // SECTION 2: SALES ORDER FORM
  // =========================================================================
  describe('2. SalesOrderFormComponent (R4 Specification)', () => {
    it('SOR-T1-01: Form initializes with deliveryDate control in Create mode', () => {
      const comp = TestBed.runInInjectionContext(() => new SalesOrderFormComponent());
      comp.ngOnInit();

      expect(comp.isEdit).toBe(false);
      expect(comp.salesOrderId).toBeNull();
      expect(comp.form.get('deliveryDate')?.value).toBeTruthy();
      expect(comp.form.get('currencyCode')?.value).toBe('INR');
    });

    it('SOR-T1-02: Create sales order saves SaveSalesOrderRequest DTO and navigates back', () => {
      const comp = TestBed.runInInjectionContext(() => new SalesOrderFormComponent());
      comp.ngOnInit();
      comp.form.patchValue({
        documentDate: '2026-08-18',
        deliveryDate: '2026-08-25',
        contactId: 5,
        currencyCode: 'INR',
        exchangeRate: 1,
        notes: 'Urgent Delivery'
      });
      comp.onLinesChange([sampleLine]);
      expect(comp.totals.subTotal).toBe(75000);
      expect(comp.totals.totalAmount).toBe(77250);

      comp.save();

      expect(mockSalesOrderService.create).toHaveBeenCalledTimes(1);
      const req: SaveSalesOrderRequest = mockSalesOrderService.create.mock.calls[0][0];
      expect(req.documentDate).toBe('2026-08-18');
      expect(req.deliveryDate).toBe('2026-08-25');
      expect(req.contactId).toBe(5);
      expect(req.notes).toBe('Urgent Delivery');
      expect(req.lines.length).toBe(1);
      expect(mockRouter.navigate).toHaveBeenCalledWith(['../'], { relativeTo: mockActivatedRoute as any });
    });

    it('SOR-T1-03: Edit sales order loads existing order by ID and updates', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('31');
      const comp = TestBed.runInInjectionContext(() => new SalesOrderFormComponent());
      comp.ngOnInit();

      expect(comp.isEdit).toBe(true);
      expect(comp.salesOrderId).toBe(31);
      expect(mockSalesOrderService.get).toHaveBeenCalledWith(31);

      comp.save();
      expect(mockSalesOrderService.update).toHaveBeenCalledWith(31, expect.any(Object));
    });
  });

  // =========================================================================
  // SECTION 3: CREDIT NOTE FORM
  // =========================================================================
  describe('3. CreditNoteFormComponent (R4 Specification)', () => {
    it('CRN-T1-01: Form initializes with invoiceId and reasonCode controls', () => {
      const comp = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      comp.ngOnInit();

      expect(comp.isEdit).toBe(false);
      expect(comp.creditNoteId).toBeNull();
      expect(comp.form.get('reasonCode')?.value).toBe(1);
      expect(comp.form.get('currencyCode')?.value).toBe('INR');
    });

    it('CRN-T1-02: Create credit note saves SaveCreditNoteRequest DTO and navigates back', () => {
      const comp = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      comp.ngOnInit();
      comp.form.patchValue({
        documentDate: '2026-08-18',
        invoiceId: '42',
        contactId: 5,
        reasonCode: 2,
        currencyCode: 'INR',
        exchangeRate: 1,
        notes: 'Damaged item return'
      });
      comp.onLinesChange([sampleLine]);
      expect(comp.totals.subTotal).toBe(75000);
      expect(comp.totals.totalAmount).toBe(77250);

      comp.save();

      expect(mockCreditNoteService.save).toHaveBeenCalledTimes(1);
      const req: SaveCreditNoteRequest = mockCreditNoteService.save.mock.calls[0][0];
      expect(req.documentDate).toBe('2026-08-18');
      expect(req.invoiceId).toBe(42);
      expect(req.contactId).toBe(5);
      expect(req.reasonCode).toBe(2);
      expect(req.notes).toBe('Damaged item return');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['../'], { relativeTo: mockActivatedRoute as any });
    });

    it('CRN-T1-03: Edit credit note loads existing credit note by ID', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('51');
      const comp = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      comp.ngOnInit();

      expect(comp.isEdit).toBe(true);
      expect(comp.creditNoteId).toBe(51);
      expect(mockCreditNoteService.get).toHaveBeenCalledWith(51);
    });

    it('CRN-T1-04: choosing a contact loads its outstanding invoices into the grid', () => {
      const comp = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      comp.ngOnInit();
      comp.form.patchValue({ contactId: 5 });

      comp.onContactChange();

      // Ledger type defaults to 3 (CONTROL) when the caller does not say.
      expect(mockLedgerService.outstandingBalances).toHaveBeenCalledWith(5);
      // Only the invoice is allocated against; a payment's negative balance
      // is not an invoice.
      expect(comp.allocationRows.length).toBe(1);
      expect(comp.allocationRows[0].transactionId).toBe(42);
    });

    it('CRN-T1-05: allocating one invoice sets it as the note invoice', () => {
      const comp = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      comp.ngOnInit();

      comp.onAllocationRowsChange([
        allocationRow(42, 77250, 500),
        allocationRow(43, 100000, 0)
      ]);

      expect(comp.form.get('invoiceId')?.value).toBe('42');
      expect(comp.allocationMessage).toBe('');
    });

    it('CRN-T1-06: allocating two invoices refuses the note and blocks save', () => {
      const comp = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      comp.ngOnInit();
      comp.onLinesChange([sampleLine]);
      comp.form.patchValue({ contactId: 5 });

      comp.onAllocationRowsChange([
        allocationRow(42, 77250, 500),
        allocationRow(43, 100000, 500)
      ]);

      expect(comp.form.get('invoiceId')?.value).toBe('');
      expect(comp.allocationMessage).toContain('exactly one invoice');

      comp.save();
      expect(mockCreditNoteService.save).not.toHaveBeenCalled();
    });

    it('CRN-T1-07: the note total is what the grid gets to allocate, in rupees', () => {
      const comp = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      comp.ngOnInit();
      comp.onLinesChange([sampleLine]);

      expect(comp.totals.totalAmount).toBe(77250);
      expect(comp.amountToAllocate).toBe(772.5);
    });
  });

  // =========================================================================
  // SECTION 4: DELIVERY CHALLAN FORM
  // =========================================================================
  describe('4. DeliveryChallanFormComponent (R4 Specification)', () => {
    it('DLC-T1-01: Form initializes with challanType, vehicleNo, and dispatchDate controls', () => {
      const comp = TestBed.runInInjectionContext(() => new DeliveryChallanFormComponent());
      comp.ngOnInit();

      expect(comp.isEdit).toBe(false);
      expect(comp.challanId).toBeNull();
      expect(comp.form.get('challanType')?.value).toBe(0);
      expect(comp.form.get('dispatchDate')?.value).toBeTruthy();
      expect(comp.form.get('currencyCode')?.value).toBe('INR');
    });

    it('DLC-T1-02: Create delivery challan saves SaveDeliveryChallanRequest DTO and navigates back', () => {
      const comp = TestBed.runInInjectionContext(() => new DeliveryChallanFormComponent());
      comp.ngOnInit();
      comp.form.patchValue({
        documentDate: '2026-08-18',
        contactId: 5,
        challanType: 1,
        vehicleNo: 'KA-04-E-5678',
        dispatchDate: '2026-08-18',
        currencyCode: 'INR',
        exchangeRate: 1,
        notes: 'Supply on approval'
      });
      comp.onLinesChange([sampleLine]);
      expect(comp.totals.subTotal).toBe(75000);
      expect(comp.totals.totalAmount).toBe(77250);

      comp.save();

      expect(mockDeliveryChallanService.create).toHaveBeenCalledTimes(1);
      const req: SaveDeliveryChallanRequest = mockDeliveryChallanService.create.mock.calls[0][0];
      expect(req.documentDate).toBe('2026-08-18');
      expect(req.contactId).toBe(5);
      expect(req.challanType).toBe(1);
      expect(req.vehicleNo).toBe('KA-04-E-5678');
      expect(req.dispatchDate).toBe('2026-08-18');
      expect(req.notes).toBe('Supply on approval');
      expect(mockRouter.navigate).toHaveBeenCalledWith(['../'], { relativeTo: mockActivatedRoute as any });
    });

    it('DLC-T1-03: Edit delivery challan loads existing challan by ID and updates', () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('61');
      const comp = TestBed.runInInjectionContext(() => new DeliveryChallanFormComponent());
      comp.ngOnInit();

      expect(comp.isEdit).toBe(true);
      expect(comp.challanId).toBe(61);
      expect(mockDeliveryChallanService.get).toHaveBeenCalledWith(61);

      comp.save();
      expect(mockDeliveryChallanService.update).toHaveBeenCalledWith(61, expect.any(Object));
    });
  });

  // =========================================================================
  // SECTION 5: BOUNDARY & VALIDATION EDGE CASES ACROSS ALL 4 FORMS
  // =========================================================================
  describe('5. Boundary Validation & Error Resilience', () => {
    it('VAL-T2-01: Invalid forms prevent submission across Quote, SalesOrder, CreditNote, DeliveryChallan', () => {
      const qot = TestBed.runInInjectionContext(() => new QuoteFormComponent());
      const sor = TestBed.runInInjectionContext(() => new SalesOrderFormComponent());
      const crn = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      const dlc = TestBed.runInInjectionContext(() => new DeliveryChallanFormComponent());

      qot.form.patchValue({ documentDate: '', validUntil: '' });
      sor.form.patchValue({ documentDate: '', deliveryDate: '' });
      crn.form.patchValue({ documentDate: '', invoiceId: '' });
      dlc.form.patchValue({ documentDate: '', dispatchDate: '' });

      qot.save();
      sor.save();
      crn.save();
      dlc.save();

      expect(mockQuoteService.create).not.toHaveBeenCalled();
      expect(mockSalesOrderService.create).not.toHaveBeenCalled();
      expect(mockCreditNoteService.save).not.toHaveBeenCalled();
      expect(mockDeliveryChallanService.create).not.toHaveBeenCalled();
    });
  });
});
