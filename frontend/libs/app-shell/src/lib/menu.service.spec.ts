import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { MenuService, buildFallbackMenu } from './menu.service';
import { FALLBACK_RAIL } from './shell-screens';
import { MenuView, SubMenuView } from './menu.models';

const TREE: MenuView[] = [
  {
    menuId: 1,
    code: 'home',
    name: 'Home',
    icon: 'house',
    module: 'dashboard',
    routePath: '/dashboard',
    isSearchable: false,
    groups: [],
  },
  {
    menuId: 2,
    code: 'sales',
    name: 'Sales',
    icon: 'shopping-cart',
    module: 'sales',
    routePath: null,
    isSearchable: false,
    groups: [
      {
        menuGroupId: 1,
        code: 'sales-g1',
        name: null,
        icon: null,
        subMenus: [
          {
            subMenuId: 1,
            code: 'all',
            name: 'All transactions',
            routePath: '/sales/transactions',
            icon: null,
            module: 'sales',
            canCreate: false,
            singularName: null,
            hasAccess: true,
            allowedActions: ['view', 'export'],
          },
          {
            subMenuId: 2,
            code: 'inv',
            name: 'Invoices',
            routePath: '/sales/invoices',
            icon: null,
            module: 'sales',
            canCreate: true,
            singularName: 'Invoice',
            hasAccess: true,
            allowedActions: ['view', 'create', 'edit'],
          },
        ],
      },
    ],
  },
];

describe('MenuService (libs/app-shell)', () => {
  let http: { get: ReturnType<typeof vi.fn> };

  const create = (): MenuService => {
    TestBed.configureTestingModule({
      providers: [{ provide: HttpClient, useValue: http }],
    });
    return TestBed.runInInjectionContext(() => new MenuService());
  };

  beforeEach(() => {
    http = { get: vi.fn().mockReturnValue(of(TREE)) };
  });

  it('MENU-01: Loads the tree and marks itself loaded', async () => {
    const service = create();
    expect(service.loaded()).toBe(false);

    await service.load();

    expect(http.get).toHaveBeenCalledWith('/api/menu');
    expect(service.loaded()).toBe(true);
    expect(service.usingFallback()).toBe(false);
    expect(service.tree().length).toBe(2);
  });

  it('MENU-02: A module with its own route keeps it; one without lands on its first screen', async () => {
    const service = create();
    await service.load();

    const rail = service.rail();
    expect(rail.map((r) => r.path)).toEqual(['/dashboard', '/sales/transactions']);
    expect(rail.map((r) => r.icon)).toEqual(['house', 'shopping-cart']);
    expect(rail[1].label).toBe('Sales');
  });

  it('MENU-03: Screens flatten across groups, direct routes included', async () => {
    const service = create();
    await service.load();

    expect(service.screens().map((s) => s.path)).toEqual([
      '/dashboard',
      '/sales/transactions',
      '/sales/invoices',
    ]);
    expect(service.screens().every((s) => !!s.label)).toBe(true);
  });

  it('MENU-04: allows() answers from the actions the server granted', async () => {
    const service = create();
    await service.load();

    expect(service.allows('/sales/invoices', 'create')).toBe(true);
    expect(service.allows('/sales/invoices', 'delete')).toBe(false);
    expect(service.allows('/sales/transactions', 'create')).toBe(false);

    // A screen the tree has never heard of is not a permission to invent.
    expect(service.allows('/somewhere/else', 'view')).toBe(false);
  });

  it('MENU-05: A query string does not stop a route being recognised', async () => {
    const service = create();
    await service.load();

    expect(service.subMenuFor('/sales/invoices?status=draft')?.code).toBe('inv');
  });

  it('MENU-06: A failed call falls back rather than leaving an empty rail', async () => {
    http.get = vi.fn().mockReturnValue(throwError(() => new Error('offline')));
    const service = create();

    await service.load();

    expect(service.loaded()).toBe(true);
    expect(service.usingFallback()).toBe(true);
    expect(service.rail().length).toBeGreaterThan(0);
    expect(service.screens().length).toBeGreaterThan(0);
  });

  it('MENU-07: An empty response is treated as unusable, not as an empty menu', async () => {
    http.get = vi.fn().mockReturnValue(of([]));
    const service = create();

    await service.load();

    expect(service.usingFallback()).toBe(true);
    expect(service.rail().length).toBeGreaterThan(0);
  });

  // ---- TK-189: codes, row lookup and the breadcrumb trail -------------------

  it('MENU-08: Each rail entry carries the code of the menu it opens', async () => {
    const service = create();
    await service.load();

    expect(service.rail().map((r) => r.code)).toEqual(['home', 'sales']);
  });

  it('MENU-09: The fallback rail still draws its icons', async () => {
    http.get = vi.fn().mockReturnValue(throwError(() => new Error('offline')));
    const service = create();
    await service.load();

    const sales = service.rail().find((r) => r.label === 'Sales');
    expect(sales?.icon).toBe('shopping-cart');
    expect(service.rail().every((r) => !!r.icon)).toBe(true);
  });

  it('MENU-10: A menu is found by its code, and counts its screens', async () => {
    const service = create();
    await service.load();

    const sales = service.menuByCode('sales');
    expect(sales?.name).toBe('Sales');
    expect(service.rowCount(sales!)).toBe(2);
    expect(service.menuByCode('nowhere')).toBeNull();
  });

  it('MENU-11: A form or record belongs to the row it sits under', async () => {
    const service = create();
    await service.load();

    expect(service.rowForPath('/sales/invoices')?.code).toBe('inv');
    expect(service.rowForPath('/sales/invoices/new')?.code).toBe('inv');
    expect(service.rowForPath('/sales/invoices/42/edit')?.code).toBe('inv');
    expect(service.rowForPath('/sales/transactions/9')?.code).toBe('all');
    expect(service.rowForPath('/nowhere/at/all')).toBeNull();
  });

  it('MENU-12: The breadcrumb is module, screen, then the form or record', async () => {
    const service = create();
    await service.load();

    const labels = (url: string) => service.crumbsFor(url)?.map((c) => c.label);

    expect(service.crumbsFor('/dashboard')).toEqual([]);
    expect(service.crumbsFor('/sales/invoices')).toEqual([
      { label: 'Sales', path: '/sales/transactions', isLink: true, isLast: false },
      { label: 'Invoices', path: '/sales/invoices', isLink: false, isLast: true },
    ]);
    expect(labels('/sales/invoices/new')).toEqual(['Sales', 'Invoices', 'New invoice']);
    expect(service.crumbsFor('/sales/invoices/new')?.[1].isLink).toBe(true);
    expect(labels('/sales/invoices/42')).toEqual(['Sales', 'Invoices', 'Invoice']);
    expect(labels('/sales/invoices/42/edit')).toEqual(['Sales', 'Invoices', 'Edit invoice']);
    expect(labels('/sales/transactions/9')).toEqual(['Sales', 'All transactions', '9']);
  });

  it('MENU-13: A URL the tree does not know gets no trail, so the caller spells out the path', async () => {
    const service = create();
    await service.load();

    expect(service.crumbsFor('/nowhere/at/all')).toBeNull();
  });

  it('MENU-14: The + opens a form: its own page, the list asked to open one, or nothing', () => {
    const service = create();
    const sub = (code: string, routePath: string | null): SubMenuView => ({
      subMenuId: 0,
      code,
      name: code,
      routePath,
      icon: null,
      module: 'x',
      canCreate: true,
      singularName: null,
      hasAccess: true,
      allowedActions: ['view', 'create'],
    });

    expect(service.createPath(sub('inv', '/sales/invoices'))).toBe('/sales/invoices/new');
    expect(service.createPath(sub('both', '/contacts'))).toBe('/contacts/new');
    expect(service.createPath(sub('ven', null))).toBe('/contacts/new?role=vendor');
    expect(service.createPath(sub('itm', '/inventory/items'))).toBe('/inventory/items?action=create');
    expect(service.createPath(sub('rin', '/sales/recurring-invoices?status=active'))).toBe('/sales/recurring-invoices?action=create');
    // No form for one record behind these, so no + at all.
    expect(service.createPath(sub('fdm', '/fee/demands'))).toBeNull();
    expect(service.createPath(sub('rst-recipes', '/restaurant/recipes'))).toBeNull();
    expect(service.createPath(sub('xyz', null))).toBeNull();
  });

  /** The server's nine core rail codes, as `MenuSeed.cs` names them. */
  const SERVER_CORE_CODES = ['home', 'contacts', 'inventory', 'purchase', 'sales', 'banking', 'accounting', 'reports', 'settings'];

  it("MENU-15: The fallback tree's module codes are the server's, so a module picked on one is the same module on the other (TK-205)", () => {
    // Accounts' was `accounts`, made from its group name; the server's is `accounting`.
    expect(FALLBACK_RAIL.map((rail) => rail.code)).toEqual(SERVER_CORE_CODES);

    const fallback = buildFallbackMenu();
    expect(fallback.map((menu) => menu.code).sort()).toEqual([...SERVER_CORE_CODES].sort());
    for (const menu of fallback) {
      expect(FALLBACK_RAIL.find((rail) => rail.label === menu.name)?.code).toBe(menu.code);
    }
  });
});

