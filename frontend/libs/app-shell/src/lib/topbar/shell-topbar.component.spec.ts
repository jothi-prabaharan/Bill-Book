import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { ShellTopbarComponent } from './shell-topbar.component';
import { AuthService, AccessibleOrg } from '@bill-book/auth';
import { ElementRef } from '@angular/core';

describe('ShellTopbarComponent (libs/app-shell)', () => {
  let mockRouter: Partial<Router>;
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
    mockRouter = {
      url: '/dashboard',
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
        { provide: Router, useValue: mockRouter },
        { provide: AuthService, useValue: mockAuthService },
        { provide: ElementRef, useValue: mockElementRef },
      ],
    });

    localStorage.setItem('bb.orgId', 'org-1');
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

  it('TOPBAR-05: Escape closes organization dropdown and popups', () => {
    const comp = createComponent();
    comp.orgOpen.set(true);
    comp.newOpen.set(true);
    comp.favOpen.set(true);

    comp.onEscape();
    expect(comp.orgOpen()).toBe(false);
    expect(comp.newOpen()).toBe(false);
    expect(comp.favOpen()).toBe(false);
  });

  it('TOPBAR-06: Outside click closes organization dropdown', () => {
    const comp = createComponent();
    comp.orgOpen.set(true);

    const outsideElement = document.createElement('span');
    document.body.appendChild(outsideElement);

    comp.onClickOutside(outsideElement);
    expect(comp.orgOpen()).toBe(false);

    document.body.removeChild(outsideElement);
  });

  it('TOPBAR-07: Selecting quick doc emits quickAction and closes popup', () => {
    const comp = createComponent();
    comp.openNew();
    expect(comp.newOpen()).toBe(true);

    let selectedAction = '';
    comp.quickAction.subscribe((action) => {
      selectedAction = action;
    });

    comp.selectDoc('INV');
    expect(selectedAction).toBe('INV');
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
    comp.orgOpen.set(true);

    let changedOrg = '';
    comp.organizationChange.subscribe((orgId) => {
      changedOrg = orgId;
    });

    await comp.pickOrg('org-2');
    expect(mockAuthService.switchOrganization).toHaveBeenCalledWith('org-2');
    expect(changedOrg).toBe('org-2');
    expect(comp.orgOpen()).toBe(false);
  });
});
