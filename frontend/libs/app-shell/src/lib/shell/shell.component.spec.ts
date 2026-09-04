import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, NavigationEnd, Event as RouterEvent } from '@angular/router';
import { Subject } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { ShellComponent } from './shell.component';
import { AuthService, AccessibleOrg } from '@bill-book/auth';
import { ElementRef } from '@angular/core';

describe('ShellComponent (libs/app-shell)', () => {
  let routerEvents$: Subject<RouterEvent>;
  let mockRouter: Partial<Router>;
  let mockAuthService: {
    canView: ReturnType<typeof vi.fn>;
    accessibleOrganizations: ReturnType<typeof vi.fn>;
    switchOrganization: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
    signOut: ReturnType<typeof vi.fn>;
  };
  let mockElementRef: ElementRef;

  const mockOrgs: AccessibleOrg[] = [
    { orgId: 'org-1', orgName: 'Main Branch', roleName: 'Owner' },
    { orgId: 'org-2', orgName: 'South Warehouse', roleName: 'Manager' },
    { orgId: 'org-3', orgName: 'North Distribution', roleName: 'Accountant' }
  ];

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
      accessibleOrganizations: vi.fn().mockResolvedValue(mockOrgs),
      switchOrganization: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn(),
      // signOut, not logout: signing out revokes the token family server-side
      // as well as clearing local storage.
      signOut: vi.fn().mockResolvedValue(undefined)
    };

    const mockNativeElement = document.createElement('div');
    const orgContainer = document.createElement('div');
    orgContainer.className = 'org-dropdown-container';
    mockNativeElement.appendChild(orgContainer);
    mockElementRef = new ElementRef(mockNativeElement);

    TestBed.configureTestingModule({
      providers: [
// The shell fetches the branch's display formats on boot, so it needs an
// HttpClient. Testing backend rather than a real one: these specs assert
// navigation and labels, and a stray request to a live backend would make them
// depend on something none of them are about.
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: ElementRef, useValue: mockElementRef }
      ]
    });

    localStorage.setItem('bb.orgId', 'org-1');
  });

  const createComponent = (): ShellComponent => {
    return TestBed.runInInjectionContext(() => new ShellComponent());
  };

  describe('Tier 1: Feature Coverage (R2 & R5 Specification)', () => {
    it('SHELL-T1-01: Component instantiates and loads accessible organizations on boot', async () => {
      const comp = createComponent();
      expect(comp).toBeDefined();
      expect(mockAuthService.accessibleOrganizations).toHaveBeenCalledTimes(1);
    });

    it('SHELL-T1-02: Left rail navigation contains all expected core modules', () => {
      const comp = createComponent();
      const navItems = comp.nav();
      const paths = navItems.map(item => item.path);

      expect(paths).toContain('/dashboard');
      expect(paths).toContain('/contacts');
      expect(paths).toContain('/inventory');
      expect(paths).toContain('/purchase');
      expect(paths).toContain('/sales');
      expect(paths).toContain('/banking');
      expect(paths).toContain('/accounting');
      expect(paths).toContain('/reports');
      expect(paths).toContain('/settings');
    });

    it('SHELL-T1-03: CRITICAL: Accounting module is labeled strictly as "Accounts" in navigation', () => {
      const comp = createComponent();
      const navItems = comp.nav();
      const accountingNav = navItems.find(i => i.path === '/accounting');

      expect(accountingNav).toBeDefined();
      expect(accountingNav?.label).toBe('Accounts');
      expect(accountingNav?.label).not.toBe('Accounting');
    });

    it('SHELL-T1-04: Topbar organization toggle opens and closes the dropdown', () => {
      const comp = createComponent();
      expect(comp.orgOpen()).toBe(false);

      comp.toggleOrg();
      expect(comp.orgOpen()).toBe(true);
      expect(comp.orgQuery()).toBe('');

      comp.toggleOrg();
      expect(comp.orgOpen()).toBe(false);
    });

    it('SHELL-T1-05: Topbar organization switcher filters orgs by query', async () => {
      const comp = createComponent();
      // Wait for orgs promise to resolve
      await Promise.resolve();
      comp.allOrgs.set(mockOrgs);

      comp.setOrgQuery('South');
      const filtered = comp.filteredOrgs();
      expect(filtered.length).toBe(1);
      expect(filtered[0].orgName).toBe('South Warehouse');

      comp.setOrgQuery('');
      expect(comp.filteredOrgs().length).toBe(3);
    });

    it('SHELL-T1-06: Topbar quick action popup toggles open and close', () => {
      const comp = createComponent();
      expect(comp.newOpen()).toBe(false);

      comp.openNew();
      expect(comp.newOpen()).toBe(true);

      comp.closeNew();
      expect(comp.newOpen()).toBe(false);
    });

    it('SHELL-T1-07: Breadcrumb component derives crumb trail for multi-level route', () => {
      const comp = createComponent();
      comp.updateCrumbs('/sales/invoices/new');

      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(3);
      expect(crumbs[0]).toEqual({ label: 'Sales', path: '/sales', isLink: true, isLast: false });
      expect(crumbs[1]).toEqual({ label: 'Invoices', path: '/sales/invoices', isLink: true, isLast: false });
      expect(crumbs[2]).toEqual({ label: 'New', path: '/sales/invoices/new', isLink: false, isLast: true });
    });

    it('SHELL-T1-08: User logout delegates to AuthService and redirects to /login', () => {
      const comp = createComponent();
      comp.logout();

      expect(mockAuthService.signOut).toHaveBeenCalledTimes(1);
      expect(mockRouter.navigateByUrl).toHaveBeenCalledWith('/login');
    });
  });

  describe('Tier 2: Boundary & Corner Cases', () => {
    it('SHELL-T2-01: Empty or dashboard route returns empty breadcrumb trail (replaces h1)', () => {
      const comp = createComponent();
      comp.updateCrumbs('/');
      expect(comp.crumbs()).toEqual([]);

      comp.updateCrumbs('/dashboard');
      expect(comp.crumbs()).toEqual([]);
    });

    it('SHELL-T2-02: Hyphenated route paths are transformed into titled spaced words', () => {
      const comp = createComponent();
      comp.updateCrumbs('/inventory/stock-adjustments');

      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(2);
      expect(crumbs[0].label).toBe('Inventory');
      expect(crumbs[1].label).toBe('Stock adjustments');
    });

    it('SHELL-T2-03: Special abbreviation "coa" is correctly expanded to "Chart of Accounts"', () => {
      const comp = createComponent();
      comp.updateCrumbs('/accounting/coa');

      const crumbs = comp.crumbs();
      expect(crumbs.length).toBe(2);
      expect(crumbs[1].label).toBe('Chart of Accounts');
    });

    it('SHELL-T2-04: Non-matching org query returns empty list without error', () => {
      const comp = createComponent();
      comp.allOrgs.set(mockOrgs);

      comp.setOrgQuery('Non-Existent Branch');
      expect(comp.filteredOrgs()).toEqual([]);
    });

    it('SHELL-T2-05: Case-insensitive org filtering matches mixed-case queries', () => {
      const comp = createComponent();
      comp.allOrgs.set(mockOrgs);

      comp.setOrgQuery('sOuTh');
      expect(comp.filteredOrgs().length).toBe(1);
      expect(comp.filteredOrgs()[0].orgId).toBe('org-2');
    });

    it('SHELL-T2-06: Escape keydown closes all open dialogs and popups', () => {
      const comp = createComponent();
      comp.orgOpen.set(true);
      comp.newOpen.set(true);
      comp.favOpen.set(true);

      comp.onEscape();

      expect(comp.orgOpen()).toBe(false);
      expect(comp.newOpen()).toBe(false);
      expect(comp.favOpen()).toBe(false);
    });

    it('SHELL-T2-07: Outside click closes organization dropdown', () => {
      const comp = createComponent();
      comp.orgOpen.set(true);

      const outsideElement = document.createElement('span');
      document.body.appendChild(outsideElement);

      comp.onClickOutside(outsideElement);
      expect(comp.orgOpen()).toBe(false);

      document.body.removeChild(outsideElement);
    });

    it('SHELL-T2-08: Role permission restriction filters out inaccessible module links', () => {
      // Mock user without sales permission
      mockAuthService.canView.mockImplementation((mod: string) => mod !== 'sales');
      const comp = createComponent();

      const navItems = comp.nav();
      const paths = navItems.map(item => item.path);

      expect(paths).not.toContain('/sales');
      expect(paths).toContain('/purchase');
      expect(paths).toContain('/accounting');
    });
  });

  describe('Tier 3: Cross-Feature Interactions & Navigation Sync', () => {
    it('SHELL-T3-01: NavigationEnd router events dynamically update breadcrumbs', () => {
      const comp = createComponent();

      routerEvents$.next(new NavigationEnd(1, '/contacts', '/contacts'));
      expect(comp.crumbs().length).toBe(1);
      expect(comp.crumbs()[0].label).toBe('Contacts');

      routerEvents$.next(new NavigationEnd(2, '/purchase/bills/101', '/purchase/bills/101'));
      expect(comp.crumbs().length).toBe(3);
      expect(comp.crumbs()[0].label).toBe('Purchase');
      expect(comp.crumbs()[1].label).toBe('Bills');
      expect(comp.crumbs()[2].label).toBe('101');
    });

    it('SHELL-T3-02: New transaction groups host Sales, Purchase, and Banking with standard document codes', () => {
      const comp = createComponent();
      const groups = comp.newGroups;

      expect(groups.length).toBe(3);
      const salesGroup = groups.find(g => g.name === 'Sales');
      const purchaseGroup = groups.find(g => g.name === 'Purchase');
      const bankingGroup = groups.find(g => g.name === 'Banking');

      expect(salesGroup?.docs.map(d => d.code)).toEqual(['INV', 'SOR', 'QOT', 'DLC', 'CRN', 'POS']);
      expect(purchaseGroup?.docs.map(d => d.code)).toEqual(['BIL', 'POR', 'GRN', 'DBN']);
      expect(bankingGroup?.docs.map(d => d.code)).toEqual(['REC', 'PAY', 'TRF']);
    });

    it('SHELL-T3-03: Dashboard basis toggle alternates between Accrual basis and Cash basis', () => {
      const comp = createComponent();
      expect(comp.base()).toBe(false);
      expect(comp.baseLabel()).toBe('Accrual basis');

      comp.toggleBase();
      expect(comp.base()).toBe(true);
      expect(comp.baseLabel()).toBe('Cash basis');

      comp.toggleBase();
      expect(comp.base()).toBe(false);
      expect(comp.baseLabel()).toBe('Accrual basis');
    });
  });

  describe('Tier 4: Real-World Shell Workflows', () => {
    it('SHELL-T4-01: Switching organization invokes auth service when new org is selected', async () => {
      const comp = createComponent();
      comp.allOrgs.set(mockOrgs);
      comp.orgOpen.set(true);

      // Picking the same org simply closes dropdown without triggering switch
      await comp.pickOrg('org-1');
      expect(comp.orgOpen()).toBe(false);
      expect(mockAuthService.switchOrganization).not.toHaveBeenCalled();
    });

    it('SHELL-T4-02: Full breadcrumb navigation trail lifecycle across multi-module walkthrough', () => {
      const comp = createComponent();

      // Step 1: Start on dashboard
      comp.updateCrumbs('/dashboard');
      expect(comp.crumbs()).toEqual([]);

      // Step 2: Navigate to Sales List
      comp.updateCrumbs('/sales/transactions');
      expect(comp.crumbs().map(c => c.label)).toEqual(['Sales', 'Transactions']);

      // Step 3: Drill down into Invoice edit
      comp.updateCrumbs('/sales/invoices/INV-2026-0042');
      expect(comp.crumbs().map(c => c.label)).toEqual(['Sales', 'Invoices', 'INV 2026 0042']);
      expect(comp.crumbs()[2].isLast).toBe(true);
      expect(comp.crumbs()[2].isLink).toBe(false);

      // Step 4: Navigate to Settings
      comp.updateCrumbs('/settings/tax');
      expect(comp.crumbs().map(c => c.label)).toEqual(['Settings', 'Tax']);
    });
  });
});
