import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
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
      providers: [
        // The strip asks MenuService whether this screen has a secondary menu, and
        // that service fetches one. A testing backend rather than a real one: these
        // specs are about crumbs, not about what the server returns.
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: mockRouter },
      ],
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

  it('CRUMB-07: Home and register controls follow the route, not a manual flag', () => {
    const comp = createComponent();
    // The mock router starts on /dashboard, so the board controls are on and
    // the register controls are off.
    expect(comp.isHome()).toBe(true);
    expect(comp.isRegister()).toBe(false);

    routerEvents$.next(new NavigationEnd(1, '/sales', '/sales'));
    expect(comp.isHome()).toBe(false);
    expect(comp.isRegister()).toBe(true);

    // A create form is not a register: nothing there is exportable.
    routerEvents$.next(new NavigationEnd(2, '/sales/invoices/new', '/sales/invoices/new'));
    expect(comp.isRegister()).toBe(false);
  });

  it('CRUMB-09: Export menu opens, emits the chosen extension, and closes behind it', () => {
    const comp = createComponent();
    routerEvents$.next(new NavigationEnd(1, '/sales', '/sales'));

    let emitted: string | null = null;
    comp.openExport.subscribe((ext) => {
      emitted = ext;
    });

    expect(comp.exportMenuOpen()).toBe(false);
    comp.toggleExportMenu();
    expect(comp.exportMenuOpen()).toBe(true);

    comp.pickExport({ label: 'Excel workbook', ext: 'xlsx' });
    expect(emitted).toBe('xlsx');
    expect(comp.exportMenuOpen()).toBe(false);
  });

  it('CRUMB-10: Navigating closes an open export menu', () => {
    const comp = createComponent();
    routerEvents$.next(new NavigationEnd(1, '/sales', '/sales'));
    comp.toggleExportMenu();
    expect(comp.exportMenuOpen()).toBe(true);

    routerEvents$.next(new NavigationEnd(2, '/purchase', '/purchase'));
    expect(comp.exportMenuOpen()).toBe(false);
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
