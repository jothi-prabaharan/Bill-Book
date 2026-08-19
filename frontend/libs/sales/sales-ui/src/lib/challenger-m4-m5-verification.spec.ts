import { TestBed } from '@angular/core/testing';
import { Router, ActivatedRoute, NavigationEnd } from '@angular/router';
import { FormBuilder } from '@angular/forms';
import { of } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { ElementRef } from '@angular/core';

// Shell & UI components
import { ShellNavComponent, ShellTopbarComponent, ShellBreadcrumbComponent } from '@bill-book/app-shell';
import { AuthService, type AccessibleOrg } from '@bill-book/auth';
import {
  DocumentLine,
  DocumentLineContext,
  totalsOf,
  recalculate,
} from '@bill-book/ui-components';

// Sales UI & Core
import { SalesListComponent } from './sales-list/sales-list.component';
import { QuoteFormComponent } from './quote-form/quote-form.component';
import { SalesOrderFormComponent } from './sales-order-form/sales-order-form.component';
import { CreditNoteFormComponent } from './credit-note-form/credit-note-form.component';
import { DeliveryChallanFormComponent } from './delivery-challan-form/delivery-challan-form.component';
import {
  TransactionService,
  InvoiceService,
  QuoteService,
  SalesOrderService,
  CreditNoteService,
  DeliveryChallanService,
  LedgerService,
  type SalesTransactionListItem,
} from '@bill-book/sales-core';

const QTY_SCALE = 1_000_000;

describe('Empirical Challenger Suite: Milestone 4, 5 & Final Verification', () => {
  let mockRouter: Partial<Router>;
  let mockActivatedRoute: {
    snapshot: {
      paramMap: {
        get: ReturnType<typeof vi.fn>;
      };
    };
  };
  let mockAuthService: {
    canView: ReturnType<typeof vi.fn>;
    accessibleOrganizations: ReturnType<typeof vi.fn>;
    switchOrganization: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let mockTransactionService: { list: ReturnType<typeof vi.fn> };
  let mockInvoiceService: {
    get: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    post: ReturnType<typeof vi.fn>;
    voidInvoice: ReturnType<typeof vi.fn>;
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
  let mockDeliveryChallanService: {
    get: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
  };
  let mockLedgerService: { outstandingBalances: ReturnType<typeof vi.fn> };
  let mockNativeElement: HTMLElement;
  let mockElementRef: ElementRef;

  const sampleOrgs: AccessibleOrg[] = [
    { orgId: 'org-1', orgName: 'Bangalore Flagship Store', roleName: 'Owner' },
    { orgId: 'org-2', orgName: 'Chennai Hub', roleName: 'Billing Clerk' },
    { orgId: 'org-3', orgName: 'Mumbai Regional Depot', roleName: 'Accountant' },
  ];

  const sampleLine: DocumentLine = {
    detailId: 1,
    lineNumber: 1,
    itemId: 101,
    itemLabel: 'Diamond Solitaire Ring',
    hsnSacCode: '7113',
    description: '18K White Gold Solitaire',
    warehouseId: 1,
    quantity: 1 * QTY_SCALE,
    uomId: 1,
    uomLabel: 'PCS',
    conversionFactor: QTY_SCALE,
    baseQuantity: 1 * QTY_SCALE,
    unitPrice: 5000000, // 50,000.00 INR (paise)
    isPriceInclusive: false,
    discountPercent: 10,
    discountAmount: 500000,
    grossAmount: 5000000,
    taxableAmount: 4500000,
    taxTreatment: 'Taxable',
    taxMasterId: 1,
    taxGroupId: null,
    taxAmount: 135000, // 3% GST on jewellery (1.5% CGST + 1.5% SGST)
    taxes: [
      { component: 'Cgst', subAccountId: 0, rate: 15000, taxableAmount: 4500000, amount: 67500 },
      { component: 'Sgst', subAccountId: 0, rate: 15000, taxableAmount: 4500000, amount: 67500 },
    ],
    lineType: 'Stock',
    accountId: 201,
    fixedAssetCategoryId: null,
    lineTotal: 4635000,
    itemBatchId: null,
    lineNotes: null,
  };

  beforeEach(() => {
    mockRouter = {
      url: '/sales',
      events: of(new NavigationEnd(1, '/sales', '/sales')),
      navigate: vi.fn().mockResolvedValue(true),
      navigateByUrl: vi.fn().mockResolvedValue(true),
    };

    mockActivatedRoute = {
      snapshot: {
        paramMap: {
          get: vi.fn().mockReturnValue(null),
        },
      },
    };

    mockAuthService = {
      canView: vi.fn().mockReturnValue(true),
      accessibleOrganizations: vi.fn().mockResolvedValue(sampleOrgs),
      switchOrganization: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn(),
    };

    mockTransactionService = {
      list: vi.fn().mockReturnValue(of([])),
    };

    mockInvoiceService = {
      get: vi.fn().mockReturnValue(of({ invoiceId: 1, lines: [] })),
      create: vi.fn().mockReturnValue(of({ invoiceId: 1 })),
      update: vi.fn().mockReturnValue(of({ invoiceId: 1 })),
      post: vi.fn().mockReturnValue(of({ success: true })),
      voidInvoice: vi.fn().mockReturnValue(of({ success: true })),
    };

    mockQuoteService = {
      get: vi.fn().mockReturnValue(of({ quoteId: 1, lines: [] })),
      create: vi.fn().mockReturnValue(of({ quoteId: 1 })),
      update: vi.fn().mockReturnValue(of({ quoteId: 1 })),
    };

    mockSalesOrderService = {
      get: vi.fn().mockReturnValue(of({ salesOrderId: 1, lines: [] })),
      create: vi.fn().mockReturnValue(of({ salesOrderId: 1 })),
      update: vi.fn().mockReturnValue(of({ salesOrderId: 1 })),
    };

    mockCreditNoteService = {
      get: vi.fn().mockReturnValue(of({ creditNoteId: 1, lines: [] })),
      save: vi.fn().mockReturnValue(of({ creditNoteId: 1 })),
    };

    mockDeliveryChallanService = {
      get: vi.fn().mockReturnValue(of({ deliveryChallanId: 1, lines: [] })),
      create: vi.fn().mockReturnValue(of({ deliveryChallanId: 1 })),
      update: vi.fn().mockReturnValue(of({ deliveryChallanId: 1 })),
    };

    mockLedgerService = {
      outstandingBalances: vi.fn().mockReturnValue(of([])),
    };

    mockNativeElement = document.createElement('div');
    const orgContainer = document.createElement('div');
    orgContainer.className = 'org-dropdown-container';
    mockNativeElement.appendChild(orgContainer);
    mockElementRef = new ElementRef(mockNativeElement);

    TestBed.configureTestingModule({
      providers: [
        FormBuilder,
        { provide: Router, useValue: mockRouter },
        { provide: ActivatedRoute, useValue: mockActivatedRoute },
        { provide: AuthService, useValue: mockAuthService },
        { provide: ElementRef, useValue: mockElementRef },
        { provide: TransactionService, useValue: mockTransactionService },
        { provide: InvoiceService, useValue: mockInvoiceService },
        { provide: QuoteService, useValue: mockQuoteService },
        { provide: SalesOrderService, useValue: mockSalesOrderService },
        { provide: CreditNoteService, useValue: mockCreditNoteService },
        { provide: DeliveryChallanService, useValue: mockDeliveryChallanService },
        { provide: LedgerService, useValue: mockLedgerService },
      ],
    });
  });

  // =========================================================================
  // 1. SHELL GRID LAYOUT & Z-INDEX LAYER STACKING EMPIRICAL AUDIT
  // =========================================================================
  describe('1. Shell Layout, Rail Active Cutout, and Chrome Z-Index Stacking', () => {
    it('CHAL-SHELL-01: Verifies CSS Grid layout contracts (56px rail, 46px topbar, 100dvh viewport)', () => {
      const shellScss = readFileSync(
        resolve(__dirname, '../../../../app-shell/src/lib/shell/shell.component.scss'),
        'utf-8'
      );

      expect(shellScss).toContain('grid-template-columns: 56px 1fr;');
      expect(shellScss).toContain('grid-template-rows: 46px auto 1fr;');
      expect(shellScss).toContain('height: 100dvh;');
      expect(shellScss).toContain('width: 100vw;');
      expect(shellScss).toContain('overflow: hidden;');
      expect(shellScss).toContain('.shell-content-cell');
      expect(shellScss).toContain('overflow-y: auto;');
    });

    it('CHAL-SHELL-02: Verifies strict z-index layering to guarantee zero chrome overlap', () => {
      const shellScss = readFileSync(
        resolve(__dirname, '../../../../app-shell/src/lib/shell/shell.component.scss'),
        'utf-8'
      );
      const tableScss = readFileSync(
        resolve(__dirname, '../../../../shared/theming/src/lib/_table.scss'),
        'utf-8'
      );

      // Top Bar (6) > Nav Rail (5) > Breadcrumbs (4) > Sticky Table Header (3) > Scrolling Content (1)
      expect(shellScss).toMatch(/\.shell-topbar-cell[\s\S]*?z-index:\s*var\(--z-topbar\)/);
      expect(shellScss).toMatch(/\.shell-nav-cell[\s\S]*?z-index:\s*var\(--z-rail\)/);
      expect(shellScss).toMatch(/\.shell-breadcrumb-cell[\s\S]*?z-index:\s*var\(--z-breadcrumb\)/);
      expect(tableScss).toMatch(/\.listwrap \.table thead th[\s\S]*?z-index:\s*var\(--z-table-head\)/);
      expect(shellScss).toMatch(/\.shell-content-cell[\s\S]*?z-index:\s*var\(--z-content\)/);
    });

    it('CHAL-SHELL-03: Verifies Left Nav Rail active item cutout rule with 4px left accent rule', () => {
      const navScss = readFileSync(
        resolve(__dirname, '../../../../app-shell/src/lib/nav/shell-nav.component.scss'),
        'utf-8'
      );

      expect(navScss).toContain('background: var(--color-ink);');
      expect(navScss).toContain('&.active');
      expect(navScss).toContain('var(--shadow-rail-active)');
      expect(navScss).toContain('background: var(--color-bg);');
    });

    it('CHAL-SHELL-04: Verifies Topbar organization searchable dropdown and outside dismissal', async () => {
      const topbar = TestBed.runInInjectionContext(() => new ShellTopbarComponent());
      topbar.allOrgs.set(sampleOrgs);

      // Initial state
      expect(topbar.orgOpen()).toBe(false);
      topbar.toggleOrg();
      expect(topbar.orgOpen()).toBe(true);

      // Filter query
      topbar.setOrgQuery('Chennai');
      expect(topbar.filteredOrgs().length).toBe(1);
      expect(topbar.filteredOrgs()[0].orgId).toBe('org-2');

      // Pick org
      await topbar.pickOrg('org-2');
      expect(mockAuthService.switchOrganization).toHaveBeenCalledWith('org-2');
      expect(topbar.orgOpen()).toBe(false);

      // Escape key closes
      topbar.toggleOrg();
      expect(topbar.orgOpen()).toBe(true);
      topbar.onEscape();
      expect(topbar.orgOpen()).toBe(false);
    });

    it('CHAL-SHELL-05: Verifies Breadcrumb path resolution, replacement of <h1>, and actions slot', () => {
      const breadcrumb = TestBed.runInInjectionContext(() => new ShellBreadcrumbComponent());
      breadcrumb.updateCrumbs('/sales/invoices/new');

      const crumbs = breadcrumb.crumbs();
      expect(crumbs.length).toBe(3);
      expect(crumbs[0]).toEqual({ label: 'Sales', path: '/sales', isLink: true, isLast: false });
      expect(crumbs[1]).toEqual({ label: 'Invoices', path: '/sales/invoices', isLink: true, isLast: false });
      expect(crumbs[2]).toEqual({ label: 'New', path: '/sales/invoices/new', isLink: false, isLast: true });

      const breadcrumbHtml = readFileSync(
        resolve(__dirname, '../../../../app-shell/src/lib/breadcrumb/shell-breadcrumb.component.html'),
        'utf-8'
      );
      expect(breadcrumbHtml).toContain('<ng-content select="[bbShellActions], .acts" />');
    });
  });

  // =========================================================================
  // 2. DATA TABLE SCROLLING AT COMPACT DENSITY & STICKY HEADERS
  // =========================================================================
  describe('2. Data Table Compact Density & Sticky Header Stress-Testing', () => {
    it('CHAL-TABLE-01: Verifies compact density minimum row height >= 32px and hairline row rules', () => {
      const tableScss = readFileSync(
        resolve(__dirname, '../../../../shared/theming/src/lib/_table.scss'),
        'utf-8'
      );

      expect(tableScss).toContain('min-height: 32px;');
      expect(tableScss).toContain('border-bottom: 1px solid var(--color-divider);');
    });

    it('CHAL-TABLE-02: Verifies sticky table header with inset bottom shadow rule', () => {
      const tableScss = readFileSync(
        resolve(__dirname, '../../../../shared/theming/src/lib/_table.scss'),
        'utf-8'
      );

      expect(tableScss).toContain('.listwrap .table thead th');
      expect(tableScss).toContain('position: sticky;');
      expect(tableScss).toContain('top: 0;');
      expect(tableScss).toContain('z-index: var(--z-table-head);');
      expect(tableScss).toContain('background: var(--color-bg);');
      expect(tableScss).toContain('box-shadow: var(--shadow-table-head);');
    });

    it('CHAL-TABLE-03: Verifies tabular numerals and right-alignment applied to numeric columns', () => {
      const tableScss = readFileSync(
        resolve(__dirname, '../../../../shared/theming/src/lib/_table.scss'),
        'utf-8'
      );

      expect(tableScss).toContain('.table th.numeric');
      expect(tableScss).toContain('.table td.numeric');
      expect(tableScss).toContain('text-align: right;');
      expect(tableScss).toContain('font-variant-numeric: tabular-nums;');
      expect(tableScss).toContain('font-feature-settings: "tnum";');
    });
  });

  // =========================================================================
  // 3. SALES LIST FILTERING, DOCUMENT SWITCHING & REACTIVE FORMS
  // =========================================================================
  describe('3. Sales Module List Filtering, Document Switching & Reactive Forms (totalsOf)', () => {
    it('CHAL-SALES-01: SalesListComponent filters by transaction type and navigates accurately', () => {
      const comp = TestBed.runInInjectionContext(() => new SalesListComponent());
      comp.ngOnInit();

      expect(mockTransactionService.list).toHaveBeenCalledWith('');

      comp.setType('Invoice');
      expect(mockTransactionService.list).toHaveBeenCalledWith('Invoice');

      comp.setType('DeliveryChallan');
      expect(mockTransactionService.list).toHaveBeenCalledWith('DeliveryChallan');

      // Test route resolver across all 5 transaction types
      const makeItem = (id: number, type: string): SalesTransactionListItem => ({
        transactionId: id,
        transactionType: type,
        documentNo: `DOC-${id}`,
        documentDate: '2026-08-19',
        contactId: 1,
        contactName: 'Test Contact',
        totalAmount: 100000,
        status: 'Draft',
      });

      expect(comp.getRouteForTransaction(makeItem(10, 'Quote'))).toBe('/sales/quotes/10');
      expect(comp.getRouteForTransaction(makeItem(20, 'SalesOrder'))).toBe('/sales/sales-orders/20');
      expect(comp.getRouteForTransaction(makeItem(30, 'Invoice'))).toBe('/sales/invoices/30');
      expect(comp.getRouteForTransaction(makeItem(40, 'DeliveryChallan'))).toBe('/sales/delivery-challans/40');
      expect(comp.getRouteForTransaction(makeItem(50, 'CreditNote'))).toBe('/sales/credit-notes/50');
    });

    it('CHAL-SALES-02: totalsOf mathematical precision stress-test (paise calculations & GST split)', () => {
      const ctx: DocumentLineContext = {
        isInterState: false,
        allowFreeTextLines: true,
        discountBeforeTax: true,
        discountLevel: 'Line',
        readonly: false,
        currencyDecimals: 2,
      };

      // Item 1: 500.00 with 10% discount + 18% GST (Intra-state: 9% CGST + 9% SGST)
      const line1 = recalculate(
        {
          ...sampleLine,
          quantity: 1 * QTY_SCALE,
          unitPrice: 50000, // 500.00
          discountPercent: 10,
          taxes: [
            { component: 'Cgst', subAccountId: 0, rate: 90000, taxableAmount: 0, amount: 0 },
            { component: 'Sgst', subAccountId: 0, rate: 90000, taxableAmount: 0, amount: 0 },
          ],
        },
        ctx
      );

      // Item 2: 1,000.00 with 5% discount + 18% GST
      const line2 = recalculate(
        {
          ...sampleLine,
          lineNumber: 2,
          quantity: 1 * QTY_SCALE,
          unitPrice: 100000, // 1000.00
          discountPercent: 5,
          taxes: [
            { component: 'Cgst', subAccountId: 0, rate: 90000, taxableAmount: 0, amount: 0 },
            { component: 'Sgst', subAccountId: 0, rate: 90000, taxableAmount: 0, amount: 0 },
          ],
        },
        ctx
      );

      const totals = totalsOf([line1, line2]);

      expect(totals.subTotal).toBe(150000); // 500 + 1000 = 1500.00
      expect(totals.discountAmount).toBe(10000); // 50 + 50 = 100.00
      expect(totals.taxableAmount).toBe(140000); // 450 + 950 = 1400.00
      expect(totals.cgstAmount).toBe(12600); // 9% of 1400 = 126.00
      expect(totals.sgstAmount).toBe(12600); // 9% of 1400 = 126.00
      expect(totals.igstAmount).toBe(0);
      expect(totals.totalAmount).toBe(165200); // 1400 + 252 = 1652.00
    });

    it('CHAL-SALES-03: totalsOf handles MRP-inclusive taxes backed out correctly', () => {
      const ctx: DocumentLineContext = {
        isInterState: false,
        allowFreeTextLines: true,
        discountBeforeTax: true,
        discountLevel: 'Line',
        readonly: false,
        currencyDecimals: 2,
      };

      // 118.00 MRP-inclusive of 18% tax -> 100.00 taxable + 9.00 CGST + 9.00 SGST
      const inclusiveLine = recalculate(
        {
          ...sampleLine,
          quantity: 1 * QTY_SCALE,
          unitPrice: 11800, // 118.00
          isPriceInclusive: true,
          discountPercent: null,
          discountAmount: 0,
          taxes: [
            { component: 'Cgst', subAccountId: 0, rate: 90000, taxableAmount: 0, amount: 0 },
            { component: 'Sgst', subAccountId: 0, rate: 90000, taxableAmount: 0, amount: 0 },
          ],
        },
        ctx
      );

      expect(inclusiveLine.grossAmount).toBe(11800);
      expect(inclusiveLine.taxableAmount).toBe(10000);
      expect(inclusiveLine.taxAmount).toBe(1800);
      expect(inclusiveLine.lineTotal).toBe(11800);

      const totals = totalsOf([inclusiveLine]);
      expect(totals.taxableAmount).toBe(10000);
      expect(totals.cgstAmount).toBe(900);
      expect(totals.sgstAmount).toBe(900);
      expect(totals.totalAmount).toBe(11800);
    });

    it('CHAL-SALES-04: totalsOf handles inter-state (IGST) supply', () => {
      const ctx: DocumentLineContext = {
        isInterState: true,
        allowFreeTextLines: true,
        discountBeforeTax: true,
        discountLevel: 'Line',
        readonly: false,
        currencyDecimals: 2,
      };

      const igstLine = recalculate(
        {
          ...sampleLine,
          quantity: 2 * QTY_SCALE,
          unitPrice: 100000, // 1000.00
          discountPercent: 0,
          taxes: [
            { component: 'Igst', subAccountId: 0, rate: 180000, taxableAmount: 0, amount: 0 },
          ],
        },
        ctx
      );

      const totals = totalsOf([igstLine]);
      expect(totals.subTotal).toBe(200000);
      expect(totals.taxableAmount).toBe(200000);
      expect(totals.cgstAmount).toBe(0);
      expect(totals.sgstAmount).toBe(0);
      expect(totals.igstAmount).toBe(36000);
      expect(totals.totalAmount).toBe(236000);
    });

    it('CHAL-SALES-05: Secondary sales forms (Quote, SalesOrder, CreditNote, DeliveryChallan) bind totals property and compute live', () => {
      const quote = TestBed.runInInjectionContext(() => new QuoteFormComponent());
      const salesOrder = TestBed.runInInjectionContext(() => new SalesOrderFormComponent());
      const creditNote = TestBed.runInInjectionContext(() => new CreditNoteFormComponent());
      const deliveryChallan = TestBed.runInInjectionContext(() => new DeliveryChallanFormComponent());

      quote.onLinesChange([sampleLine]);
      salesOrder.onLinesChange([sampleLine]);
      creditNote.onLinesChange([sampleLine]);
      deliveryChallan.onLinesChange([sampleLine]);

      expect(quote.totals.subTotal).toBe(5000000);
      expect(quote.totals.totalAmount).toBe(4635000);

      expect(salesOrder.totals.subTotal).toBe(5000000);
      expect(salesOrder.totals.totalAmount).toBe(4635000);

      expect(creditNote.totals.subTotal).toBe(5000000);
      expect(creditNote.totals.totalAmount).toBe(4635000);

      expect(deliveryChallan.totals.subTotal).toBe(5000000);
      expect(deliveryChallan.totals.totalAmount).toBe(4635000);
    });
  });

  // =========================================================================
  // 4. PURE CSS INTERACTION STATES (NO JS ANIMATION/HOVER CODE)
  // =========================================================================
  describe('4. Pure CSS Interaction States Verification', () => {
    it('CHAL-CSS-01: Verifies absence of JS-driven mouseover/mouseenter/mouseleave handlers in template files', () => {
      const libsDir = resolve(__dirname, '../../../../');

      const findHtmlFiles = (dir: string): string[] => {
        let results: string[] = [];
        const list = readdirSync(dir);
        for (const file of list) {
          const fullPath = join(dir, file);
          const stat = statSync(fullPath);
          if (stat.isDirectory()) {
            results = results.concat(findHtmlFiles(fullPath));
          } else if (file.endsWith('.html')) {
            results.push(fullPath);
          }
        }
        return results;
      };

      const htmlFiles = findHtmlFiles(libsDir);
      for (const file of htmlFiles) {
        const content = readFileSync(file, 'utf-8');
        expect(
          content.includes('(mouseenter)'),
          `Forbidden (mouseenter) JS event found in ${file}`
        ).toBe(false);
        expect(
          content.includes('(mouseleave)'),
          `Forbidden (mouseleave) JS event found in ${file}`
        ).toBe(false);
        expect(
          content.includes('(mouseover)'),
          `Forbidden (mouseover) JS event found in ${file}`
        ).toBe(false);
        expect(
          content.includes('(mouseout)'),
          `Forbidden (mouseout) JS event found in ${file}`
        ).toBe(false);
      }
    });

    it('CHAL-CSS-02: Verifies pure CSS pseudo-classes (:hover, :focus-visible, @keyframes) used for interactions', () => {
      const buttonScss = readFileSync(
        resolve(__dirname, '../../../../shared/theming/src/lib/_buttons.scss'),
        'utf-8'
      );
      const tokenScss = readFileSync(
        resolve(__dirname, '../../../../shared/theming/src/lib/_tokens.scss'),
        'utf-8'
      );
      const formScss = readFileSync(
        resolve(__dirname, '../../../../shared/theming/src/lib/_forms.scss'),
        'utf-8'
      );

      // Buttons use CSS :hover and :active
      expect(buttonScss).toContain('&:hover');
      expect(buttonScss).toContain('.btn:active');

      // Tokens and Forms provide themed :focus-visible outline
      expect(tokenScss).toContain(':focus-visible');
      expect(tokenScss).toContain('outline: 2px solid var(--color-accent)');
      expect(formScss).toContain('&:focus-visible');
    });
  });

  // =========================================================================
  // 5. STRICT FORENSIC UI STRING AUDIT ("Accounts" vs "Accounting")
  // =========================================================================
  describe('5. UI Forensic Audit: Zero User-Visible "Accounting" Strings', () => {
    it('CHAL-AUDIT-01: Verifies all navigation items and breadcrumb labels use "Accounts", never "Accounting"', () => {
      const nav = TestBed.runInInjectionContext(() => new ShellNavComponent());
      const accountsItem = nav.allNavItems.find((i) => i.path === '/accounting');
      expect(accountsItem).toBeDefined();
      expect(accountsItem?.label).toBe('Accounts');
      expect(accountsItem?.label.toLowerCase()).not.toContain('accounting');

      const crumb = TestBed.runInInjectionContext(() => new ShellBreadcrumbComponent());
      crumb.updateCrumbs('/accounting');
      expect(crumb.crumbs()[0].label).toBe('Accounts');
    });

    it('CHAL-AUDIT-02: Forensic scan of all HTML files across frontend/ to verify zero user-visible "Accounting"', () => {
      const frontendDir = resolve(__dirname, '../../../../..');

      const getHtmlFiles = (dir: string): string[] => {
        let results: string[] = [];
        const list = readdirSync(dir);
        for (const file of list) {
          if (file === 'node_modules' || file === 'dist' || file === '.angular') continue;
          const fullPath = join(dir, file);
          const stat = statSync(fullPath);
          if (stat.isDirectory()) {
            results = results.concat(getHtmlFiles(fullPath));
          } else if (file.endsWith('.html')) {
            results.push(fullPath);
          }
        }
        return results;
      };

      const htmlFiles = getHtmlFiles(frontendDir);
      expect(htmlFiles.length).toBeGreaterThan(10);

      for (const file of htmlFiles) {
        const rawContent = readFileSync(file, 'utf-8');
        // Clean out route URLs like routerLink="/accounting/..." and angular control flow @case ('accounting')
        const cleanContent = rawContent
          .replace(/routerLink="\/accounting[^"]*"/g, '')
          .replace(/\[routerLink\]="\[?'\/accounting[^'\]]*'\]?"/g, '')
          .replace(/@case\s*\(\s*['"]accounting['"]\s*\)/g, '');

        const userVisibleMatch = cleanContent.match(/>[^<]*\bAccounting\b[^<]*</i) ||
                                 cleanContent.match(/(?:title|aria-label|placeholder)=["'][^"']*\bAccounting\b[^"']*["']/i);

        expect(
          userVisibleMatch,
          `Found forbidden user-facing "Accounting" string in template: ${file}`
        ).toBeNull();
      }
    });
  });
});
