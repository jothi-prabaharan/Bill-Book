import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { MenuService } from './menu.service';
import { MenuView } from './menu.models';

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
});
