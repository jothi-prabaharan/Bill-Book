import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { FormBuilder, FormGroup } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { InvoiceFormComponent } from './invoice-form.component';
import { InvoiceService, InvoiceView, SaveInvoiceRequest } from '@bill-book/sales-core';
import { DocumentLine, UiMessage } from '@bill-book/ui-components';

/**
 * The form's members are `protected` — a template is the only thing that should
 * reach them — so the tests go through a declared shape rather than loosening
 * the component.
 */
interface InvoiceFormHarness {
  ngOnInit(): void;
  isEdit(): boolean;
  invoiceId(): number | null;
  status(): string;
  saving(): boolean;
  form: FormGroup;
  voidForm: FormGroup;
  messages(): UiMessage[];
  lines(): DocumentLine[];
  context(): { readonly: boolean; currencyDecimals: number; isInterState: boolean };
  totals(): {
    subTotal: number;
    discountAmount: number;
    cgstAmount: number;
    sgstAmount: number;
    igstAmount: number;
    totalAmount: number;
  };
  editable(): boolean;
  onLinesChange(lines: readonly DocumentLine[]): void;
  save(): Promise<void>;
  post(): Promise<void>;
  voidInvoice(): Promise<void>;
}

describe('InvoiceFormComponent (sales/sales-ui/invoice-form)', () => {
  let mockInvoiceService: {
    get: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    post: ReturnType<typeof vi.fn>;
    voidInvoice: ReturnType<typeof vi.fn>;
    previewGl: ReturnType<typeof vi.fn>;
    createFromSalesOrder: ReturnType<typeof vi.fn>;
  };
  let mockRouter: Partial<Router>;
  let mockActivatedRoute: {
    snapshot: { paramMap: { get: ReturnType<typeof vi.fn> } };
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
    daysOverdue: 0,
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
        baseQuantity: 1,
        returnedQuantity: 0,
        unitPrice: 10000,
        discountPercent: 5,
        discountAmount: 500,
        grossAmount: 10000,
        taxableAmount: 9500,
        taxTreatment: 'Taxable',
        taxAmount: 1710,
        lineTotal: 11210,
        accountId: 201,
        taxes: [
          {
            invoiceDetailTaxId: 1,
            taxComponent: 'Cgst',
            rate: 9,
            taxableAmount: 9500,
            amount: 855,
            amountBase: 855,
          },
          {
            invoiceDetailTaxId: 2,
            taxComponent: 'Sgst',
            rate: 9,
            taxableAmount: 9500,
            amount: 855,
            amountBase: 855,
          },
        ],
      },
    ],
  };

  /**
   * A line as the grid holds it: integer paise, quantity at six decimals. Two
   * units at ₹50.00 — which is 5000 paise, not ₹5000.
   */
  const sampleLines: DocumentLine[] = [
    {
      detailId: 1,
      lineNumber: 1,
      itemId: 5,
      itemLabel: 'Diamond Ring 18K',
      hsnSacCode: '7113',
      description: '18K Diamond Solitaire Ring',
      warehouseId: null,
      quantity: 2_000_000,
      uomId: null,
      uomLabel: null,
      conversionFactor: 1_000_000,
      baseQuantity: 2_000_000,
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
        { component: 'Sgst', subAccountId: 0, rate: 9, taxableAmount: 9000, amount: 810 },
      ],
      lineType: 'Stock',
      accountId: 201,
      fixedAssetCategoryId: null,
      lineTotal: 10620,
      itemBatchId: null,
      lineNotes: null,
    },
  ];

  beforeEach(() => {
    // Promises, not streams: InvoiceService is awaited now, so that a refusal
    // can be caught and put into the message box with its own words.
    mockInvoiceService = {
      get: vi.fn().mockResolvedValue(sampleInvoiceView),
      create: vi.fn().mockResolvedValue({ invoiceId: 43 }),
      update: vi.fn().mockResolvedValue(undefined),
      post: vi.fn().mockResolvedValue(undefined),
      voidInvoice: vi.fn().mockResolvedValue(undefined),
      previewGl: vi.fn().mockResolvedValue({
        legs: [],
        totalDebit: 0,
        totalCredit: 0,
        isBalanced: true,
      }),
      createFromSalesOrder: vi.fn().mockResolvedValue({ invoiceId: 44 }),
    };

    mockRouter = { navigate: vi.fn().mockResolvedValue(true) };

    mockActivatedRoute = {
      snapshot: { paramMap: { get: vi.fn().mockReturnValue(null) } },
    };

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: InvoiceService, useValue: mockInvoiceService },
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
      ],
    });
  });

  const createComponent = (): InvoiceFormHarness =>
    TestBed.runInInjectionContext(
      () => new InvoiceFormComponent(),
    ) as unknown as InvoiceFormHarness;

  /** Lets ngOnInit's fire-and-forget load settle before assertions. */
  const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

  describe('Tier 1: Feature Coverage', () => {
    it('INV-T1-01: Form initializes with default values in Create mode', () => {
      const comp = createComponent();
      comp.ngOnInit();

      expect(comp.isEdit()).toBe(false);
      expect(comp.invoiceId()).toBeNull();
      expect(comp.status()).toBe('Draft');
      expect(comp.form.get('currencyCode')?.value).toBe('INR');
      expect(comp.form.get('exchangeRate')?.value).toBe(1);
      expect(comp.form.get('documentDate')?.value).toBeTruthy();

      // An invoice needs a due date, so the form offers one rather than
      // letting the server refuse the save.
      expect(comp.form.get('dueDate')?.value).toBeTruthy();

      expect(comp.context().readonly).toBe(false);
      expect(comp.context().currencyDecimals).toBe(2);
    });

    it('INV-T1-02: onLinesChange updates component lines and calculates totals', () => {
      const comp = createComponent();
      comp.onLinesChange(sampleLines);

      expect(comp.lines().length).toBe(1);
      expect(comp.lines()[0].grossAmount).toBe(10000);
      expect(comp.totals().subTotal).toBe(10000);
      expect(comp.totals().discountAmount).toBe(1000);
      expect(comp.totals().cgstAmount).toBe(810);
      expect(comp.totals().sgstAmount).toBe(810);
      expect(comp.totals().totalAmount).toBe(10620);
    });

    it('INV-T1-03: Create invoice sends the DTO the backend takes, in the units it takes', async () => {
      const comp = createComponent();
      comp.ngOnInit();
      comp.form.patchValue({
        documentDate: '2026-08-19',
        dueDate: '2026-09-19',
        contactId: 10,
        currencyCode: 'INR',
        exchangeRate: 1,
        billingAddress: 'Bangalore HQ',
        notes: 'Test Notes',
      });
      comp.onLinesChange(sampleLines);

      await comp.save();

      expect(mockInvoiceService.create).toHaveBeenCalledTimes(1);
      const request: SaveInvoiceRequest = mockInvoiceService.create.mock.calls[0][0];

      expect(request.documentDate).toBe('2026-08-19');
      expect(request.dueDate).toBe('2026-09-19');
      expect(request.contactId).toBe(10);
      expect(request.currencyCode).toBe('INR');
      expect(request.exchangeRate).toBe(1);
      expect(request.billingAddress).toBe('Bangalore HQ');
      expect(request.notes).toBe('Test Notes');
      expect(request.lines.length).toBe(1);

      // The scale boundary. The grid holds 2_000_000 and 5000 paise; the API
      // takes 2 and ₹50. Passed straight through, this line would have been
      // saved at a hundredth of its price and a millionth of its quantity.
      expect(request.lines[0].quantity).toBe(2);
      expect(request.lines[0].unitPrice).toBe(50);
      expect(request.lines[0].itemId).toBe(5);
      expect(request.lines[0].taxTreatment).toBe('Taxable');

      // Computed figures are dropped: the server recomputes every one of them
      // at the rates in force on the document's date.
      expect(request.lines[0]).not.toHaveProperty('lineTotal');
      expect(request.lines[0]).not.toHaveProperty('taxAmount');
    });

    it('INV-T1-04: Edit mode loads invoice by ID and maps lines through the scale boundary', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();
      await settle();

      expect(comp.isEdit()).toBe(true);
      expect(comp.invoiceId()).toBe(42);
      expect(mockInvoiceService.get).toHaveBeenCalledWith(42);
      expect(comp.form.get('documentDate')?.value).toBe('2026-08-18');
      expect(comp.form.get('dueDate')?.value).toBe('2026-09-18');
      expect(comp.form.get('contactId')?.value).toBe(10);
      expect(comp.lines().length).toBe(1);

      // One unit at ₹10,000 arrives as 1_000_000 and 1,000,000 paise.
      expect(comp.lines()[0].quantity).toBe(1_000_000);
      expect(comp.lines()[0].unitPrice).toBe(1_000_000);

      // Computed amounts are left at zero rather than carried across — the grid
      // recalculates them as it renders, so copying the server's in would put
      // two answers on screen for one line.
      expect(comp.lines()[0].lineTotal).toBe(0);
    });

    it('INV-T1-05: Update invoice calls the service with the invoice ID and payload', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();
      await settle();

      comp.onLinesChange(sampleLines);
      await comp.save();

      expect(mockInvoiceService.update).toHaveBeenCalledTimes(1);
      expect(mockInvoiceService.update).toHaveBeenCalledWith(42, expect.any(Object));
    });

    it('INV-T1-06: Posting reloads the invoice, and a posted one is read-only', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();
      await settle();

      // The reload after posting is what tells the screen it is posted — the
      // server decides that, not the button that was clicked.
      mockInvoiceService.get.mockResolvedValue({ ...sampleInvoiceView, status: 'Posted' });

      await comp.post();

      expect(mockInvoiceService.post).toHaveBeenCalledWith(42);
      expect(comp.status()).toBe('Posted');
      expect(comp.editable()).toBe(false);
      expect(comp.context().readonly).toBe(true);
      expect(comp.form.disabled).toBe(true);
      expect(comp.saving()).toBe(false);
    });

    it('INV-T1-07: Voiding takes its reason from the form, not a browser prompt', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();
      await settle();

      comp.voidForm.patchValue({ reason: 'Order cancelled by customer' });
      mockInvoiceService.get.mockResolvedValue({
        ...sampleInvoiceView,
        status: 'Void',
        voidReason: 'Order cancelled by customer',
      });

      await comp.voidInvoice();

      expect(mockInvoiceService.voidInvoice).toHaveBeenCalledWith(42, {
        reason: 'Order cancelled by customer',
      });
      expect(comp.status()).toBe('Void');
      expect(comp.editable()).toBe(false);
      expect(comp.saving()).toBe(false);
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('INV-T2-01: Invalid form prevents submission and aborts the create call', async () => {
      const comp = createComponent();
      comp.ngOnInit();

      comp.form.patchValue({ documentDate: '', exchangeRate: 0 });

      await comp.save();
      expect(mockInvoiceService.create).not.toHaveBeenCalled();
    });

    it('INV-T2-02: A posted invoice loads read-only', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      mockInvoiceService.get.mockResolvedValue({ ...sampleInvoiceView, status: 'Posted' });

      const comp = createComponent();
      comp.ngOnInit();
      await settle();

      expect(comp.status()).toBe('Posted');
      expect(comp.context().readonly).toBe(true);
      expect(comp.form.disabled).toBe(true);
    });

    it('INV-T2-03: Voiding without a reason is refused before the round trip', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();
      await settle();

      // The reason is required, and the field says so rather than the server
      // having to.
      await comp.voidInvoice();

      expect(mockInvoiceService.voidInvoice).not.toHaveBeenCalled();
      expect(comp.status()).toBe('Draft');
      expect(comp.voidForm.get('reason')?.touched).toBe(true);
    });

    it('INV-T2-04: A refusal keeps the server’s own words and clears the saving flag', async () => {
      mockInvoiceService.create.mockRejectedValueOnce(
        new HttpErrorResponse({
          status: 409,
          error: { message: 'There is not enough stock available to issue: C7 - Name 7.' },
        }),
      );

      const comp = createComponent();
      comp.ngOnInit();
      comp.form.patchValue({ contactId: 10 });
      comp.onLinesChange(sampleLines);

      await comp.save();

      expect(comp.saving()).toBe(false);
      expect(comp.messages()).toEqual([
        {
          tone: 'error',
          text: 'There is not enough stock available to issue: C7 - Name 7.',
          detail: [],
        },
      ]);
      expect(mockRouter.navigate).not.toHaveBeenCalled();
    });

    it('INV-T2-05: A refusal on post leaves the invoice as it was and says why', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      mockInvoiceService.post.mockRejectedValueOnce(
        new HttpErrorResponse({
          status: 400,
          error: { message: 'The period this invoice falls in has been closed.' },
        }),
      );

      const comp = createComponent();
      comp.ngOnInit();
      await settle();

      await comp.post();

      expect(comp.saving()).toBe(false);
      expect(comp.status()).toBe('Draft');
      expect(comp.messages()[0].text).toBe('The period this invoice falls in has been closed.');
    });

    it('INV-T2-06: An invoice with no priced line is refused before it reaches the server', async () => {
      const comp = createComponent();
      comp.ngOnInit();
      comp.form.patchValue({ contactId: 10 });
      comp.onLinesChange([]);

      await comp.save();

      expect(mockInvoiceService.create).not.toHaveBeenCalled();
      expect(comp.messages()[0].tone).toBe('error');
    });
  });

  describe('Tier 3: Cross-Feature Interactions', () => {
    it('INV-T3-01: Creating navigates to the new invoice, which now has a number', async () => {
      const comp = createComponent();
      comp.ngOnInit();
      comp.form.patchValue({ contactId: 4 });
      comp.onLinesChange(sampleLines);

      await comp.save();

      expect(mockRouter.navigate).toHaveBeenCalledWith(['/sales/invoices', 43]);
    });

    it('INV-T3-02: An inter-state GSTIN draws the IGST column instead of CGST and SGST', () => {
      const comp = createComponent();
      comp.ngOnInit();

      // 29 is Karnataka; the branch is in 33. The screen only decides which
      // columns are drawn — the document's own IsInterState comes from the
      // server resolving the place of supply.
      comp.form.patchValue({ contactGstin: '29ABCDE1234F1Z5' });
      expect(comp.context().isInterState).toBe(true);

      comp.form.patchValue({ contactGstin: '33ABCDE1234F1Z5' });
      expect(comp.context().isInterState).toBe(false);
    });
  });

  describe('Tier 4: Real-World Sales Lifecycle Workflow', () => {
    it('INV-T4-01: Header, lines, save, then post', async () => {
      mockActivatedRoute.snapshot.paramMap.get.mockReturnValue('42');
      const comp = createComponent();
      comp.ngOnInit();
      await settle();

      comp.form.patchValue({
        documentDate: '2026-08-19',
        dueDate: '2026-09-02',
        contactId: 4,
        billingAddress: '77 Brigade Road, Bangalore',
        notes: 'Thank you for your business!',
      });

      comp.onLinesChange(sampleLines);
      expect(comp.totals().totalAmount).toBe(10620);

      await comp.save();
      expect(mockInvoiceService.update).toHaveBeenCalledTimes(1);

      mockInvoiceService.get.mockResolvedValue({ ...sampleInvoiceView, status: 'Posted' });
      await comp.post();

      expect(mockInvoiceService.post).toHaveBeenCalledWith(42);
      expect(comp.editable()).toBe(false);
    });
  });
});
