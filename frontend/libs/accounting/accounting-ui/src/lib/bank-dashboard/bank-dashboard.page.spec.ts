import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it, beforeEach } from 'vitest';
import { BankDashboardPage } from './bank-dashboard.page';

describe('BankDashboardPage', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  const createComponent = (): BankDashboardPage =>
    TestBed.runInInjectionContext(() => new BankDashboardPage());

  it('DASH-B01: Component instantiates cleanly', () => {
    const component = createComponent();
    expect(component).toBeTruthy();
  });

  it('DASH-B02: Default accounts list contains all 9 accounts', () => {
    const component = createComponent();
    expect(component.accounts().length).toBe(9);
    expect(component.accounts()[0].id).toBe('hdfc');
  });

  it('DASH-B03: Base currency toggle switches base state and label', () => {
    const component = createComponent();
    expect(component.base()).toBe(false);
    expect(component.baseLabel()).toBe('Show base INR');

    component.toggleBase();
    expect(component.base()).toBe(true);
    expect(component.baseLabel()).toBe('Showing base INR');

    // Check foreign currency formatting under base mode (HSBC USD)
    const hsbc = component.accounts().find((a) => a.id === 'hsbc')!;
    expect(component.formatBal(hsbc)).toContain('₹ 24,71,742');

    component.toggleBase();
    expect(component.base()).toBe(false);
    expect(component.formatBal(hsbc)).toContain('$ 28,450.00');
  });

  it('DASH-B04: Menu toggling opens and closes menus', () => {
    const component = createComponent();
    const dummyEvent = new MouseEvent('click');

    expect(component.openMenuId()).toBeNull();
    component.toggleMenu('hdfc', dummyEvent);
    expect(component.openMenuId()).toBe('hdfc');

    component.closeMenu();
    expect(component.openMenuId()).toBeNull();
  });
});
