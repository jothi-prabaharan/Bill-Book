import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, NavigationEnd, Event as RouterEvent } from '@angular/router';
import { Subject } from 'rxjs';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { ShellComponent } from './shell.component';
import { AuthService } from '@bill-book/auth';
import { ElementRef } from '@angular/core';
import { ShellBoardService } from '../board-state.service';
import { FavouritesService } from '../favourites.service';

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
      accessibleOrganizations: vi.fn().mockResolvedValue([]),
      switchOrganization: vi.fn().mockResolvedValue(undefined),
      logout: vi.fn(),
      // signOut, not logout: signing out revokes the token family server-side
      // as well as clearing local storage.
      signOut: vi.fn().mockResolvedValue(undefined),
    };

    mockElementRef = new ElementRef(document.createElement('div'));

    TestBed.configureTestingModule({
      providers: [
        // The shell fetches the branch's display formats on boot, so it needs an
        // HttpClient. Testing backend rather than a real one: these specs assert
        // navigation and labels, and a stray request to a live backend would make
        // them depend on something none of them are about.
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: ElementRef, useValue: mockElementRef },
      ],
    });

    localStorage.setItem('bb.orgId', 'org-1');
    localStorage.removeItem('billbook.favourites.v1');
  });

  const createComponent = (): ShellComponent => {
    return TestBed.runInInjectionContext(() => new ShellComponent());
  };

  describe('Tier 1: Feature Coverage (R2 & R5 Specification)', () => {
    it('SHELL-T1-01: Component instantiates', () => {
      const comp = createComponent();
      expect(comp).toBeDefined();
    });

    it('SHELL-T1-02: Left rail navigation contains all expected core modules', () => {
      const comp = createComponent();
      const paths = comp.nav().map((item) => item.path);

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
      const accountingNav = comp.nav().find((i) => i.path === '/accounting');

      expect(accountingNav).toBeDefined();
      expect(accountingNav?.label).toBe('Accounts');
      expect(accountingNav?.label).not.toBe('Accounting');
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

    it('SHELL-T2-08: Role permission restriction filters out inaccessible module links', () => {
      // Mock user without sales permission
      mockAuthService.canView.mockImplementation((mod: string) => mod !== 'sales');
      const comp = createComponent();

      const paths = comp.nav().map((item) => item.path);

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

    it('SHELL-T3-03: Board basis toggle alternates between Accrual basis and Cash basis', () => {
      const comp = createComponent();
      const board = TestBed.inject(ShellBoardService);
      board.base.set(false);

      expect(comp.base()).toBe(false);
      expect(comp.baseLabel()).toBe('Accrual basis');

      comp.toggleBase();
      expect(comp.base()).toBe(true);
      expect(comp.baseLabel()).toBe('Cash basis');

      comp.toggleBase();
      expect(comp.base()).toBe(false);
      expect(comp.baseLabel()).toBe('Accrual basis');
    });

    it('SHELL-T3-04: Customize mode and Reset are relayed to the board service', () => {
      const comp = createComponent();
      const board = TestBed.inject(ShellBoardService);
      board.stopEdit();
      const resetsBefore = board.resetCount();

      comp.startEdit();
      expect(board.editing()).toBe(true);
      expect(comp.editing()).toBe(true);

      comp.resetLayout();
      expect(board.resetCount()).toBe(resetsBefore + 1);

      comp.stopEdit();
      expect(board.editing()).toBe(false);
    });

    it('SHELL-T3-05: Only a screen the chrome knows about can be starred', () => {
      const comp = createComponent();
      const favourites = TestBed.inject(FavouritesService);

      routerEvents$.next(new NavigationEnd(1, '/sales/invoices', '/sales/invoices'));
      expect(comp.currentIsStarrable()).toBe(true);
      expect(comp.currentIsStarred()).toBe(false);

      comp.toggleStar();
      expect(comp.currentIsStarred()).toBe(true);
      expect(favourites.starred()).toContain('/sales/invoices');

      comp.toggleStar();
      expect(comp.currentIsStarred()).toBe(false);

      // A document being edited is not a screen in the registry.
      routerEvents$.next(new NavigationEnd(2, '/sales/invoices/4021', '/sales/invoices/4021'));
      expect(comp.currentIsStarrable()).toBe(false);
    });
  });

  describe('Tier 4: Real-World Shell Workflows', () => {
    it('SHELL-T4-02: Full breadcrumb navigation trail lifecycle across multi-module walkthrough', () => {
      const comp = createComponent();

      // Step 1: Start on dashboard
      comp.updateCrumbs('/dashboard');
      expect(comp.crumbs()).toEqual([]);

      // Step 2: Navigate to Sales List
      comp.updateCrumbs('/sales/transactions');
      expect(comp.crumbs().map((c) => c.label)).toEqual(['Sales', 'Transactions']);

      // Step 3: Drill down into Invoice edit
      comp.updateCrumbs('/sales/invoices/INV-2026-0042');
      expect(comp.crumbs().map((c) => c.label)).toEqual(['Sales', 'Invoices', 'INV 2026 0042']);
      expect(comp.crumbs()[2].isLast).toBe(true);
      expect(comp.crumbs()[2].isLink).toBe(false);

      // Step 4: Navigate to Settings
      comp.updateCrumbs('/settings/tax');
      expect(comp.crumbs().map((c) => c.label)).toEqual(['Settings', 'Tax']);
    });
  });
});
