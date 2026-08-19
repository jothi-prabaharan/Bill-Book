import { ElementRef, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, Event as RouterEvent } from '@angular/router';
import { Subject } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { ShellNavComponent } from './nav/shell-nav.component';
import { ShellTopbarComponent } from './topbar/shell-topbar.component';
import { ShellBreadcrumbComponent } from './breadcrumb/shell-breadcrumb.component';
import { AuthService, type AccessibleOrg } from '@bill-book/auth';

describe('Milestone 3 Empirical Challenger: App Shell Decomposition Suite', () => {
  let routerEvents$: Subject<RouterEvent>;
  let mockRouter: Partial<Router>;
  let mockAuthService: {
    canView: ReturnType<typeof vi.fn>;
    accessibleOrganizations: ReturnType<typeof vi.fn>;
    switchOrganization: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let mockElementRef: ElementRef;

  const mockOrgs: AccessibleOrg[] = [
    { orgId: 'org-main', orgName: 'Main Retail Store', roleName: 'Owner' },
    { orgId: 'org-north', orgName: 'North Distribution Hub', roleName: 'Manager' },
    { orgId: 'org-south', orgName: 'South Warehouse Outlet', roleName: 'Cashier' },
  ];

  beforeEach(() => {
    routerEvents$ = new Subject<RouterEvent>();
    mockRouter = {
      url: '/dashboard',
      events: routerEvents$.asObservable(),
      navigateByUrl: vi.fn().mockResolvedValue(true),
      navigate: vi.fn().mockResolvedValue(true),
    };

    mockAuthService = {
      canView: vi.fn().mockReturnValue(true),
      accessibleOrganizations: vi.fn().mockResolvedValue(mockOrgs),
      switchOrganization: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn(),
    };

    const mockNativeElement = document.createElement('div');
    const orgContainer = document.createElement('div');
    orgContainer.className = 'org-dropdown-container';
    mockNativeElement.appendChild(orgContainer);
    mockElementRef = new ElementRef(mockNativeElement);

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: ElementRef, useValue: mockElementRef },
      ],
    });

    localStorage.setItem('bb.orgId', 'org-main');
  });

  // =========================================================================
  // CHALLENGE 1: Z-INDEX COLLISION & STACKING LAYER INTEGRITY
  // =========================================================================
  describe('Challenge 1: Z-Index Collision & Stacking Layer Hierarchy', () => {
    const rootPath = resolve(__dirname, '../../../../');
    const shellScssPath = resolve(__dirname, 'shell/shell.component.scss');
    const topbarScssPath = resolve(__dirname, 'topbar/shell-topbar.component.scss');
    const navScssPath = resolve(__dirname, 'nav/shell-nav.component.scss');
    const breadcrumbScssPath = resolve(__dirname, 'breadcrumb/shell-breadcrumb.component.scss');
    const tableScssPath = resolve(rootPath, 'libs/shared/theming/src/lib/_table.scss');

    it('CHAL-M3-01: SCSS source files define the exact architectural z-index hierarchy', () => {
      const shellScss = readFileSync(shellScssPath, 'utf-8');
      const topbarScss = readFileSync(topbarScssPath, 'utf-8');
      const navScss = readFileSync(navScssPath, 'utf-8');
      const breadcrumbScss = readFileSync(breadcrumbScssPath, 'utf-8');
      const tableScss = readFileSync(tableScssPath, 'utf-8');

      // Topbar: z-index: 6
      expect(shellScss).toMatch(/\.shell-topbar-cell\s*\{[^}]*z-index:\s*6/s);
      expect(topbarScss).toMatch(/\.shell-header\s*\{[^}]*z-index:\s*6/s);

      // Left Rail: z-index: 5
      expect(shellScss).toMatch(/\.shell-nav-cell\s*\{[^}]*z-index:\s*5/s);
      expect(navScss).toMatch(/\.shell-sidebar\s*\{[^}]*z-index:\s*5/s);

      // Breadcrumb strip: z-index: 4
      expect(shellScss).toMatch(/\.shell-breadcrumb-cell\s*\{[^}]*z-index:\s*4/s);
      expect(breadcrumbScss).toMatch(/\.crumbs\s*\{[^}]*z-index:\s*4/s);

      // Sticky Table Header: z-index: 3
      expect(tableScss).toMatch(/\.table thead th\s*\{[^}]*z-index:\s*3/s);

      // Main Content Viewport: z-index: 1
      expect(shellScss).toMatch(/\.shell-content-cell\s*\{[^}]*z-index:\s*1/s);
    });

    it('CHAL-M3-02: Stacking order guarantees Topbar (6) > Rail (5) > Breadcrumbs (4) > Table Header (3) > Content (1)', () => {
      const layers = [
        { name: 'Topbar', zIndex: 6 },
        { name: 'Rail', zIndex: 5 },
        { name: 'Breadcrumbs', zIndex: 4 },
        { name: 'TableHeader', zIndex: 3 },
        { name: 'Content', zIndex: 1 },
      ];

      for (let i = 0; i < layers.length - 1; i++) {
        expect(
          layers[i].zIndex,
          `Layer "${layers[i].name}" (${layers[i].zIndex}) must have higher z-index than "${layers[i + 1].name}" (${layers[i + 1].zIndex})`,
        ).toBeGreaterThan(layers[i + 1].zIndex);
      }
    });

    it('CHAL-M3-03: Modal dialogs, dropdowns, and overlays stack above all standard chrome layers', () => {
      const topbarScss = readFileSync(topbarScssPath, 'utf-8');
      const navScss = readFileSync(navScssPath, 'utf-8');

      // Topbar Org Dropdown: z-index: 20
      expect(topbarScss).toMatch(/\.shell-org-dropdown\s*\{[^}]*z-index:\s*20/s);

      // Mobile More Overlay: z-index: 20 / panel: 21
      expect(navScss).toMatch(/\.shell-more-overlay\s*\{[^}]*z-index:\s*20/s);
      expect(navScss).toMatch(/\.shell-more-panel\s*\{[^}]*z-index:\s*21/s);
    });
  });

  // =========================================================================
  // CHALLENGE 2: ACTION PROJECTION ([bbShellActions] & .acts)
  // =========================================================================
  describe('Challenge 2: Action Projection in ShellBreadcrumbComponent', () => {
    it('CHAL-M3-04: Breadcrumb template defines projection target for [bbShellActions] and .acts', () => {
      const breadcrumbHtmlPath = resolve(__dirname, 'breadcrumb/shell-breadcrumb.component.html');
      const template = readFileSync(breadcrumbHtmlPath, 'utf-8');

      // Verify that <ng-content select="[bbShellActions], .acts" /> exists inside <div class="acts">
      expect(template).toMatch(/<div class="acts">[\s\S]*?<ng-content select="\[bbShellActions\],\s*\.acts"\s*\/>[\s\S]*?<\/div>/);
    });

    it('CHAL-M3-05: Dashboard contextual actions render only when on dashboard route', () => {
      const comp = TestBed.runInInjectionContext(() => new ShellBreadcrumbComponent());
      expect(comp.isHome()).toBe(true);
      expect(comp.isRegister()).toBe(false);

      // Basis toggle
      expect(comp.base()).toBe(false);
      expect(comp.baseLabel()).toBe('Accrual basis');
      comp.toggleBase();
      expect(comp.base()).toBe(true);
      expect(comp.baseLabel()).toBe('Cash basis');

      // Customization edit state
      expect(comp.editing()).toBe(false);
      expect(comp.notEditing()).toBe(true);
      comp.startEdit();
      expect(comp.editing()).toBe(true);
      expect(comp.notEditing()).toBe(false);
      comp.stopEdit();
      expect(comp.editing()).toBe(false);
      expect(comp.notEditing()).toBe(true);
    });
  });

  // =========================================================================
  // CHALLENGE 3: NAVIGATION ACCESSIBILITY & ROLE PERMISSIONS FILTERING
  // =========================================================================
  describe('Challenge 3: Navigation Accessibility & Role Permissions', () => {
    it('CHAL-M3-06: Accessibility: Landmark nav elements carry standard aria-label attributes in templates', () => {
      const navHtmlPath = resolve(__dirname, 'nav/shell-nav.component.html');
      const navTemplate = readFileSync(navHtmlPath, 'utf-8');

      // Desktop rail nav
      expect(navTemplate).toMatch(/<nav\s+aria-label="Modules"\s+class="shell-sidebar desktop-nav"/);
      // Mobile bottom tab bar nav
      expect(navTemplate).toMatch(/<nav\s+aria-label="Modules"\s+class="shell-mobile-nav mobile-nav"/);

      const breadcrumbHtmlPath = resolve(__dirname, 'breadcrumb/shell-breadcrumb.component.html');
      const breadcrumbTemplate = readFileSync(breadcrumbHtmlPath, 'utf-8');
      // Breadcrumb nav
      expect(breadcrumbTemplate).toMatch(/<nav\s+aria-label="Breadcrumb"\s+class="crumbs"/);
    });

    it('CHAL-M3-07: Accessibility: Active/current breadcrumb item has aria-current="page"', () => {
      const breadcrumbHtmlPath = resolve(__dirname, 'breadcrumb/shell-breadcrumb.component.html');
      const template = readFileSync(breadcrumbHtmlPath, 'utf-8');

      expect(template).toContain('@if (crumb.isLast) {');
      expect(template).toContain('<span aria-current="page">{{ crumb.label }}</span>');
    });

    it('CHAL-M3-08: Accessibility: Topbar org picker provides aria-expanded and aria-label', () => {
      const topbarHtmlPath = resolve(__dirname, 'topbar/shell-topbar.component.html');
      const template = readFileSync(topbarHtmlPath, 'utf-8');

      expect(template).toContain('aria-label="Switch organization"');
      expect(template).toContain('[attr.aria-expanded]="orgOpen()"');
      expect(template).toContain('[attr.aria-current]="org.orgId === currentOrgId() ? \'true\' : null"');
    });

    it('CHAL-M3-09: Accessibility: Display-only FY tag carries informative aria-label', () => {
      const topbarHtmlPath = resolve(__dirname, 'topbar/shell-topbar.component.html');
      const template = readFileSync(topbarHtmlPath, 'utf-8');

      expect(template).toContain('aria-label="Current financial year"');
      expect(template).toContain('{{ financialYear() }}');
    });

    it('CHAL-M3-10: Role filtering: canView restricts desktop primaryNav, settings, and mobile tabs', () => {
      // Allow only sales and contacts
      mockAuthService.canView.mockImplementation((mod: string) => {
        return mod === 'sales' || mod === 'contacts';
      });

      const navComp = TestBed.runInInjectionContext(() => new ShellNavComponent());
      const visible = navComp.nav().map((i) => i.path);

      // Home (module: null) is always visible
      expect(visible).toContain('/dashboard');
      expect(visible).toContain('/contacts');
      expect(visible).toContain('/sales');

      // Others must be excluded
      expect(visible).not.toContain('/inventory');
      expect(visible).not.toContain('/purchase');
      expect(visible).not.toContain('/banking');
      expect(visible).not.toContain('/accounting');
      expect(visible).not.toContain('/reports');
      expect(visible).not.toContain('/settings');

      expect(navComp.settingsItem()).toBeUndefined();
      expect(navComp.primaryNav().map((i) => i.path)).toEqual(['/dashboard', '/contacts', '/sales']);
    });

    it('CHAL-M3-11: Role filtering: Dynamic revocation via signal immediately updates computed nav', () => {
      const allowedMod = signal<string | null>('sales');
      mockAuthService.canView.mockImplementation((mod: string) => mod === allowedMod());

      const navComp = TestBed.runInInjectionContext(() => new ShellNavComponent());
      expect(navComp.nav().map((i) => i.path)).toEqual(['/dashboard', '/sales']);

      // Dynamically revoke sales and grant inventory
      allowedMod.set('inventory');
      expect(navComp.nav().map((i) => i.path)).toEqual(['/dashboard', '/inventory']);

      // Revoke all
      allowedMod.set(null);
      expect(navComp.nav().map((i) => i.path)).toEqual(['/dashboard']);
    });
  });

  // =========================================================================
  // CHALLENGE 4: DOWNSTREAM MODULE ROUTES & STRICT "Accounts" LABEL AUDIT
  // =========================================================================
  describe('Challenge 4: Route Integration & Accounting -> Accounts Enforcement', () => {
    it('CHAL-M3-12: Strict Rule R5: Accounting module is labeled strictly as "Accounts" across all shell views', () => {
      const navComp = TestBed.runInInjectionContext(() => new ShellNavComponent());
      const accountingNav = navComp.allNavItems.find((i) => i.path === '/accounting');
      expect(accountingNav?.label).toBe('Accounts');
      expect(accountingNav?.label).not.toMatch(/accounting/i);

      const crumbComp = TestBed.runInInjectionContext(() => new ShellBreadcrumbComponent());
      crumbComp.updateCrumbs('/accounting/general-ledger');
      const crumbs = crumbComp.crumbs();
      expect(crumbs[0].label).toBe('Accounts');
      expect(crumbs[0].label).not.toMatch(/accounting/i);
      expect(crumbs[1].label).toBe('General ledger');
    });

    it('CHAL-M3-13: Deep nested routes for all 8 business modules produce valid breadcrumb chains', () => {
      const crumbComp = TestBed.runInInjectionContext(() => new ShellBreadcrumbComponent());

      const moduleRoutes = [
        { url: '/contacts/customers/new', expected: ['Contacts', 'Customers', 'New'] },
        { url: '/inventory/items/SKU-990', expected: ['Inventory', 'Items', 'SKU 990'] },
        { url: '/purchase/bills/BIL-2026-001', expected: ['Purchase', 'Bills', 'BIL 2026 001'] },
        { url: '/sales/invoices/INV-2026-042', expected: ['Sales', 'Invoices', 'INV 2026 042'] },
        { url: '/banking/reconciliations/new', expected: ['Banking', 'Reconciliations', 'New'] },
        { url: '/accounting/coa', expected: ['Accounts', 'Chart of Accounts'] },
        { url: '/reports/profit-and-loss', expected: ['Reports', 'Profit and loss'] },
        { url: '/settings/number-series', expected: ['Settings', 'Number series'] },
      ];

      for (const route of moduleRoutes) {
        crumbComp.updateCrumbs(route.url);
        expect(crumbComp.crumbs().map((c) => c.label)).toEqual(route.expected);
      }
    });

    it('CHAL-M3-14: Shell Topbar provides standard Quick Action document codes across Sales, Purchase, and Banking', () => {
      const topbarComp = TestBed.runInInjectionContext(() => new ShellTopbarComponent());
      const groups = topbarComp.newGroups;

      const salesDocs = groups.find((g) => g.name === 'Sales')?.docs.map((d) => d.code);
      expect(salesDocs).toEqual(['INV', 'SOR', 'QOT', 'DLC', 'CRN', 'POS']);

      const purchaseDocs = groups.find((g) => g.name === 'Purchase')?.docs.map((d) => d.code);
      expect(purchaseDocs).toEqual(['BIL', 'POR', 'GRN', 'DBN']);

      const bankingDocs = groups.find((g) => g.name === 'Banking')?.docs.map((d) => d.code);
      expect(bankingDocs).toEqual(['REC', 'PAY', 'TRF']);
    });
  });
});
