import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { Router, Event as RouterEvent, NavigationEnd } from '@angular/router';
import { Subject, of } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { ShellSubpanelComponent } from './shell-subpanel.component';
import { MenuService } from '../menu.service';
import { ShellPanelService } from '../panel-state.service';
import { MenuView } from '../menu.models';

const sub = (over: Partial<MenuView['groups'][number]['subMenus'][number]> = {}) => ({
  subMenuId: 1,
  code: 'x',
  name: 'X',
  routePath: '/x',
  icon: null,
  module: 'sales',
  canCreate: false,
  singularName: null,
  hasAccess: true,
  allowedActions: ['view'],
  ...over,
});

const TREE: MenuView[] = [
  {
    menuId: 5,
    code: 'sales',
    name: 'Sales',
    icon: 'shopping-cart',
    module: 'sales',
    routePath: null,
    isSearchable: false,
    groups: [
      {
        menuGroupId: 4,
        code: 'sales-g1',
        name: null,
        icon: null,
        subMenus: [
          sub({ subMenuId: 19, code: 'all', name: 'All transactions', routePath: '/sales/transactions' }),
          sub({ subMenuId: 20, code: 'qot', name: 'Quotes', routePath: '/sales/transactions?type=Quote' }),
          sub({
            subMenuId: 22,
            code: 'inv',
            name: 'Invoices',
            routePath: '/sales/invoices',
            canCreate: true,
            singularName: 'Invoice',
            allowedActions: ['view', 'create'],
          }),
        ],
      },
    ],
  },
  {
    menuId: 9,
    code: 'settings',
    name: 'Settings',
    icon: 'settings',
    module: 'settings',
    routePath: null,
    isSearchable: true,
    groups: [
      {
        menuGroupId: 14,
        code: 'settings-g1',
        name: 'Organisation',
        icon: 'building-2',
        subMenus: [sub({ subMenuId: 90, code: 'org', name: 'Organisation profile', routePath: '/settings/organization', module: 'settings' })],
      },
      {
        menuGroupId: 15,
        code: 'settings-g2',
        name: 'Users and access',
        icon: 'users-round',
        subMenus: [sub({ subMenuId: 95, code: 'usr', name: 'Users', routePath: '/settings/users', module: 'settings' })],
      },
    ],
  },
];

describe('ShellSubpanelComponent (libs/app-shell)', () => {
  let routerEvents$: Subject<RouterEvent>;
  let mockRouter: Partial<Router>;

  const create = async (url: string): Promise<ShellSubpanelComponent> => {
    // Some specs build a second panel on another URL; the module has to be torn
    // down first or configureTestingModule refuses.
    TestBed.resetTestingModule();
    routerEvents$ = new Subject<RouterEvent>();
    mockRouter = {
      url,
      events: routerEvents$.asObservable(),
      navigateByUrl: vi.fn().mockResolvedValue(true),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: HttpClient, useValue: { get: vi.fn().mockReturnValue(of(TREE)) } },
      ],
    });

    await TestBed.inject(MenuService).load();
    return TestBed.runInInjectionContext(() => new ShellSubpanelComponent());
  };

  beforeEach(() => {
    localStorage.removeItem('billbook.subpanel.collapsed.v1');
    localStorage.removeItem('billbook.subpanel.width.v1');
  });

  it('PANEL-01: Shows the module the current URL belongs to', async () => {
    const comp = await create('/sales/invoices');

    expect(comp.label()).toBe('Sales');
    expect(comp.count()).toBe(3);
    expect(comp.visible()).toBe(true);
  });

  it('PANEL-02: Follows navigation into another module', async () => {
    const comp = await create('/sales/invoices');

    routerEvents$.next(new NavigationEnd(1, '/settings/users', '/settings/users'));

    expect(comp.label()).toBe('Settings');
    expect(comp.count()).toBe(2);
  });

  it('PANEL-03: The row for the current screen is marked, others are not', async () => {
    const comp = await create('/sales/invoices');
    const rows = comp.groups()[0].subMenus;

    expect(comp.isCurrent(rows[2])).toBe(true);
    expect(comp.isCurrent(rows[0])).toBe(false);
  });

  it('PANEL-04: Exactly one row is ever current, even where rows share a path', async () => {
    // The mixed register and every typed one live at /sales/transactions and differ
    // only by their query. Stripping the query to compare marked all of them.
    const onMixed = await create('/sales/transactions');
    expect(onMixed.groups()[0].subMenus.filter((s) => onMixed.isCurrent(s)).map((s) => s.code))
      .toEqual(['all']);

    const onTyped = await create('/sales/transactions?type=Quote');
    expect(onTyped.groups()[0].subMenus.filter((s) => onTyped.isCurrent(s)).map((s) => s.code))
      .toEqual(['qot']);

    const onOwnPath = await create('/sales/invoices');
    expect(onOwnPath.groups()[0].subMenus.filter((s) => onOwnPath.isCurrent(s)).map((s) => s.code))
      .toEqual(['inv']);
  });

  it('PANEL-05: An unnamed section is always open; a named one collapses', async () => {
    const sales = await create('/sales/invoices');
    expect(sales.isOpen(sales.groups()[0])).toBe(true);

    const settings = await create('/settings/users');
    const panel = TestBed.inject(ShellPanelService);
    panel.openGroup.set(null);

    const [organisation, users] = settings.groups();
    expect(settings.isOpen(organisation)).toBe(false);

    settings.toggleGroup(users);
    expect(settings.isOpen(users)).toBe(true);
    expect(settings.isOpen(organisation)).toBe(false);

    // One at a time: opening the other shuts the first.
    settings.toggleGroup(organisation);
    expect(settings.isOpen(users)).toBe(false);
  });

  it('PANEL-06: Landing on a screen opens the section holding it', async () => {
    const comp = await create('/settings/users');
    expect(comp.isOpen(comp.groups()[1])).toBe(true);
  });

  it('PANEL-07: Search narrows rows, drops empty sections and opens what is left', async () => {
    const comp = await create('/settings/users');

    comp.setQuery('users');
    expect(comp.groups().map((g) => g.code)).toEqual(['settings-g2']);
    expect(comp.isOpen(comp.groups()[0])).toBe(true);

    comp.setQuery('@@@nothing@@@');
    expect(comp.count()).toBe(0);
  });

  it('PANEL-08: Navigating clears the search', async () => {
    const comp = await create('/settings/users');
    comp.setQuery('users');

    routerEvents$.next(new NavigationEnd(1, '/settings/organization', '/settings/organization'));
    expect(comp.count()).toBe(2);
  });

  it('PANEL-09: Create is offered only where the role holds it, and opens the form', async () => {
    const comp = await create('/sales/invoices');
    const [all, , invoices] = comp.groups()[0].subMenus;

    expect(comp.canCreate(all)).toBe(false);
    expect(comp.canCreate(invoices)).toBe(true);
    expect(comp.createLabel(invoices)).toBe('New invoice');

    comp.create(invoices, new Event('click'));
    expect(mockRouter.navigateByUrl).toHaveBeenCalledWith('/sales/invoices/new');
  });

  it('PANEL-10: Collapsing hides the panel without losing its contents', async () => {
    const comp = await create('/sales/invoices');
    const panel = TestBed.inject(ShellPanelService);

    panel.toggle();
    expect(comp.visible()).toBe(false);
    expect(comp.count()).toBe(3);

    panel.toggle();
    expect(comp.visible()).toBe(true);
  });

  it('PANEL-11: Width is clamped to what can be dragged back', async () => {
    await create('/sales/invoices');
    const panel = TestBed.inject(ShellPanelService);

    panel.setWidth(10);
    expect(panel.width()).toBe(196);

    panel.setWidth(9000);
    expect(panel.width()).toBe(430);

    panel.setWidth(300);
    expect(panel.widthPx()).toBe('300px');
  });
});
