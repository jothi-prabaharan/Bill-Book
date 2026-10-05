/**
 * A branch's business type and the add-ons it allows (D-36). The rules mirror
 * `AddOnRules.Allowed` in `Shared.Kernel/Apps/App.cs`; the server decides, and
 * a page uses these only to offer what the server would accept.
 */
export type BusinessTypeCode = 'Retail' | 'Pharma' | 'Jewellery' | 'School' | 'Service' | 'Restaurant';

export type AddOnCode =
  | 'Hrms'
  | 'Payroll'
  | 'Crm'
  | 'Pos'
  | 'FixedAssets'
  | 'ClientPortal'
  | 'EInvoicing'
  | 'Budget'
  | 'RecurringBilling'
  | 'SavingsScheme';

export const BUSINESS_TYPES: readonly { value: BusinessTypeCode; label: string }[] = [
  { value: 'Retail', label: 'Retail' },
  { value: 'Pharma', label: 'Pharmacy' },
  { value: 'Jewellery', label: 'Jewellery' },
  { value: 'School', label: 'School' },
  { value: 'Service', label: 'Service' },
  { value: 'Restaurant', label: 'Restaurant' },
];

export const ADD_ONS: readonly { value: AddOnCode; label: string }[] = [
  { value: 'Hrms', label: 'HRMS' },
  { value: 'Payroll', label: 'Payroll' },
  { value: 'Crm', label: 'CRM' },
  { value: 'Pos', label: 'Point of Sale' },
  { value: 'FixedAssets', label: 'Fixed Assets' },
  { value: 'ClientPortal', label: 'Client Portal' },
  { value: 'EInvoicing', label: 'E-Invoicing' },
  { value: 'Budget', label: 'Budgeting' },
  { value: 'RecurringBilling', label: 'Recurring Billing' },
  { value: 'SavingsScheme', label: 'Savings Scheme' },
];

/** `General` is what Retail was called before TK-136; anything else passes through. */
export function normaliseBusinessType(value: string | null | undefined): string | null {
  if (!value) return null;
  return value.trim().toLowerCase() === 'general' ? 'Retail' : value;
}

const TRADING: readonly string[] = ['Retail', 'Pharma', 'Jewellery'];

/** Whether a branch of `businessType` may switch `addOn` on. */
export function isAddOnAllowed(businessType: string | null | undefined, addOn: string): boolean {
  const bt = normaliseBusinessType(businessType);
  if (addOn === 'Hrms' || addOn === 'Payroll') return true;
  if (!bt) return false;
  switch (addOn) {
    case 'Crm':
    case 'FixedAssets':
    case 'ClientPortal':
    case 'Budget':
    case 'RecurringBilling':
      return ['Retail', 'Pharma', 'Jewellery', 'School', 'Service', 'Restaurant'].includes(bt);
    case 'Pos':
      // A restaurant bills through its own order screen, not the retail till (D-43).
      return TRADING.includes(bt);
    case 'EInvoicing':
      return [...TRADING, 'Restaurant'].includes(bt);
    case 'SavingsScheme':
      return bt === 'Jewellery';
    default:
      return false;
  }
}
