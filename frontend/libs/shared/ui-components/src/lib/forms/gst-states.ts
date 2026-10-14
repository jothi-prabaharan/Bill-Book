import { BbSelectOption } from './field.model';

/**
 * Standard Indian GST 2-digit state and union territory codes.
 * Used for Place of Supply in all Sales and Purchase transactions.
 */
export const GST_STATE_OPTIONS: readonly BbSelectOption<string>[] = [
  { value: '01', label: '01 - Jammu & Kashmir' },
  { value: '02', label: '02 - Himachal Pradesh' },
  { value: '03', label: '03 - Punjab' },
  { value: '04', label: '04 - Chandigarh' },
  { value: '05', label: '05 - Uttarakhand' },
  { value: '06', label: '06 - Haryana' },
  { value: '07', label: '07 - Delhi' },
  { value: '08', label: '08 - Rajasthan' },
  { value: '09', label: '09 - Uttar Pradesh' },
  { value: '10', label: '10 - Bihar' },
  { value: '11', label: '11 - Sikkim' },
  { value: '12', label: '12 - Arunachal Pradesh' },
  { value: '13', label: '13 - Nagaland' },
  { value: '14', label: '14 - Manipur' },
  { value: '15', label: '15 - Mizoram' },
  { value: '16', label: '16 - Tripura' },
  { value: '17', label: '17 - Meghalaya' },
  { value: '18', label: '18 - Assam' },
  { value: '19', label: '19 - West Bengal' },
  { value: '20', label: '20 - Jharkhand' },
  { value: '21', label: '21 - Odisha' },
  { value: '22', label: '22 - Chhattisgarh' },
  { value: '23', label: '23 - Madhya Pradesh' },
  { value: '24', label: '24 - Gujarat' },
  { value: '26', label: '26 - Dadra & Nagar Haveli and Daman & Diu' },
  { value: '27', label: '27 - Maharashtra' },
  { value: '29', label: '29 - Karnataka' },
  { value: '30', label: '30 - Goa' },
  { value: '31', label: '31 - Lakshadweep' },
  { value: '32', label: '32 - Kerala' },
  { value: '33', label: '33 - Tamil Nadu' },
  { value: '34', label: '34 - Puducherry' },
  { value: '35', label: '35 - Andaman & Nicobar Islands' },
  { value: '36', label: '36 - Telangana' },
  { value: '37', label: '37 - Andhra Pradesh' },
  { value: '38', label: '38 - Ladakh' },
  { value: '97', label: '97 - Other Territory' },
];

/**
 * Standard currencies for transaction create pages.
 */
export const TRANSACTION_CURRENCY_OPTIONS: readonly BbSelectOption<string>[] = [
  { value: 'INR', label: 'INR - Indian Rupee (₹)' },
  { value: 'USD', label: 'USD - US Dollar ($)' },
  { value: 'EUR', label: 'EUR - Euro (€)' },
  { value: 'GBP', label: 'GBP - British Pound (£)' },
  { value: 'AED', label: 'AED - UAE Dirham' },
  { value: 'SAR', label: 'SAR - Saudi Riyal' },
  { value: 'SGD', label: 'SGD - Singapore Dollar' },
  { value: 'AUD', label: 'AUD - Australian Dollar' },
  { value: 'CAD', label: 'CAD - Canadian Dollar' },
  { value: 'JPY', label: 'JPY - Japanese Yen (¥)' },
  { value: 'QAR', label: 'QAR - Qatari Riyal' },
  { value: 'OMR', label: 'OMR - Omani Rial' },
  { value: 'KWD', label: 'KWD - Kuwaiti Dinar' },
  { value: 'BHD', label: 'BHD - Bahraini Dinar' },
  { value: 'MYR', label: 'MYR - Malaysian Ringgit' },
];

/**
 * Resolves the default GST state code (2 digits) from an organization's GSTIN or returns fallback '33'.
 */
export function resolveOrgStateCode(gstin?: string | null): string {
  if (gstin && gstin.trim().length >= 2) {
    const code = gstin.trim().substring(0, 2);
    if (/^\d{2}$/.test(code)) {
      return code;
    }
  }
  return '33';
}

/**
 * Resolves the default currency code from an organization's baseCurrency or returns fallback 'INR'.
 */
export function resolveOrgCurrency(baseCurrency?: string | null): string {
  if (baseCurrency && baseCurrency.trim().length === 3) {
    return baseCurrency.trim().toUpperCase();
  }
  return 'INR';
}

