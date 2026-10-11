import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { NavigationEnd, Router, Event as RouterEvent } from '@angular/router';
import { Subject, of } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { MenuService } from './menu.service';
import { MenuView } from './menu.models';
import { ShellPanelService } from './panel-state.service';
import { ShellNavigationService } from './shell-navigation.service';
import { ShellRailItem } from './shell-screens';

const row = (subMenuId: number, code: string, name: string, routePath: string) => ({
  subMenuId,
  code,
  name,
  routePath,
  icon: null,
  module: 'x',
  canCreate: false,
  singularName: null,
  hasAccess: true,
  allowedActions: ['view'],
});

const TREE: MenuView[] = [
  { menuId: 1, code: 'home', name: 'Home', icon: 'house', module: 'dashboard', routePath: '/dashboard', isSearchable: false, groups: [] },
  {
    menuId: 2, code: 'sales', name: 'Sales', icon: 'shopping-cart', module: 'sales', routePath: null, isSearchable: false,
    groups: [{ menuGroupId: 1, code: 'sales-g1', name: null, icon: null, subMenus: [row(1, 'qot', 'Quotes', '/sales/quotes'), row(2, 'inv', 'Invoices', '/sales/invoices')] }],
  },
  {
    menuId: 3, code: 'settings', name: 'Settings', icon: 'settings', module: 'settings', routePath: null, isSearchable: false,
    groups: [
      { menuGroupId: 2, code: 'settings-g1', name: 'Organisation', icon: null, subMenus: [row(3, 'org', 'Organisation profile', '/settings/organization')] },
      { menuGroupId: 3, code: 'settings-g2', name: 'Users and access', icon: null, subMenus: [row(4, 'usr', 'Users', '/settings/users')] },
    ],
  },
  {
    menuId: 4, code: 'approvals', name: 'Approvals', icon: 'check-circle', module: '', routePath: null, isSearchable: false,
    groups: [{ menuGroupId: 4, code: 'approvals-g1', name: null, icon: null, subMenus: [row(5, 'inbox', 'Waiting for me', '/approvals')] }],
  },
];

const item = (code: string, path: string, label: string, module: string | null): ShellRailItem => ({ code, path, label, icon: '', module });
const SALES = item('sales', '/sales/quotes', 'Sales', 'sales');
const SETTINGS = item('settings', '/settings/organization', 'Settings', 'settings');
const HOME = item('home', '/dashboard', 'Home', 'dashboard');
const APPROVALS = item('approvals', '/approvals', 'Approvals', null);

const click = (over: Partial<MouseEvent> = {}): MouseEvent =>
  ({ button: 0, ctrlKey: false, metaKey: false, shiftKey: false, altKey: false, preventDefault: vi.fn(), ...over }) as unknown as MouseEvent;

describe('ShellNavigationService (libs/app-shell, TK-189)', () => {
  let routerEvents$: Subject<RouterEvent>;
  let router: { url: string; events: Subject<RouterEvent>['asObservable'] extends () => infer O ? O : never; navigateByUrl: ReturnType<typeof vi.fn> };

  const create = async (url: string, load = true): Promise<ShellNavigationService> => {
    TestBed.resetTestingModule();
    routerEvents$ = new Subject<RouterEvent>();
    router = { url, events: routerEvents$.asObservable(), navigateByUrl: vi.fn().mockResolvedValue(true) };
    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: router },
        { provide: HttpClient, useValue: { get: vi.fn().mockReturnValue(of(TREE)) } },
      ],
    });
    if (load) await TestBed.inject(MenuService).load();
    return TestBed.inject(ShellNavigationService);
  };

  beforeEach(() => {
    localStorage.removeItem('billbook.subpanel.collapsed.v1');
  });

  it('NAVS-01: The menu follows the screen until a module is picked', async () => {
    const nav = await create('/sales/invoices');
    expect(nav.panelMenu()?.code).toBe('sales');
    expect(nav.activeCode()).toBe('sales');
    expect(nav.isCurrent(SALES)).toBe(true);
    expect(nav.isCurrent(SETTINGS)).toBe(false);
  });

  it('NAVS-02: Picking another module opens its menu and marks it, without navigating', async () => {
    const nav = await create('/sales/invoices');
    const event = click();

    expect(nav.pickRail(SETTINGS, event)).toBe(false);
    expect(event.preventDefault).toHaveBeenCalled();
    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(nav.panelMenu()?.code).toBe('settings');
    expect(nav.isCurrent(SETTINGS)).toBe(true);
    expect(nav.isCurrent(SALES)).toBe(false);
  });

  it('NAVS-03: Picking the open module hides its menu, and again shows it', async () => {
    const nav = await create('/sales/invoices');
    const panel = TestBed.inject(ShellPanelService);

    nav.pickRail(SALES, click());
    expect(panel.collapsed()).toBe(true);
    expect(nav.panelShown()).toBe(false);

    nav.pickRail(SALES, click());
    expect(panel.collapsed()).toBe(false);
    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('NAVS-04: A hidden menu comes back when another module is picked', async () => {
    const nav = await create('/sales/invoices');
    const panel = TestBed.inject(ShellPanelService);
    panel.toggle();
    expect(panel.collapsed()).toBe(true);

    nav.pickRail(SETTINGS, click());
    expect(panel.collapsed()).toBe(false);
  });

  it('NAVS-05: A module with no menu, or a single screen, goes straight there', async () => {
    const nav = await create('/sales/invoices');

    nav.pickRail(HOME, click());
    expect(router.navigateByUrl).toHaveBeenCalledWith('/dashboard');

    nav.pickRail(APPROVALS, click());
    expect(router.navigateByUrl).toHaveBeenCalledWith('/approvals');
  });

  it('NAVS-06: A modified or middle click is left to the browser', async () => {
    const nav = await create('/sales/invoices');
    for (const event of [click({ ctrlKey: true }), click({ metaKey: true }), click({ shiftKey: true }), click({ button: 1 })]) {
      expect(nav.pickRail(SETTINGS, event)).toBe(true);
      expect(event.preventDefault).not.toHaveBeenCalled();
    }
    expect(nav.panelMenu()?.code).toBe('sales');
  });

  it('NAVS-07: Navigating hands the menu back to the screen and closes the sheets', async () => {
    const nav = await create('/sales/invoices');
    nav.pickRail(SETTINGS, click());
    nav.sheetOpen.set(true);
    nav.moreOpen.set(true);

    routerEvents$.next(new NavigationEnd(1, '/sales/quotes', '/sales/quotes'));
    expect(nav.panelMenu()?.code).toBe('sales');
    expect(nav.sheetOpen()).toBe(false);
    expect(nav.moreOpen()).toBe(false);
  });

  it('NAVS-08: On a phone a tab opens its sheet, and the menu button toggles the sheet', async () => {
    const nav = await create('/dashboard');
    nav.isPhone.set(true);

    nav.pickRail(SALES, click());
    expect(nav.sheetOpen()).toBe(true);
    expect(nav.panelMenu()?.code).toBe('sales');
    expect(router.navigateByUrl).not.toHaveBeenCalled();

    nav.togglePanel();
    expect(nav.sheetOpen()).toBe(false);
    expect(TestBed.inject(ShellPanelService).collapsed()).toBe(false);
  });

  it('NAVS-09: Back from a module that is not a tab returns to More; from a tab it just closes', async () => {
    const nav = await create('/dashboard');
    nav.isPhone.set(true);

    nav.pickRail(SETTINGS, click());
    nav.closeSheet();
    expect(nav.sheetOpen()).toBe(false);
    expect(nav.moreOpen()).toBe(true);

    nav.closeMore();
    nav.pickRail(SALES, click());
    nav.closeSheet();
    expect(nav.moreOpen()).toBe(false);
  });

  it('NAVS-10: Before the tree loads, the static registry says which module a screen is under', async () => {
    const nav = await create('/settings/hsn-sac', false);
    expect(nav.isCurrent(item('inventory', '/inventory', 'Inventory', 'inventory'))).toBe(true);
    expect(nav.isCurrent(item('settings', '/settings', 'Settings', 'settings'))).toBe(false);
  });
});
