import { TestBed } from '@angular/core/testing';
import { Router, Event as RouterEvent } from '@angular/router';
import { Subject } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { resolve, join } from 'node:path';
import { ElementRef } from '@angular/core';

import { ShellComponent } from './shell/shell.component';
import { ShellNavComponent } from './nav/shell-nav.component';
import { ShellTopbarComponent } from './topbar/shell-topbar.component';
import { ShellBreadcrumbComponent } from './breadcrumb/shell-breadcrumb.component';
import { AuthService, type AccessibleOrg } from '@bill-book/auth';

describe('Adversarial Stress Test Suite: Milestone 3 App Shell', () => {
  let routerEvents$: Subject<RouterEvent>;
  let mockRouter: Partial<Router>;
  let mockAuthService: {
    canView: ReturnType<typeof vi.fn>;
    accessibleOrganizations: ReturnType<typeof vi.fn>;
    switchOrganization: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };
  let mockNativeElement: HTMLElement;
  let mockElementRef: ElementRef;

  const mockOrgs: AccessibleOrg[] = [
    { orgId: 'org-hq', orgName: 'Headquarters Bengaluru', roleName: 'Owner' },
    { orgId: 'org-mum', orgName: 'Mumbai Retail Hub', roleName: 'Store Manager' },
    { orgId: 'org-del', orgName: 'Delhi Warehouse & Depot', roleName: 'Inventory Accountant' },
    { orgId: 'org-chn', orgName: 'Chennai Distribution Center', roleName: 'Auditor' },
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

    mockNativeElement = document.createElement('div');
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

    localStorage.setItem('bb.orgId', 'org-hq');
  });

  // =========================================================================
  // CHALLENGE AREA 1: LAYOUT & VIEWPORT STRESS TESTING (360px, 768px, 1920px)
  // =========================================================================
  describe('Challenge Area 1: Layout & Viewport Stress-Testing', () => {
    it('ADV-LAYOUT-01: Grid layout rules adhere to 56px rail, 46px topbar, and proper row/col spans on desktop', () => {
      const scssPath = resolve(__dirname, 'shell/shell.component.scss');
      const scss = readFileSync(scssPath, 'utf-8');

      // Verify desktop grid structure
      expect(scss).toContain('grid-template-columns: 56px 1fr;');
      expect(scss).toContain('grid-template-rows: 46px auto 1fr;');
      expect(scss).toContain('height: 100dvh;');
      expect(scss).toContain('width: 100vw;');
      expect(scss).toContain('overflow: hidden;');

      // Verify content scroll viewport does not induce double scrollbars
      expect(scss).toContain('.shell-content-cell');
      expect(scss).toContain('overflow-y: auto;');
      expect(scss).toContain('min-height: 0;');
      expect(scss).toContain('min-width: 0;');
    });

    it('ADV-LAYOUT-02: Responsive breakpoint at 860px cleanly transforms layout for mobile (360px) and tablet (768px)', () => {
      const scssPath = resolve(__dirname, 'shell/shell.component.scss');
      const scss = readFileSync(scssPath, 'utf-8');

      expect(scss).toContain('@media (max-width: 860px)');
      expect(scss).toContain('grid-template-columns: 1fr;');
      expect(scss).toContain('grid-template-rows: 46px auto 1fr auto;');
    });

    it('ADV-LAYOUT-03: Left rail and mobile bottom nav mutually exclude each other via media queries', () => {
      const navScssPath = resolve(__dirname, 'nav/shell-nav.component.scss');
      const navScss = readFileSync(navScssPath, 'utf-8');

      expect(navScss).toContain('@media (max-width: 860px)');
      expect(navScss).toContain('.desktop-nav {');
      expect(navScss).toContain('display: none !important;');

      expect(navScss).toContain('@media (min-width: 861px)');
      expect(navScss).toContain('.mobile-nav {');
      expect(navScss).toContain('display: none !important;');
    });

    it('ADV-LAYOUT-04: Z-index stacking hierarchy prevents chrome overlap', () => {
      const shellScss = readFileSync(resolve(__dirname, 'shell/shell.component.scss'), 'utf-8');
      const topbarScss = readFileSync(resolve(__dirname, 'topbar/shell-topbar.component.scss'), 'utf-8');
      const navScss = readFileSync(resolve(__dirname, 'nav/shell-nav.component.scss'), 'utf-8');

      // Topbar (z-index: 6) > Left rail (z-index: 5) > Breadcrumbs (z-index: 4) > Content (z-index: 1)
      expect(shellScss).toContain('z-index: 6'); // topbar
      expect(shellScss).toContain('z-index: 5'); // nav
      expect(shellScss).toContain('z-index: 4'); // breadcrumb
      expect(shellScss).toContain('z-index: 1'); // content

      // Dropdown and modal layers are above chrome
      expect(topbarScss).toContain('z-index: 20'); // org dropdown
      expect(navScss).toContain('z-index: 20'); // more overlay
      expect(navScss).toContain('z-index: 21'); // more panel
    });
  });

  // =========================================================================
  // CHALLENGE AREA 2: ROUTE PATH RESOLUTION & DEEP LINK STRESS TESTING
  // =========================================================================
  describe('Challenge Area 2: Route Path Resolution Stress-Testing', () => {
    it('ADV-ROUTE-01: Deep route /sales/invoices/new extracts 3 levels without crashing', () => {
      const comp = TestBed.runInInjectionContext(() => new ShellComponent());
      comp.updateCrumbs('/sales/invoices/new');

      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(3);
      expect(crumbs[0]).toEqual({ label: 'Sales', path: '/sales', isLink: true, isLast: false });
      expect(crumbs[1]).toEqual({ label: 'Invoices', path: '/sales/invoices', isLink: true, isLast: false });
      expect(crumbs[2]).toEqual({ label: 'New', path: '/sales/invoices/new', isLink: false, isLast: true });
    });

    it('ADV-ROUTE-02: Deep parameterized route /sales/invoices/123 extracts numeric ID without crashing', () => {
      const comp = TestBed.runInInjectionContext(() => new ShellComponent());
      comp.updateCrumbs('/sales/invoices/123');

      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(3);
      expect(crumbs[0]).toEqual({ label: 'Sales', path: '/sales', isLink: true, isLast: false });
      expect(crumbs[1]).toEqual({ label: 'Invoices', path: '/sales/invoices', isLink: true, isLast: false });
      expect(crumbs[2]).toEqual({ label: '123', path: '/sales/invoices/123', isLink: false, isLast: true });
    });

    it('ADV-ROUTE-03: Multi-hyphenated route /inventory/stock-adjustments extracts spaced capitalized words', () => {
      const comp = TestBed.runInInjectionContext(() => new ShellComponent());
      comp.updateCrumbs('/inventory/stock-adjustments');

      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(2);
      expect(crumbs[0]).toEqual({ label: 'Inventory', path: '/inventory', isLink: true, isLast: false });
      expect(crumbs[1]).toEqual({ label: 'Stock adjustments', path: '/inventory/stock-adjustments', isLink: false, isLast: true });
    });

    it('ADV-ROUTE-04: Deep route /accounting/coa extracts "Accounts" and "Chart of Accounts"', () => {
      const comp = TestBed.runInInjectionContext(() => new ShellComponent());
      comp.updateCrumbs('/accounting/coa');

      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(2);
      expect(crumbs[0]).toEqual({ label: 'Accounts', path: '/accounting', isLink: true, isLast: false });
      expect(crumbs[1]).toEqual({ label: 'Chart of Accounts', path: '/accounting/coa', isLink: false, isLast: true });
    });

    it('ADV-ROUTE-05: Deep route with arbitrary invalid/unknown path /invalid-route gracefully resolves without error', () => {
      const comp = TestBed.runInInjectionContext(() => new ShellComponent());
      comp.updateCrumbs('/invalid-route');

      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(1);
      expect(crumbs[0]).toEqual({ label: 'Invalid route', path: '/invalid-route', isLink: false, isLast: true });
    });

    it('ADV-ROUTE-06: Malformed routes with multiple slashes and query strings parse cleanly', () => {
      const comp = TestBed.runInInjectionContext(() => new ShellComponent());

      // Query params and multiple slashes
      comp.updateCrumbs('///purchase//bills///105?view=summary&sort=desc#details');
      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(3);
      expect(crumbs[0]).toEqual({ label: 'Purchase', path: '/purchase', isLink: true, isLast: false });
      expect(crumbs[1]).toEqual({ label: 'Bills', path: '/purchase/bills', isLink: true, isLast: false });
      expect(crumbs[2]).toEqual({ label: '105', path: '/purchase/bills/105', isLink: false, isLast: true });
    });

    it('ADV-ROUTE-07: Breadcrumb component directly mirrors identical robust route handling', () => {
      const crumbComp = TestBed.runInInjectionContext(() => new ShellBreadcrumbComponent());
      crumbComp.updateCrumbs('/accounting/coa');

      expect(crumbComp.crumbs()).toEqual([
        { label: 'Accounts', path: '/accounting', isLink: true, isLast: false },
        { label: 'Chart of Accounts', path: '/accounting/coa', isLink: false, isLast: true },
      ]);
    });
  });

  // =========================================================================
  // CHALLENGE AREA 3: ORG SWITCHER STRESS TESTING
  // =========================================================================
  describe('Challenge Area 3: Org Switcher Stress-Testing', () => {
    it('ADV-ORG-01: Empty search string preserves all available organizations', () => {
      const topbar = TestBed.runInInjectionContext(() => new ShellTopbarComponent());
      topbar.allOrgs.set(mockOrgs);

      topbar.setOrgQuery('');
      expect(topbar.filteredOrgs().length).toBe(4);
      expect(topbar.filteredOrgs()).toEqual(mockOrgs);
    });

    it('ADV-ORG-02: Non-matching search string returns empty array with zero crashes', () => {
      const topbar = TestBed.runInInjectionContext(() => new ShellTopbarComponent());
      topbar.allOrgs.set(mockOrgs);

      topbar.setOrgQuery('@@@XYZ-NO-MATCH###');
      expect(topbar.filteredOrgs()).toEqual([]);
    });

    it('ADV-ORG-03: Switching org IDs triggers auth service switch and emits change', async () => {
      const topbar = TestBed.runInInjectionContext(() => new ShellTopbarComponent());
      topbar.allOrgs.set(mockOrgs);
      topbar.orgOpen.set(true);

      let emittedOrg = '';
      topbar.organizationChange.subscribe((id) => (emittedOrg = id));

      await topbar.pickOrg('org-mum');
      expect(mockAuthService.switchOrganization).toHaveBeenCalledWith('org-mum');
      expect(emittedOrg).toBe('org-mum');
      expect(topbar.orgOpen()).toBe(false);
    });

    it('ADV-ORG-04: Picking current active org ID closes popup without re-triggering switch', async () => {
      const topbar = TestBed.runInInjectionContext(() => new ShellTopbarComponent());
      topbar.allOrgs.set(mockOrgs);
      topbar.orgOpen.set(true);

      await topbar.pickOrg('org-hq'); // currentOrgId is org-hq
      expect(mockAuthService.switchOrganization).not.toHaveBeenCalled();
      expect(topbar.orgOpen()).toBe(false);
    });

    it('ADV-ORG-05: Escape key handling closes org switcher and all open overlays', () => {
      const topbar = TestBed.runInInjectionContext(() => new ShellTopbarComponent());
      topbar.orgOpen.set(true);
      topbar.newOpen.set(true);
      topbar.favOpen.set(true);

      topbar.onEscape();
      expect(topbar.orgOpen()).toBe(false);
      expect(topbar.newOpen()).toBe(false);
      expect(topbar.favOpen()).toBe(false);
    });

    it('ADV-ORG-06: Outside click dismisses open organization switcher dropdown', () => {
      const topbar = TestBed.runInInjectionContext(() => new ShellTopbarComponent());
      topbar.orgOpen.set(true);

      const outsideElement = document.createElement('div');
      document.body.appendChild(outsideElement);

      topbar.onClickOutside(outsideElement);
      expect(topbar.orgOpen()).toBe(false);

      document.body.removeChild(outsideElement);
    });
  });

  // =========================================================================
  // CHALLENGE AREA 4: STRICT UI LABEL AUDIT ("Accounting" strictly forbidden)
  // =========================================================================
  describe('Challenge Area 4: Strict UI Label Audit', () => {
    it('ADV-AUDIT-01: ShellNavComponent labels accounting navigation strictly as "Accounts"', () => {
      const nav = TestBed.runInInjectionContext(() => new ShellNavComponent());
      const navItems = nav.nav();
      const accountingNav = navItems.find((i) => i.path === '/accounting');

      expect(accountingNav).toBeDefined();
      expect(accountingNav?.label).toBe('Accounts');
      expect(accountingNav?.label).not.toMatch(/accounting/i);
    });

    it('ADV-AUDIT-02: ShellComponent nav computation strictly outputs "Accounts" for accounting route', () => {
      const shell = TestBed.runInInjectionContext(() => new ShellComponent());
      const accountingItem = shell.nav().find((i) => i.path === '/accounting');

      expect(accountingItem).toBeDefined();
      expect(accountingItem?.label).toBe('Accounts');
      expect(accountingItem?.label).not.toMatch(/accounting/i);
    });

    it('ADV-AUDIT-03: ShellBreadcrumbComponent transforms /accounting path segment strictly to "Accounts"', () => {
      const breadcrumbs = TestBed.runInInjectionContext(() => new ShellBreadcrumbComponent());
      breadcrumbs.updateCrumbs('/accounting');

      const items = breadcrumbs.crumbs();
      expect(items.length).toBe(1);
      expect(items[0].label).toBe('Accounts');
      expect(items[0].label).not.toMatch(/accounting/i);
    });

    it('ADV-AUDIT-04: Forensic File Scan across all libs/app-shell templates and code ensures no user-visible "Accounting" text', () => {
      const appShellDir = resolve(__dirname, '..');

      const getAllSourceFiles = (dir: string): string[] => {
        let results: string[] = [];
        const entries = readdirSync(dir);
        for (const entry of entries) {
          const fullPath = join(dir, entry);
          const stat = statSync(fullPath);
          if (stat && stat.isDirectory()) {
            results = results.concat(getAllSourceFiles(fullPath));
          } else if (
            (entry.endsWith('.html') || entry.endsWith('.ts') || entry.endsWith('.scss')) &&
            !entry.endsWith('.spec.ts') // Spec files test for the forbidden word, so ignore specs
          ) {
            results.push(fullPath);
          }
        }
        return results;
      };

      const sourceFiles = getAllSourceFiles(appShellDir);
      expect(sourceFiles.length).toBeGreaterThan(5);

      for (const file of sourceFiles) {
        const content = readFileSync(file, 'utf-8');

        if (file.endsWith('.html')) {
          // Strip Angular control flow statements (like @case ('accounting'), @switch, etc.)
          const cleanHtml = content.replace(/@case\s*\([^)]*\)/g, '').replace(/@switch\s*\([^)]*\)/g, '');
          // Match visible text content or attribute labels (e.g. title="Accounting", aria-label="Accounting", >...Accounting...<)
          const visibleTextMatches = cleanHtml.match(/>[^<]*\bAccounting\b[^<]*</i) ||
                                     cleanHtml.match(/(?:title|aria-label|placeholder)=["'][^"']*\bAccounting\b[^"']*["']/i);
          expect(
            visibleTextMatches,
            `Found forbidden user-facing "Accounting" text in template: ${file}`
          ).toBeNull();
        }

        if (file.endsWith('.ts')) {
          // TS label check: verify label: 'Accounting' does NOT exist
          const labelAccountingMatch = content.match(/label:\s*['"`]Accounting['"`]/i);
          expect(
            labelAccountingMatch,
            `Found forbidden label: 'Accounting' in TS file: ${file}`
          ).toBeNull();
        }
      }
    });
  });
});
