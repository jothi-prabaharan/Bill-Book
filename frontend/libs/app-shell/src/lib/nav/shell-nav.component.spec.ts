import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { ShellNavComponent } from './shell-nav.component';
import { AuthService } from '@bill-book/auth';

describe('ShellNavComponent (libs/app-shell)', () => {
  let mockRouter: Partial<Router>;
  let mockAuthService: {
    canView: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    mockRouter = {
      url: '/dashboard',
      navigateByUrl: vi.fn().mockResolvedValue(true),
      navigate: vi.fn().mockResolvedValue(true),
    };

    mockAuthService = {
      canView: vi.fn().mockReturnValue(true),
    };

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
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

  it('NAV-06: mobileTopNav contains exactly first 4 items, mobileMoreNav contains the rest', () => {
    const comp = createComponent();
    expect(comp.mobileTopNav().length).toBe(4);
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
});
