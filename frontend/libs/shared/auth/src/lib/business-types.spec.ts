import { describe, expect, it } from 'vitest';
import { ADD_ONS, BUSINESS_TYPES, isAddOnAllowed, normaliseBusinessType } from './business-types';

/** Held to AddOnRules.Allowed in Shared.Kernel/Apps/App.cs. */
const SERVER: Record<string, string[]> = {
  Retail: ['Hrms', 'Payroll', 'Crm', 'Pos', 'FixedAssets', 'ClientPortal', 'EInvoicing', 'Budget', 'RecurringBilling'],
  Pharma: ['Hrms', 'Payroll', 'Crm', 'Pos', 'FixedAssets', 'ClientPortal', 'EInvoicing', 'Budget', 'RecurringBilling'],
  Jewellery: ADD_ONS.map((a) => a.value),
  School: ['Hrms', 'Payroll', 'Crm', 'FixedAssets', 'ClientPortal', 'Budget', 'RecurringBilling'],
  Service: ['Hrms', 'Payroll', 'Crm', 'FixedAssets', 'ClientPortal', 'Budget', 'RecurringBilling'],
  Restaurant: ['Hrms', 'Payroll', 'Crm', 'FixedAssets', 'ClientPortal', 'EInvoicing', 'Budget', 'RecurringBilling'],
};

describe('business types and add-ons', () => {
  it('offers the six business types, Retail first', () => {
    expect(BUSINESS_TYPES.map((b) => b.value)).toEqual(['Retail', 'Pharma', 'Jewellery', 'School', 'Service', 'Restaurant']);
  });

  it.each(Object.keys(SERVER))('allows exactly what the server allows for %s', (bt) => {
    const allowed = ADD_ONS.map((a) => a.value).filter((a) => isAddOnAllowed(bt, a));
    expect(allowed).toEqual(SERVER[bt]);
  });

  it('allows only HRMS and Payroll with no business type', () => {
    expect(ADD_ONS.map((a) => a.value).filter((a) => isAddOnAllowed(null, a))).toEqual(['Hrms', 'Payroll']);
  });

  it('reads the old name General as Retail', () => {
    expect(normaliseBusinessType('General')).toBe('Retail');
    expect(normaliseBusinessType('Pharma')).toBe('Pharma');
    expect(normaliseBusinessType('')).toBeNull();
    expect(isAddOnAllowed('General', 'Pos')).toBe(true);
  });
});
