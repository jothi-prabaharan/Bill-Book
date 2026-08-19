import { TestBed } from '@angular/core/testing';
import { Router, Event as RouterEvent } from '@angular/router';
import { Subject, of } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { resolve, join } from 'node:path';

import { ShellComponent } from '../shell/shell.component';
import { AuthService } from '@bill-book/auth';
import { ElementRef } from '@angular/core';
import { SalesListComponent } from '@bill-book/sales-ui';
import { TransactionService } from '@bill-book/sales-core';
import { totalsOf, DocumentLine } from '@bill-book/ui-components';

describe('Cross-Module E2E Integration & Layout Architecture Suite', () => {
  let routerEvents$: Subject<RouterEvent>;
  let mockRouter: Partial<Router>;
  let mockAuthService: {
    canView: ReturnType<typeof vi.fn>;
    accessibleOrganizations: ReturnType<typeof vi.fn>;
    switchOrganization: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let mockTransactionService: {
    list: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    routerEvents$ = new Subject<RouterEvent>();
    mockRouter = {
      url: '/dashboard',
      events: routerEvents$.asObservable(),
      navigateByUrl: vi.fn().mockResolvedValue(true),
      navigate: vi.fn().mockResolvedValue(true)
    };

    mockAuthService = {
      canView: vi.fn().mockReturnValue(true),
      accessibleOrganizations: vi.fn().mockResolvedValue([
        { orgId: 'org-1', orgName: 'Main Showroom', roleName: 'Owner' },
        { orgId: 'org-2', orgName: 'Branch 2', roleName: 'Manager' }
      ]),
      switchOrganization: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn()
    };

    mockTransactionService = {
      list: vi.fn().mockReturnValue(of([
        {
          transactionId: 201,
          documentDate: '2026-08-19',
          transactionType: 'Invoice',
          documentNo: 'INV-2026-0001',
          contactName: 'Diamond Plaza',
          totalAmount: 59000,
          status: 'Posted'
        },
        {
          transactionId: 202,
          documentDate: '2026-08-19',
          transactionType: 'Quote',
          documentNo: 'QOT-2026-0001',
          contactName: 'Emerald Jewels',
          totalAmount: 18000,
          status: 'Draft'
        }
      ]))
    };

    const mockNativeElement = document.createElement('div');
    const orgContainer = document.createElement('div');
    orgContainer.className = 'org-dropdown-container';
    mockNativeElement.appendChild(orgContainer);

    localStorage.setItem('bb.orgId', 'org-1');

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: ElementRef, useValue: new ElementRef(mockNativeElement) },
        { provide: TransactionService, useValue: mockTransactionService }
      ]
    });
  });

  // =========================================================================
  // TIER 1: CRITICAL REQUIREMENTS & AUDIT OF FORBIDDEN "Accounting" UI STRING
  // =========================================================================
  describe('Tier 1: Feature Contracts & Forbidden String Audit (R5)', () => {
    it('INT-T1-01: Left rail navigation item for accounting path is strictly labeled "Accounts"', () => {
      const shell = TestBed.runInInjectionContext(() => new ShellComponent());
      const navItems = shell.nav();
      const accountingItem = navItems.find(i => i.path === '/accounting');

      expect(accountingItem).toBeDefined();
      expect(accountingItem?.label).toBe('Accounts');
      expect(accountingItem?.label).not.toMatch(/accounting/i);
    });

    it('INT-T1-02: Forensic Audit: No user-facing HTML templates in accounting-ui contain "Accounting" string', () => {
      const accountingUiPath = resolve(__dirname, '../../../../accounting/accounting-ui/src/lib');

      const findHtmlFiles = (dir: string): string[] => {
        let results: string[] = [];
        try {
          const list = readdirSync(dir);
          for (const file of list) {
            const fullPath = join(dir, file);
            const stat = statSync(fullPath);
            if (stat && stat.isDirectory()) {
              results = results.concat(findHtmlFiles(fullPath));
            } else if (file.endsWith('.html')) {
              results.push(fullPath);
            }
          }
        } catch {
          // Directory not found or empty
        }
        return results;
      };

      const htmlFiles = findHtmlFiles(accountingUiPath);
      for (const htmlFile of htmlFiles) {
        const content = readFileSync(htmlFile, 'utf-8');
        // Match user visible text (e.g. >...Accounting...<)
        const visibleAccountingMatch = content.match(/>[^<]*\bAccounting\b[^<]*</i);
        expect(visibleAccountingMatch).toBeNull();
      }
    });

    it('INT-T1-03: Layer stacking Z-Index hierarchy in CSS matches architectural contract', () => {
      const stylesPath = resolve(__dirname, '../../../../../apps/web/src/styles.scss');
      const cssContent = readFileSync(stylesPath, 'utf-8');

      // Top bar header: z-index >= 6 or sticky
      // Table header: z-index: 3 with inset shadow
      expect(cssContent).toContain('.listwrap .table thead th');
      expect(cssContent).toContain('z-index: 3');
      expect(cssContent).toContain('box-shadow: inset 0 -1px 0');
    });
  });

  // =========================================================================
  // TIER 2: BOUNDARY CASES & MULTI-MODULE RESILIENCE
  // =========================================================================
  describe('Tier 2: Boundary Cases & Multi-Module Route Transitions', () => {
    it('INT-T2-01: Rapid switching between modules preserves breadcrumb consistency', () => {
      const shell = TestBed.runInInjectionContext(() => new ShellComponent());

      const testRoutes = [
        { url: '/dashboard', expectedCrumbs: [] },
        { url: '/contacts', expectedCrumbs: ['Contacts'] },
        { url: '/inventory/items', expectedCrumbs: ['Inventory', 'Items'] },
        { url: '/purchase/bills/new', expectedCrumbs: ['Purchase', 'Bills', 'New'] },
        { url: '/sales/transactions', expectedCrumbs: ['Sales', 'Transactions'] },
        { url: '/accounting/chart-of-accounts', expectedCrumbs: ['Accounts', 'Chart of accounts'] },
        { url: '/settings/currencies', expectedCrumbs: ['Settings', 'Currencies'] }
      ];

      for (const route of testRoutes) {
        shell.updateCrumbs(route.url);
        const labels = shell.crumbs().map(c => c.label);
        expect(labels).toEqual(route.expectedCrumbs);
      }
    });

    it('INT-T2-02: Permission lockdown immediately reflects across navigation computation', () => {
      // Allow only dashboard and settings
      mockAuthService.canView.mockImplementation((mod: string) => mod === 'settings');

      const shell = TestBed.runInInjectionContext(() => new ShellComponent());
      const visibleItems = shell.nav();

      expect(visibleItems.map(i => i.path)).toEqual(['/dashboard', '/settings']);
    });
  });

  // =========================================================================
  // TIER 3: CROSS-FEATURE COMBINATIONS
  // =========================================================================
  describe('Tier 3: Pairwise Cross-Feature Interactions', () => {
    it('INT-T3-01: Org switch -> Navigation update -> Sales List load -> Grid filter workflow', () => {
      const shell = TestBed.runInInjectionContext(() => new ShellComponent());
      const salesList = TestBed.runInInjectionContext(() => new SalesListComponent());

      // 1. Shell boots on org-1
      expect(shell.currentOrgId()).toBe('org-1');

      // 2. User navigates to Sales List
      shell.updateCrumbs('/sales/transactions');
      salesList.ngOnInit();

      expect(shell.crumbs().length).toBe(2);
      expect(salesList.transactions.length).toBe(2);

      // 3. User filters by Invoice in sales list
      salesList.setType('Invoice');
      expect(mockTransactionService.list).toHaveBeenCalledWith('Invoice');
    });

    it('INT-T3-02: Document line items dynamic calculation integrity across line additions', () => {
      const initialLines: DocumentLine[] = [
        {
          detailId: 1,
          lineNumber: 1,
          itemId: 1,
          itemLabel: 'Item A',
          hsnSacCode: '1001',
          description: 'Item A Description',
          warehouseId: null,
          quantity: 2,
          uomId: null,
          uomLabel: null,
          conversionFactor: 1,
          baseQuantity: 2,
          unitPrice: 1000,
          isPriceInclusive: false,
          discountPercent: 10,
          discountAmount: 200,
          grossAmount: 2000,
          taxableAmount: 1800,
          taxTreatment: 'Taxable',
          taxMasterId: 1,
          taxGroupId: null,
          taxAmount: 324,
          taxes: [
            { component: 'Cgst', subAccountId: 0, rate: 9, taxableAmount: 1800, amount: 162 },
            { component: 'Sgst', subAccountId: 0, rate: 9, taxableAmount: 1800, amount: 162 }
          ],
          lineType: 'Stock',
          accountId: 1,
          fixedAssetCategoryId: null,
          lineTotal: 2124,
          itemBatchId: null,
          lineNotes: null
        },
        {
          detailId: 2,
          lineNumber: 2,
          itemId: 2,
          itemLabel: 'Item B (Interstate)',
          hsnSacCode: '1002',
          description: 'Item B Description',
          warehouseId: null,
          quantity: 1,
          uomId: null,
          uomLabel: null,
          conversionFactor: 1,
          baseQuantity: 1,
          unitPrice: 5000,
          isPriceInclusive: false,
          discountPercent: 0,
          discountAmount: 0,
          grossAmount: 5000,
          taxableAmount: 5000,
          taxTreatment: 'Taxable',
          taxMasterId: 2,
          taxGroupId: null,
          taxAmount: 900,
          taxes: [
            { component: 'Igst', subAccountId: 0, rate: 18, taxableAmount: 5000, amount: 900 }
          ],
          lineType: 'Stock',
          accountId: 2,
          fixedAssetCategoryId: null,
          lineTotal: 5900,
          itemBatchId: null,
          lineNotes: null
        }
      ];

      const totals = totalsOf(initialLines);

      expect(totals.subTotal).toBe(7000);
      expect(totals.discountAmount).toBe(200);
      expect(totals.taxableAmount).toBe(6800);
      expect(totals.cgstAmount).toBe(162);
      expect(totals.sgstAmount).toBe(162);
      expect(totals.igstAmount).toBe(900);
      expect(totals.totalAmount).toBe(8024); // 6800 + 162 + 162 + 900 = 8024
    });
  });

  // =========================================================================
  // TIER 4: REAL-WORLD APPLICATION SCENARIOS & WORKFLOWS
  // =========================================================================
  describe('Tier 4: End-to-End Enterprise Scenario Walkthrough', () => {
    it('INT-T4-01: End-to-End Retail ERP Workflow Simulation', () => {
      // 1. Initialize App Shell
      const shell = TestBed.runInInjectionContext(() => new ShellComponent());
      expect(shell.nav().length).toBeGreaterThan(5);

      // 2. Navigate to Sales Register
      shell.updateCrumbs('/sales/transactions');
      expect(shell.crumbs()[0].label).toBe('Sales');

      // 3. Load Sales List Component
      const salesList = TestBed.runInInjectionContext(() => new SalesListComponent());
      salesList.ngOnInit();
      expect(salesList.transactions.length).toBe(2);

      // 4. Drill down to specific transaction
      const targetInvoice = salesList.transactions[0];
      expect(targetInvoice.transactionType).toBe('Invoice');
      expect(salesList.getRouteForTransaction(targetInvoice)).toBe('/sales/invoices/201');

      // 5. Update Breadcrumb for Invoice details
      shell.updateCrumbs('/sales/invoices/201');
      expect(shell.crumbs().map(c => c.label)).toEqual(['Sales', 'Invoices', '201']);
      expect(shell.crumbs()[2].isLast).toBe(true);
    });
  });
});
