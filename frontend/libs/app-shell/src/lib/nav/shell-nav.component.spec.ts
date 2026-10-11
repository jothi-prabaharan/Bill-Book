import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NavigationEnd, Router, Event as RouterEvent } from '@angular/router';
import { Subject } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { ShellNavComponent } from './shell-nav.component';
import { AccessibleOrg, AuthService, SessionContext, SessionContextService } from '@bill-book/auth';
import { MenuService } from '../menu.service';
import { ShellRailItem } from '../shell-screens';
import { ShellNavigationService } from '../shell-navigation.service';

describe('ShellNavComponent (libs/app-shell)', () => {
  let routerEvents$: Subject<RouterEvent>;
  let mockRouter: Partial<Router>;
  let mockAuthService: {
    canView: ReturnType<typeof vi.fn>;
    accessibleOrganizations: ReturnType<typeof vi.fn>;
    activeOrgId: ReturnType<typeof signal<string | null>>;
  };
  let sessionContext: ReturnType<typeof signal<SessionContext | null>>;

  const orgs: AccessibleOrg[] = [
    { orgId: 'org-1', orgName: 'Chennai', roleName: 'Accountant' },
    { orgId: 'org-2', orgName: 'Madurai', roleName: 'Viewer' },
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
      accessibleOrganizations: vi.fn().mockResolvedValue(orgs),
      activeOrgId: signal<string | null>('org-1'),
    };

    sessionContext = signal<SessionContext | null>({
      displayName: 'Praba Ravi',
      email: 'praba@example.com',
      branchName: 'Chennai',
      branchCode: 'CHN',
      licenseStatus: 'Active',
      licenseExpiry: null,
      expiryIsBranchLevel: false,
      permissions: [],
    });

    TestBed.configureTestingModule({
      providers: [
        // MenuService reads the tree over HTTP; the testing backend answers nothing.
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: SessionContextService, useValue: { context: sessionContext } },
      ],
    });
  });

  const createComponent = (): ShellNavComponent => {
    return TestBed.runInInjectionContext(() => new ShellNavComponent());
  };

  it('NAV-01: Instantiates correctly with default inputs', () => {
    const comp = createComponent();
    expect(comp).toBeDefined();
    expect(comp.userDisplayName()).toBe('Praba');
    expect(comp.userRoleName()).toBe('Owner');
  });

  it('NAV-02: Left rail navigation contains all expected core modules', () => {
    const comp = createComponent();
    const navItems = comp.nav();
    const paths = navItems.map((item) => item.path);

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

  it('NAV-03: CRITICAL UI RULE: Accounting module is labeled strictly as "Accounts"', () => {
    const comp = createComponent();
    const navItems = comp.nav();
    const accountingNav = navItems.find((i) => i.path === '/accounting');

    expect(accountingNav).toBeDefined();
    expect(accountingNav?.label).toBe('Accounts');
    expect(accountingNav?.label).not.toMatch(/accounting/i);
  });

  it('NAV-04: Role permission filtering excludes disallowed modules', () => {
    mockAuthService.canView.mockImplementation((mod: string) => mod !== 'sales');
    const comp = createComponent();

    const navItems = comp.nav();
    const paths = navItems.map((i) => i.path);

    expect(paths).not.toContain('/sales');
    expect(paths).toContain('/purchase');
    expect(paths).toContain('/accounting');
  });

  it('NAV-05: primaryNav and settingsItem are properly segregated', () => {
    const comp = createComponent();
    expect(comp.primaryNav().some((i) => i.path === '/settings')).toBe(false);
    expect(comp.settingsItem()?.path).toBe('/settings');
  });

  it('NAV-06: mobile tabs are Home, Purchase, Sales and Banking, as the design has them; More holds the rest', () => {
    const comp = createComponent();
    expect(comp.mobileTopNav().map((i) => i.label)).toEqual(['Home', 'Purchase', 'Sales', 'Banking']);
    expect(comp.mobileMoreNav().map((i) => i.label)).toEqual(['Contacts', 'Inventory', 'Accounts', 'Reports', 'Settings']);
  });

  it('NAV-06a: with the server\'s menu, where Home is module "dashboard", Home is still the first tab', () => {
    const serverRail: ShellRailItem[] = [
      { path: '/dashboard', label: 'Home', icon: 'house', module: 'dashboard' },
      { path: '/contacts', label: 'Contacts', icon: 'users-round', module: 'contacts' },
      { path: '/purchase/bills', label: 'Purchase', icon: 'package', module: 'purchase' },
      { path: '/sales/quotes', label: 'Sales', icon: 'shopping-cart', module: 'sales' },
      { path: '/banking', label: 'Banking', icon: 'landmark', module: 'banking' },
      { path: '/reports', label: 'Reports', icon: 'chart-no-axes-combined', module: 'reports' },
    ];
    TestBed.overrideProvider(MenuService, {
      useValue: { rail: signal(serverRail), menuForPath: () => null, menuByCode: () => null, rowCount: () => 0 },
    });
    const comp = createComponent();

    expect(comp.mobileTopNav().map((i) => i.label)).toEqual(['Home', 'Purchase', 'Sales', 'Banking']);
    expect(comp.mobileMoreNav().map((i) => i.label)).toEqual(['Contacts', 'Reports']);
  });

  it('NAV-06b: mobile tabs fall back to the first four when a design tab is not on the menu', () => {
    mockAuthService.canView.mockImplementation((mod: string) => mod !== 'banking');
    const comp = createComponent();
    expect(comp.mobileTopNav()).toEqual(comp.nav().slice(0, 4));
    expect(comp.mobileMoreNav().length).toBe(comp.nav().length - 4);
  });

  it('NAV-07: onLogout emits logout output', () => {
    const comp = createComponent();
    let emitted = false;
    comp.logout.subscribe(() => {
      emitted = true;
    });

    comp.onLogout();
    expect(emitted).toBe(true);
  });

  // ---- Account button and menu (TK-189) ----------------------------------

  it('NAV-08: the account button shows initials from the session name and the current branch role', async () => {
    const comp = createComponent();
    await Promise.resolve();
    await Promise.resolve();

    expect(comp.userName()).toBe('Praba Ravi');
    expect(comp.userInitials()).toBe('PR');
    expect(comp.userEmail()).toBe('praba@example.com');
    expect(comp.userRole()).toBe('Accountant');

    mockAuthService.activeOrgId.set('org-2');
    expect(comp.userRole()).toBe('Viewer');
  });

  it('NAV-09: before the session loads, the inputs stand in for name and role', () => {
    sessionContext.set(null);
    mockAuthService.accessibleOrganizations.mockResolvedValue([]);
    const comp = createComponent();

    expect(comp.userName()).toBe('Praba');
    expect(comp.userInitials()).toBe('PR');
    expect(comp.userRole()).toBe('Owner');
  });

  it('NAV-10: a one-word name gives its first two letters', () => {
    sessionContext.update((c) => (c ? { ...c, displayName: 'Jothi' } : c));
    const comp = createComponent();
    expect(comp.userInitials()).toBe('JO');
  });

  it('NAV-11: the account menu opens, and Escape, navigating or Logout close it', () => {
    const comp = createComponent();
    const click = { currentTarget: null } as unknown as MouseEvent;

    comp.toggleProfile(click);
    expect(comp.profileOpen()).toBe(true);
    comp.onEscape();
    expect(comp.profileOpen()).toBe(false);

    comp.toggleProfile(click);
    routerEvents$.next(new NavigationEnd(1, '/sales', '/sales'));
    expect(comp.profileOpen()).toBe(false);

    comp.toggleProfile(click);
    let emitted = false;
    comp.logout.subscribe(() => (emitted = true));
    comp.onLogout();
    expect(comp.profileOpen()).toBe(false);
    expect(emitted).toBe(true);
  });

  it('NAV-12: the menu sits level with the account button', () => {
    const comp = createComponent();
    const button = document.createElement('button');
    button.getBoundingClientRect = () => ({ bottom: window.innerHeight - 70 }) as DOMRect;

    comp.toggleProfile({ currentTarget: button } as unknown as MouseEvent);
    expect(comp.menuBottom()).toBe(70);
  });

  // ---- Active module highlighting (menu selection) -----------------------

  it('NAV-13: marks the owning module active when viewing a cross-module route (e.g. /settings/hsn-sac in Inventory)', () => {
    const comp = createComponent();
    routerEvents$.next(new NavigationEnd(1, '/settings/hsn-sac', '/settings/hsn-sac'));

    const inventoryItem = comp.nav().find((i) => i.label === 'Inventory');
    const settingsItem = comp.nav().find((i) => i.label === 'Settings');
    const homeItem = comp.nav().find((i) => i.label === 'Home');

    expect(inventoryItem).toBeDefined();
    expect(comp.isActive(inventoryItem!)).toBe(true);
    expect(comp.isActive(settingsItem!)).toBe(false);
    expect(comp.isActive(homeItem!)).toBe(false);
  });

  it('NAV-14: marks rail item active for standard module routes', () => {
    const comp = createComponent();
    routerEvents$.next(new NavigationEnd(1, '/inventory/items', '/inventory/items'));

    const inventoryItem = comp.nav().find((i) => i.label === 'Inventory');
    const contactsItem = comp.nav().find((i) => i.label === 'Contacts');

    expect(comp.isActive(inventoryItem!)).toBe(true);
    expect(comp.isActive(contactsItem!)).toBe(false);
  });

  it('NAV-15: updates active rail item on navigation', () => {
    const comp = createComponent();
    const inventoryItem = comp.nav().find((i) => i.label === 'Inventory')!;
    const contactsItem = comp.nav().find((i) => i.label === 'Contacts')!;

    routerEvents$.next(new NavigationEnd(1, '/inventory/categories', '/inventory/categories'));
    expect(comp.isActive(inventoryItem)).toBe(true);
    expect(comp.isActive(contactsItem)).toBe(false);

    routerEvents$.next(new NavigationEnd(2, '/contacts', '/contacts'));
    expect(comp.isActive(inventoryItem)).toBe(false);
    expect(comp.isActive(contactsItem)).toBe(true);
  });

  // ---- TK-189: picking on the rail, the More tab ---------------------------

  it('NAV-16: A rail click goes through the shared navigation rule', () => {
    const comp = createComponent();
    const navigation = TestBed.inject(ShellNavigationService);
    const pick = vi.spyOn(navigation, 'pickRail').mockReturnValue(false);
    const sales = comp.nav().find((i) => i.label === 'Sales')!;
    const event = new MouseEvent('click');

    comp.toggleProfile({ currentTarget: null } as unknown as MouseEvent);
    comp.pick(sales, event);

    expect(pick).toHaveBeenCalledWith(sales, event);
    expect(comp.profileOpen()).toBe(false);
  });

  it('NAV-17: More is marked while the screen is in a module that is not a tab', () => {
    const comp = createComponent();
    routerEvents$.next(new NavigationEnd(1, '/settings/users', '/settings/users'));
    expect(comp.moreActive()).toBe(true);

    routerEvents$.next(new NavigationEnd(2, '/sales/quotes', '/sales/quotes'));
    expect(comp.moreActive()).toBe(false);
  });

  it('NAV-18: Only a tap on the dim area closes More, not one on the sheet', () => {
    const comp = createComponent();
    const navigation = TestBed.inject(ShellNavigationService);
    navigation.moreOpen.set(true);
    const backdrop = document.createElement('div');
    const sheet = document.createElement('div');

    comp.onMoreBackdrop({ target: sheet, currentTarget: backdrop } as unknown as MouseEvent);
    expect(navigation.moreOpen()).toBe(true);

    comp.onMoreBackdrop({ target: backdrop, currentTarget: backdrop } as unknown as MouseEvent);
    expect(navigation.moreOpen()).toBe(false);
  });
});

