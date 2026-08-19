import { TestBed } from '@angular/core/testing';
import { Router, NavigationEnd, Event as RouterEvent } from '@angular/router';
import { Subject } from 'rxjs';
import { describe, expect, it, beforeEach } from 'vitest';
import { ShellBreadcrumbComponent, BreadcrumbItem } from './shell-breadcrumb.component';

describe('ShellBreadcrumbComponent (libs/app-shell)', () => {
  let routerEvents$: Subject<RouterEvent>;
  let mockRouter: Partial<Router>;

  beforeEach(() => {
    routerEvents$ = new Subject<RouterEvent>();
    mockRouter = {
      url: '/dashboard',
      events: routerEvents$.asObservable(),
    };

    TestBed.configureTestingModule({
      providers: [{ provide: Router, useValue: mockRouter }],
    });
  });

  const createComponent = (): ShellBreadcrumbComponent => {
    return TestBed.runInInjectionContext(() => new ShellBreadcrumbComponent());
  };

  it('CRUMB-01: Instantiates with empty crumbs on dashboard', () => {
    const comp = createComponent();
    expect(comp).toBeDefined();
    expect(comp.crumbs()).toEqual([]);
  });

  it('CRUMB-02: Derives breadcrumb trail for multi-level route', () => {
    const comp = createComponent();
    comp.updateCrumbs('/sales/invoices/new');

    const crumbs = comp.crumbs();
    expect(crumbs.length).toBe(3);
    expect(crumbs[0]).toEqual({ label: 'Sales', path: '/sales', isLink: true, isLast: false });
    expect(crumbs[1]).toEqual({ label: 'Invoices', path: '/sales/invoices', isLink: true, isLast: false });
    expect(crumbs[2]).toEqual({ label: 'New', path: '/sales/invoices/new', isLink: false, isLast: true });
  });

  it('CRUMB-03: CRITICAL UI RULE: /accounting route generates crumb labeled strictly "Accounts"', () => {
    const comp = createComponent();
    comp.updateCrumbs('/accounting/trial-balance');

    const crumbs = comp.crumbs();
    expect(crumbs.length).toBe(2);
    expect(crumbs[0].label).toBe('Accounts');
    expect(crumbs[0].label).not.toMatch(/accounting/i);
    expect(crumbs[1].label).toBe('Trial balance');
  });

  it('CRUMB-04: Special abbreviation "coa" expands to "Chart of Accounts"', () => {
    const comp = createComponent();
    comp.updateCrumbs('/accounting/coa');

    const crumbs = comp.crumbs();
    expect(crumbs.length).toBe(2);
    expect(crumbs[0].label).toBe('Accounts');
    expect(crumbs[1].label).toBe('Chart of Accounts');
  });

  it('CRUMB-05: Hyphenated words are formatted cleanly', () => {
    const comp = createComponent();
    comp.updateCrumbs('/inventory/stock-adjustments');

    const crumbs = comp.crumbs();
    expect(crumbs.length).toBe(2);
    expect(crumbs[0].label).toBe('Inventory');
    expect(crumbs[1].label).toBe('Stock adjustments');
  });

  it('CRUMB-06: NavigationEnd events update breadcrumbs dynamically', () => {
    const comp = createComponent();

    routerEvents$.next(new NavigationEnd(1, '/contacts', '/contacts'));
    expect(comp.crumbs().length).toBe(1);
    expect(comp.crumbs()[0].label).toBe('Contacts');
  });

  it.skip('CRUMB-07: Dashboard basis toggle works seamlessly', () => {
    const comp = createComponent();
    // Set isHome input to true for dashboard route
    comp.isHome.set(true);
    expect(comp.base()).toBe(false);
    expect(comp.baseLabel()).toBe('Accrual basis');

    comp.onToggleBase();
    expect(comp.base()).toBe(true);
    expect(comp.baseLabel()).toBe('Cash basis');

    comp.onToggleBase();
    expect(comp.base()).toBe(false);
    expect(comp.baseLabel()).toBe('Accrual basis');
  });

  it('CRUMB-08: onCrumbClicked emits crumbClick output', () => {
    const comp = createComponent();
    let clickedItem: BreadcrumbItem | null = null;
    comp.crumbClick.subscribe((item) => {
      clickedItem = item;
    });

    const item = { label: 'Sales', path: '/sales', isLink: true, isLast: false };
    comp.onCrumbClicked(item);
    expect(clickedItem).toEqual(item);
  });
});
