import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NavigationEnd, Router, Event as RouterEvent } from '@angular/router';
import { Subject } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { ShellTopbarComponent } from './shell-topbar.component';
import { AuthService, AccessibleOrg, SessionContext, SessionContextService } from '@bill-book/auth';
import { signal } from '@angular/core';

import { ElementRef } from '@angular/core';
import { FavouritesService } from '../favourites.service';
import { ShellNotificationsService } from '../notifications.service';

describe('ShellTopbarComponent (libs/app-shell)', () => {
  let routerEvents$: Subject<RouterEvent>;
  let mockRouter: Partial<Router>;
  const sessionContext = signal<SessionContext | null>({
    displayName: 'Priya',
    email: 'priya@example.com',
    branchName: 'Chennai',
    branchCode: 'CHN',
    businessType: 'Retail',
    licenseStatus: 'Active',
    licenseExpiry: null,
    expiryIsBranchLevel: false,
    permissions: [],
    addOns: 'Payroll',
  });

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
    { orgId: 'org-3', orgName: 'North Distribution', roleName: 'Accountant' },
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
      // signOut revokes the token family server-side as well as clearing
      // storage, which is what "sign out" has to mean on a shared machine.
      signOut: vi.fn().mockResolvedValue(undefined),
    };

    const mockNativeElement = document.createElement('div');
    const orgContainer = document.createElement('div');
    orgContainer.className = 'org-dropdown-container';
    mockNativeElement.appendChild(orgContainer);
    mockElementRef = new ElementRef(mockNativeElement);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: ElementRef, useValue: mockElementRef },
        { provide: SessionContextService, useValue: { context: sessionContext } },

      ],
    });

    localStorage.setItem('bb.orgId', 'org-1');
    localStorage.removeItem('billbook.favourites.v1');
  });

  const createComponent = (): ShellTopbarComponent => {
    return TestBed.runInInjectionContext(() => new ShellTopbarComponent());
  };

  it('TOPBAR-01: Instantiates and boots organization list', async () => {
    const comp = createComponent();
    expect(comp).toBeDefined();
    expect(mockAuthService.accessibleOrganizations).toHaveBeenCalledTimes(1);
    expect(comp.financialYear()).toBe('FY 2026-27');
  });

  it('TOPBAR-02: Organization toggle opens and closes the dropdown', () => {
    const comp = createComponent();
    expect(comp.orgOpen()).toBe(false);

    comp.toggleOrg();
    expect(comp.orgOpen()).toBe(true);
    expect(comp.orgQuery()).toBe('');

    comp.toggleOrg();
    expect(comp.orgOpen()).toBe(false);
  });

  it('TOPBAR-03: Organization search filters available branches', async () => {
    const comp = createComponent();
    await Promise.resolve();
    comp.allOrgs.set(mockOrgs);

    comp.setOrgQuery('South');
    const filtered = comp.filteredOrgs();
    expect(filtered.length).toBe(1);
    expect(filtered[0].orgName).toBe('South Warehouse');

    comp.setOrgQuery('');
    expect(comp.filteredOrgs().length).toBe(3);
  });

  it('TOPBAR-04: Case-insensitive search works accurately', () => {
    const comp = createComponent();
    comp.allOrgs.set(mockOrgs);

    comp.setOrgQuery('nOrTh');
    expect(comp.filteredOrgs().length).toBe(1);
    expect(comp.filteredOrgs()[0].orgId).toBe('org-3');
  });

  it('TOPBAR-05: Escape closes every panel and clears every query', () => {
    const comp = createComponent();
    comp.toggleOrg();
    comp.setOrgQuery('south');

    comp.onEscape();
    expect(comp.orgOpen()).toBe(false);
    expect(comp.newOpen()).toBe(false);
    expect(comp.favOpen()).toBe(false);
    expect(comp.searchOpen()).toBe(false);
    expect(comp.notifOpen()).toBe(false);
    expect(comp.orgQuery()).toBe('');
  });

  it('TOPBAR-06: Pointerdown outside the bar closes the open panel', () => {
    const comp = createComponent();
    comp.toggleOrg();

    const outsideElement = document.createElement('span');
    document.body.appendChild(outsideElement);

    comp.onPointerDownOutside(outsideElement);
    expect(comp.orgOpen()).toBe(false);

    document.body.removeChild(outsideElement);
  });

  it('TOPBAR-07: Selecting a document emits its code and routes to a real create page', () => {
    const comp = createComponent();
    comp.toggleNew();
    expect(comp.newOpen()).toBe(true);

    let selectedAction = '';
    comp.quickAction.subscribe((action) => {
      selectedAction = action;
    });

    const invoice = comp.newGroups()
      .flatMap((g) => g.docs)
      .find((d) => d.code === 'INV');
    expect(invoice).toBeDefined();

    comp.selectDoc(invoice!);
    expect(selectedAction).toBe('INV');
    expect(mockRouter.navigateByUrl).toHaveBeenCalledWith('/sales/invoices/new');
    expect(comp.newOpen()).toBe(false);
  });

  it('TOPBAR-08: doLogout emits logout output, delegates to AuthService, and navigates to login', () => {
    const comp = createComponent();
    let loggedOut = false;
    comp.logout.subscribe(() => {
      loggedOut = true;
    });

    comp.doLogout();
    expect(loggedOut).toBe(true);
    expect(mockAuthService.signOut).toHaveBeenCalledTimes(1);
    expect(mockRouter.navigateByUrl).toHaveBeenCalledWith('/login');
  });

  it('TOPBAR-09: Switching to a different organization calls switchOrganization and emits output', async () => {
    const comp = createComponent();
    comp.allOrgs.set(mockOrgs);
    comp.toggleOrg();

    let changedOrg = '';
    comp.organizationChange.subscribe((orgId) => {
      changedOrg = orgId;
    });

    await comp.pickOrg('org-2');
    expect(mockAuthService.switchOrganization).toHaveBeenCalledWith('org-2');
    expect(changedOrg).toBe('org-2');
    expect(comp.orgOpen()).toBe(false);
  });

  it('TOPBAR-10: Only one panel is open at a time', () => {
    const comp = createComponent();

    comp.toggleNew();
    expect(comp.newOpen()).toBe(true);

    comp.toggleSearch();
    expect(comp.newOpen()).toBe(false);
    expect(comp.searchOpen()).toBe(true);

    comp.toggleNotif();
    expect(comp.searchOpen()).toBe(false);
    expect(comp.notifOpen()).toBe(true);
  });

  it('TOPBAR-11: Navigating closes whatever was open', () => {
    const comp = createComponent();
    comp.toggleFav();
    expect(comp.favOpen()).toBe(true);

    routerEvents$.next(new NavigationEnd(1, '/sales', '/sales'));
    expect(comp.favOpen()).toBe(false);
  });

  it('TOPBAR-12: Search returns nothing until something is typed, then matches screens', () => {
    const comp = createComponent();
    comp.toggleSearch();
    expect(comp.searchGroups()).toEqual([]);
    expect(comp.searchEmpty()).toBe(false);

    comp.setSearchQuery('trial');
    const hits = comp.searchGroups().flatMap((g) => g.items);
    expect(hits.map((h) => h.path)).toContain('/accounting/trial-balance');

    comp.setSearchQuery('@@@no-such-screen@@@');
    expect(comp.searchGroups()).toEqual([]);
    expect(comp.searchEmpty()).toBe(true);
  });

  it('TOPBAR-13: Search hides screens the role may not open', () => {
    mockAuthService.canView.mockImplementation((mod: string) => mod !== 'banking');
    const comp = createComponent();

    comp.setSearchQuery('bank');
    const paths = comp.searchGroups().flatMap((g) => g.items.map((i) => i.path));
    expect(paths).not.toContain('/banking/banks');
  });

  it('TOPBAR-14: Favourites lists starred screens and unstarring removes them', () => {
    const comp = createComponent();
    const favourites = TestBed.inject(FavouritesService);

    expect(comp.favEmpty()).toBe(true);

    favourites.star('/sales/invoices');
    const starred = comp.favGroups().flatMap((g) => g.items);
    expect(starred.map((s) => s.path)).toEqual(['/sales/invoices']);
    expect(comp.favCount()).toBe(1);

    comp.unstar(starred[0], new Event('click'));
    expect(comp.favEmpty()).toBe(true);
  });

  it('TOPBAR-15: The bell shows a dot only while something is unread', () => {
    const comp = createComponent();
    const notifications = TestBed.inject(ShellNotificationsService);

    expect(notifications.hasUnread()).toBe(false);

    notifications.set([
      { id: '1', kind: 'Invoice', when: '2h', text: 'INV-0042 is overdue', unread: true },
    ]);
    expect(notifications.hasUnread()).toBe(true);

    comp.markAllRead();
    expect(notifications.hasUnread()).toBe(false);
  });

  it('TOPBAR-16: New transaction search narrows the document list', () => {
    const comp = createComponent();
    comp.toggleNew();

    comp.setNewQuery('credit');
    const codes = comp.newFilteredGroups().flatMap((g) => g.docs.map((d) => d.code));
    expect(codes).toEqual(['CRN']);
    expect(comp.newEmpty()).toBe(false);

    comp.setNewQuery('@@@');
    expect(comp.newEmpty()).toBe(true);
  });

  // ---- Branch switcher grouped by organisation (TK-189) -------------------

  const groupedOrgs: AccessibleOrg[] = [
    { orgId: 'org-1', orgName: 'Chennai', roleName: 'Owner', customerName: 'Kumar Textiles', businessType: 'Retail', baseCurrency: 'INR', gstin: '33AAAAA0000A1Z5', city: 'Chennai' },
    { orgId: 'org-2', orgName: 'Coimbatore', roleName: 'Owner', customerName: 'Kumar Textiles', businessType: 'Retail', baseCurrency: 'INR', gstin: '33BBBBB0000B1Z5', city: 'Coimbatore' },
    { orgId: 'org-3', orgName: 'Anna Nagar', roleName: 'Accountant', customerName: 'Kumar Pharma', businessType: 'Pharma', baseCurrency: 'INR', gstin: null, city: 'Chennai' },
  ];

  it('TOPBAR-17: branches are grouped by organisation, in the order the API lists them', () => {
    const comp = createComponent();
    comp.allOrgs.set(groupedOrgs);

    const groups = comp.orgGroups();
    expect(groups.map((g) => g.name)).toEqual(['Kumar Textiles', 'Kumar Pharma']);
    expect(groups[0].orgs.map((o) => o.orgId)).toEqual(['org-1', 'org-2']);
    expect(groups[1].orgs.map((o) => o.orgId)).toEqual(['org-3']);
  });

  it('TOPBAR-18: only the current branch\'s group is open at first, and opening another shuts it', () => {
    const comp = createComponent();
    comp.allOrgs.set(groupedOrgs);

    expect(comp.orgGroups().map((g) => g.open)).toEqual([true, false]);

    comp.toggleOrgGroup(comp.orgGroups()[1]);
    expect(comp.orgGroups().map((g) => g.open)).toEqual([false, true]);

    comp.toggleOrgGroup(comp.orgGroups()[1]);
    expect(comp.orgGroups().map((g) => g.open)).toEqual([false, false]);
  });

  it('TOPBAR-19: closing the switcher forgets which group was opened', () => {
    const comp = createComponent();
    comp.allOrgs.set(groupedOrgs);
    comp.toggleOrg();
    comp.toggleOrgGroup(comp.orgGroups()[1]);

    comp.closeAll();
    expect(comp.orgGroups().map((g) => g.open)).toEqual([true, false]);
  });

  it('TOPBAR-20: a search opens every group and matches GSTIN and city as well as names', () => {
    const comp = createComponent();
    comp.allOrgs.set(groupedOrgs);

    comp.setOrgQuery('33bbbbb');
    expect(comp.orgGroups().map((g) => [g.name, g.orgs.map((o) => o.orgId), g.open])).toEqual([
      ['Kumar Textiles', ['org-2'], true],
    ]);

    comp.setOrgQuery('chennai');
    expect(comp.orgGroups().map((g) => g.name)).toEqual(['Kumar Textiles', 'Kumar Pharma']);
    expect(comp.orgGroups().every((g) => g.open)).toBe(true);

    comp.setOrgQuery('kumar pharma');
    expect(comp.filteredOrgs().map((o) => o.orgId)).toEqual(['org-3']);
  });

  it('TOPBAR-21: a row reads trade · organisation · currency', () => {
    const comp = createComponent();
    expect(comp.orgMeta(groupedOrgs[2])).toBe('Pharma · Kumar Pharma · INR');
  });

  it('TOPBAR-22: an API without the new fields still lists every branch, in one group, with its role', () => {
    const comp = createComponent();
    comp.allOrgs.set(mockOrgs);

    const groups = comp.orgGroups();
    expect(groups.length).toBe(1);
    expect(groups[0].name).toBe('Branches');
    expect(groups[0].open).toBe(true);
    expect(groups[0].orgs.length).toBe(3);
    expect(comp.orgMeta(mockOrgs[1])).toBe('Retail · Manager');
  });
});
