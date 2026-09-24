import { describe, expect, it } from 'vitest';
import { ApplicationRow, applicationStatus } from './applications.page';

/** The status column on Settings › Applications (TK-44). */
describe('applicationStatus', () => {
  const row = (overrides: Partial<ApplicationRow>): ApplicationRow => ({
    app: 'Payroll',
    licensed: true,
    licenseType: 'Standard',
    expiryDate: '2027-01-01',
    isActive: true,
    ...overrides,
  });

  it('reads an app the customer never started as not started', () => {
    expect(applicationStatus(row({ licensed: false, expiryDate: null }), '2026-09-24')).toBe('Not started');
  });

  it('tells a trial, an active licence, an expired one and a suspended one apart', () => {
    expect(applicationStatus(row({ licenseType: 'Trial' }), '2026-09-24')).toBe('Trial');
    expect(applicationStatus(row({}), '2026-09-24')).toBe('Active');
    expect(applicationStatus(row({ expiryDate: '2026-09-01' }), '2026-09-24')).toBe('Expired');
    expect(applicationStatus(row({ isActive: false }), '2026-09-24')).toBe('Suspended');
  });
});
