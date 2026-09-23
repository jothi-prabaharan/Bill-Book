import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup } from '@angular/forms';
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
  SaveDeliveryChallanRequest,
  ChallanType
} from '@bill-book/sales-core';
import { AllocationRow, DocumentLine, UiMessage } from '@bill-book/ui-components';

/**
 * The sales order form's members are `protected` — a template is the only thing
 * that should reach them — so the tests go through a declared shape rather than
 * loosening the component. Same pattern as the shared input components' specs.
 */
interface SalesOrderFormHarness {
  ngOnInit(): void;
  isEdit(): boolean;
  salesOrderId(): number | null;
  form: FormGroup;
  messages(): UiMessage[];
  totals(): { subTotal: number; totalAmount: number };
  onLinesChange(lines: readonly DocumentLine[]): void;
  save(): Promise<void>;
}

/**
 * The delivery challan form, by the same declared shape — protected members,
 * signals and awaited calls since TK-12.
 */
interface DeliveryChallanFormHarness {
  ngOnInit(): void;
  isEdit(): boolean;
  challanId(): number | null;
  status(): string;
  form: FormGroup;
  voidForm: FormGroup;
  messages(): UiMessage[];
  lines(): (DocumentLine & { salesOrderDetailId?: number | null })[];
  totals(): { subTotal: number; totalAmount: number };
  editable(): boolean;
  canPost(): boolean;
  canVoid(): boolean;
  challanTypes: { value: number; label: string }[];
  onLinesChange(lines: readonly DocumentLine[]): void;
  load(): Promise<void>;
  loadOrder(): Promise<void>;
  save(): Promise<void>;
  post(): Promise<void>;
  voidChallan(): Promise<void>;
}

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
    confirm: ReturnType<typeof vi.fn>;
    voidOrder: ReturnType<typeof vi.fn>;
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
    post: ReturnType<typeof vi.fn>;
    voidChallan: ReturnType<typeof vi.fn>;
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
    transactionTypeCode: 'INV',
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

    // Promises, not streams: SalesOrderService is awaited now, so that a
    // refusal can be caught and put into the message box with its own words.
    mockSalesOrderService = {
      get: vi.fn().mockResolvedValue({
        salesOrderId: 31,
        documentNo: 'SO/2026/0031',
        documentDate: '2026-08-18',
        deliveryDate: '2026-08-25',
        fulfilmentStatus: 'Open',
        status: 'Draft',
        contactId: 5,
        currencyCode: 'INR',
        exchangeRate: 1,
        lines: []
      }),
      create: vi.fn().mockResolvedValue({ salesOrderId: 31 }),
      update: vi.fn().mockResolvedValue(undefined),
      confirm: vi.fn().mockResolvedValue(undefined),
      voidOrder: vi.fn().mockResolvedValue(undefined)
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

    // Promises, like the order: a refusal is caught and shown in its own words.
    mockDeliveryChallanService = {
      get: vi.fn().mockResolvedValue({
        deliveryChallanId: 61,
        documentNo: 'DC/2026/0061',
        documentDate: '2026-08-18',
        contactId: 5,
        status: 'Draft',
        challanType: 1,
        vehicleNo: 'KA-01-AB-1234',
        dispatchDate: '2026-08-18',
        currencyCode: 'INR',
        exchangeRate: 1,
        lines: []
      }),
      create: vi.fn().mockResolvedValue({ deliveryChallanId: 61 }),
      update: vi.fn().mockResolvedValue({ deliveryChallanId: 61 }),
      post: vi.fn().mockResolvedValue({ deliveryChallanId: 61 }),
      voidChallan: vi.fn().mockResolvedValue({ deliveryChallanId: 61 })
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
  //
  // Rewritten for T2.2. Three things changed and each is load-bearing:
  //
  //  - the members are `protected`, because a template is the only thing that
  //    should be reaching them, so the tests go through a typed harness
  //  - state is in signals, so `isEdit` and `totals` are read as calls
  //  - `save()` awaits, so the assertions do too
  //
  // What has not changed is what these tests were for: that the DTO leaving the
  // screen says what was typed into it.
  describe('2. SalesOrderFormComponent (T2.2)', () => {
    const build = (): SalesOrderFormHarness =>
      TestBed.runInInjectionContext(
        () => new SalesOrderFormComponent(),
      ) as unknown as SalesOrderFormHarness;

    it('SOR-T1-01: Form initializes with deliveryDate control in Create mode', () => {
      const comp = build();
      comp.ngOnInit();

      expect(comp.isEdit()).toBe(false);
      expect(comp.salesOrderId()).toBeNull();
      expect(comp.form.get('deliveryDate')).not.toBeNull();
      expect(comp.form.get('documentDate')?.value).toBeTruthy();
      expect(comp.form.get('currencyCode')?.value).toBe('INR');
    });

    it('SOR-T1-02: Create sales order saves SaveSalesOrderRequest DTO and navigates to it', async () => {
      const comp = build();
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
      expect(comp.totals().subTotal).toBe(75000);
      expect(comp.totals().totalAmount).toBe(77250);

      await comp.save();

      expect(mockSalesOrderService.create).toHaveBeenCalledTimes(1);
      const req: SaveSalesOrderRequest = mockSalesOrderService.create.mock.calls[0][0];
      expect(req.documentDate).toBe('2026-08-18');
      expect(req.deliveryDate).toBe('2026-08-25');
      expect(req.contactId).toBe(5);
      expect(req.notes).toBe('Urgent Delivery');
      expect(req.lines.length).toBe(1);

      // The new order, not the list: it has a number now and the person who
      // raised it is usually about to confirm it.
      expect(mockRouter.navigate).toHaveBeenCalledWith(['/sales/sales-orders', 31]);
    });

    it('SOR-T1-03: Edit sales order loads existing order by ID and updates', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('31');
      const comp = build();
      comp.ngOnInit();

      expect(comp.isEdit()).toBe(true);
      expect(comp.salesOrderId()).toBe(31);
      expect(mockSalesOrderService.get).toHaveBeenCalledWith(31);

      comp.form.patchValue({ contactId: 5 });
      comp.onLinesChange([sampleLine]);
      await comp.save();

      expect(mockSalesOrderService.update).toHaveBeenCalledWith(31, expect.any(Object));
    });

    it('SOR-T1-04: a line’s amounts cross the scale boundary rather than going straight through', async () => {
      const comp = build();
      comp.ngOnInit();
      comp.form.patchValue({ contactId: 5 });

      // The grid speaks integer paise and six-decimal quantities. sampleLine is
      // one unit at 75000 paise, so the API must be told 1 at ₹750 — not 75000.
      comp.onLinesChange([sampleLine]);
      await comp.save();

      const req: SaveSalesOrderRequest = mockSalesOrderService.create.mock.calls[0][0];
      expect(req.lines[0].unitPrice).toBe(750);
      expect(req.lines[0].quantity).toBeCloseTo(0.000001, 9);

      // Computed figures are dropped: the server recomputes every one of them
      // at the rates in force on the document's date.
      expect(req.lines[0]).not.toHaveProperty('lineTotal');
      expect(req.lines[0]).not.toHaveProperty('taxAmount');
    });

    it('SOR-T1-05: a refusal keeps the server’s own words instead of a generic message', async () => {
      mockSalesOrderService.create.mockRejectedValueOnce(
        new HttpErrorResponse({
          status: 409,
          error: { message: 'There is not enough stock available to reserve: C7 - Name 7.' },
        }),
      );

      const comp = build();
      comp.ngOnInit();
      comp.form.patchValue({ contactId: 5 });
      comp.onLinesChange([sampleLine]);

      await comp.save();

      expect(comp.messages()).toEqual([
        {
          tone: 'error',
          text: 'There is not enough stock available to reserve: C7 - Name 7.',
          detail: [],
        },
      ]);

      expect(mockRouter.navigate).not.toHaveBeenCalled();
    });

    it('SOR-T1-06: an order with no line is refused before it reaches the server', async () => {
      const comp = build();
      comp.ngOnInit();
      comp.form.patchValue({ contactId: 5 });
      comp.onLinesChange([]);

      await comp.save();

      expect(mockSalesOrderService.create).not.toHaveBeenCalled();
      expect(comp.messages()[0].tone).toBe('error');
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
    const newChallan = () =>
      TestBed.runInInjectionContext(
        () => new DeliveryChallanFormComponent(),
      ) as unknown as DeliveryChallanFormHarness;

    /** A confirmed order of 10, 4 already delivered, on order line 901. */
    const confirmedOrder = {
      salesOrderId: 31,
      documentNo: 'SO/2026/0031',
      status: 'Posted',
      fulfilmentStatus: 'PartlyDelivered',
      contactId: 5,
      contactGstin: '33AAAAA0000A1Z5',
      currencyCode: 'INR',
      exchangeRate: 1,
      billingAddress: 'Chennai',
      shippingAddress: 'Madurai',
      lines: [
        {
          salesOrderDetailId: 901,
          lineNumber: 1,
          itemId: 10,
          itemLabel: 'Gold Bar 24K',
          quantity: 10,
          conversionFactor: 1,
          deliveredQuantity: 4,
          reservedQuantity: 6,
          unitPrice: 750,
          discountPercent: 0,
          taxGroupId: 3,
          lineType: 'Stock',
          taxes: []
        },
        {
          // Fully delivered already — nothing left for this challan to carry.
          salesOrderDetailId: 902,
          lineNumber: 2,
          itemId: 11,
          quantity: 2,
          conversionFactor: 1,
          deliveredQuantity: 2,
          reservedQuantity: 0,
          unitPrice: 100,
          lineType: 'Stock',
          taxes: []
        }
      ]
    };

    it('DLC-T1-01: Form initializes as a new sale challan dated today', () => {
      const comp = newChallan();
      comp.ngOnInit();

      expect(comp.isEdit()).toBe(false);
      expect(comp.challanId()).toBeNull();
      expect(comp.form.get('challanType')?.value).toBe(ChallanType.Sale);
      expect(comp.form.get('dispatchDate')?.value).toBeTruthy();
      expect(comp.form.get('currencyCode')?.value).toBe('INR');
      expect(comp.form.get('salesOrderId')?.value).toBeNull();
    });

    it('DLC-T1-02: The challan types are the server enum, value for value', () => {
      const comp = newChallan();

      // "Transfer" used to be sent as 2, which the server reads as Approval.
      expect(comp.challanTypes.map((t) => t.value)).toEqual([0, 1, 2, 3, 4]);
      expect(comp.challanTypes.find((t) => t.value === ChallanType.BranchTransfer)?.label)
        .toBe('Branch transfer');
    });

    it('DLC-T1-03: Create sends the transport fields and opens the new challan', async () => {
      const comp = newChallan();
      comp.ngOnInit();
      comp.form.patchValue({
        documentDate: '2026-08-18',
        contactId: 5,
        challanType: ChallanType.Approval,
        vehicleNo: 'KA-04-E-5678',
        transporterName: 'Swift Logistics',
        ewayBillNo: '123456789012',
        ewayBillDate: '2026-08-18',
        dispatchDate: '2026-08-18',
        currencyCode: 'INR',
        exchangeRate: 1,
        notes: 'Supply on approval'
      });
      comp.onLinesChange([{ ...sampleLine, taxGroupId: 3 }]);
      expect(comp.totals().subTotal).toBe(75000);
      expect(comp.totals().totalAmount).toBe(77250);

      await comp.save();

      expect(mockDeliveryChallanService.create).toHaveBeenCalledTimes(1);
      const req: SaveDeliveryChallanRequest = mockDeliveryChallanService.create.mock.calls[0][0];
      expect(req.documentDate).toBe('2026-08-18');
      expect(req.contactId).toBe(5);
      expect(req.challanType).toBe(ChallanType.Approval);
      expect(req.vehicleNo).toBe('KA-04-E-5678');
      expect(req.transporterName).toBe('Swift Logistics');
      expect(req.ewayBillNo).toBe('123456789012');
      expect(req.ewayBillDate).toBe('2026-08-18');
      expect(req.notes).toBe('Supply on approval');
      expect(req.salesOrderId).toBeUndefined();

      // The tax group goes with the line: it was dropped, so every challan saved
      // from this screen carried no tax at all.
      expect(req.lines[0].taxGroupId).toBe(3);
      expect(req.lines[0].itemId).toBe(10);
      expect(req.lines[0].salesOrderDetailId).toBeUndefined();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/sales/delivery-challans', 61]);
    });

    it('DLC-T1-04: Edit loads the challan by id and updates it', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('61');
      const comp = newChallan();
      comp.ngOnInit();
      await comp.load();

      expect(comp.isEdit()).toBe(true);
      expect(comp.challanId()).toBe(61);
      expect(mockDeliveryChallanService.get).toHaveBeenCalledWith(61);
      expect(comp.form.get('vehicleNo')?.value).toBe('KA-01-AB-1234');

      comp.onLinesChange([sampleLine]);
      await comp.save();

      expect(mockDeliveryChallanService.update).toHaveBeenCalledWith(61, expect.any(Object));
    });

    it('DLC-T1-05: Load order fills the customer and one line per order line still outstanding', async () => {
      mockSalesOrderService.get.mockResolvedValueOnce(confirmedOrder);
      const comp = newChallan();
      comp.ngOnInit();
      comp.form.patchValue({ salesOrderId: 31 });

      await comp.loadOrder();

      expect(mockSalesOrderService.get).toHaveBeenCalledWith(31);
      expect(comp.form.get('contactId')?.value).toBe(5);
      expect(comp.form.get('contactGstin')?.value).toBe('33AAAAA0000A1Z5');
      expect(comp.form.get('shippingAddress')?.value).toBe('Madurai');

      // The fully delivered line is left out; the other carries what is left.
      const lines = comp.lines();
      expect(lines).toHaveLength(1);
      expect(lines[0].salesOrderDetailId).toBe(901);
      expect(lines[0].itemId).toBe(10);

      await comp.save();

      const req: SaveDeliveryChallanRequest = mockDeliveryChallanService.create.mock.calls[0][0];
      expect(req.salesOrderId).toBe(31);
      expect(req.lines).toHaveLength(1);
      expect(req.lines[0].salesOrderDetailId).toBe(901);
      expect(req.lines[0].quantity).toBe(6);
      expect(req.lines[0].taxGroupId).toBe(3);
    });

    it('DLC-T1-06: Load order refuses an order that is not confirmed', async () => {
      mockSalesOrderService.get.mockResolvedValueOnce({ ...confirmedOrder, status: 'Draft' });
      const comp = newChallan();
      comp.form.patchValue({ salesOrderId: 31 });

      await comp.loadOrder();

      expect(comp.messages()[0]?.tone).toBe('error');
      expect(comp.form.get('contactId')?.value).toBe(0);
    });

    it('DLC-T1-07: Post dispatches the challan and reloads it', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('61');
      const comp = newChallan();
      comp.ngOnInit();
      await comp.load();

      expect(comp.canPost()).toBe(true);

      mockDeliveryChallanService.get.mockResolvedValueOnce({
        deliveryChallanId: 61,
        documentNo: 'DC/2026/0061',
        documentDate: '2026-08-18',
        dispatchDate: '2026-08-18',
        contactId: 5,
        status: 'Posted',
        challanType: 0,
        currencyCode: 'INR',
        exchangeRate: 1,
        lines: []
      });

      await comp.post();

      expect(mockDeliveryChallanService.post).toHaveBeenCalledWith(61);
      expect(comp.status()).toBe('Posted');
      expect(comp.messages()[0]?.tone).toBe('success');

      // Dispatched: read-only, and answered with a return rather than a void.
      expect(comp.editable()).toBe(false);
      expect(comp.canPost()).toBe(false);
      expect(comp.canVoid()).toBe(false);
      expect(comp.form.disabled).toBe(true);
    });

    it('DLC-T1-08: A void needs a reason before it reaches the server', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('61');
      const comp = newChallan();
      comp.ngOnInit();
      await comp.load();

      expect(comp.canVoid()).toBe(true);

      await comp.voidChallan();
      expect(mockDeliveryChallanService.voidChallan).not.toHaveBeenCalled();

      comp.voidForm.patchValue({ reason: '  Keyed twice  ' });
      await comp.voidChallan();

      expect(mockDeliveryChallanService.voidChallan).toHaveBeenCalledWith(61, { reason: 'Keyed twice' });
    });

    it('DLC-T1-09: A refused post shows the server\'s own words', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('61');
      mockDeliveryChallanService.post.mockRejectedValueOnce(
        new HttpErrorResponse({
          status: 409,
          error: { message: 'Order line 1 has 3 left to deliver, and this challan delivers 4.' }
        })
      );
      const comp = newChallan();
      comp.ngOnInit();
      await comp.load();

      await comp.post();

      expect(comp.messages()[0]?.tone).toBe('error');
      expect(comp.messages()[0]?.text).toContain('left to deliver');
      expect(comp.status()).toBe('Draft');
    });
  });

  // =========================================================================
  // SECTION 5: BOUNDARY & VALIDATION EDGE CASES ACROSS ALL 4 FORMS
  // =========================================================================
  describe('5. Boundary Validation & Error Resilience', () => {
    it('VAL-T2-01: Invalid forms prevent submission across Quote, SalesOrder, CreditNote, DeliveryChallan', async () => {
      const qot = TestBed.runInInjectionContext(() => new QuoteFormComponent());
      const sor = TestBed.runInInjectionContext(
        () => new SalesOrderFormComponent(),
      ) as unknown as SalesOrderFormHarness;
      const crn = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      const dlc = TestBed.runInInjectionContext(
        () => new DeliveryChallanFormComponent(),
      ) as unknown as DeliveryChallanFormHarness;

      qot.form.patchValue({ documentDate: '', validUntil: '' });
      sor.form.patchValue({ documentDate: '', contactId: 0 });
      crn.form.patchValue({ documentDate: '', invoiceId: '' });
      dlc.form.patchValue({ documentDate: '', dispatchDate: '' });

      qot.save();
      await sor.save();
      crn.save();
      await dlc.save();

      expect(mockQuoteService.create).not.toHaveBeenCalled();
      expect(mockSalesOrderService.create).not.toHaveBeenCalled();
      expect(mockCreditNoteService.save).not.toHaveBeenCalled();
      expect(mockDeliveryChallanService.create).not.toHaveBeenCalled();
    });
  });
});
