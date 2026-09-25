/** The AMC service's shapes (S8, TK-68), as the API sends them: enums by name. */

export type BillingFrequency = 'Upfront' | 'Quarterly' | 'HalfYearly' | 'Annual';
export type AmcCoverage = 'Comprehensive' | 'NonComprehensive';
export type ContractStatus = 'Draft' | 'Active' | 'Expired' | 'Terminated';
export type VisitKind = 'Scheduled' | 'Breakdown';

export interface AmcContract {
  amcContractId: number;
  contractNo: string;
  vendorContactId: number;
  startDate: string;
  endDate: string;
  contractValue: number;
  billingFrequency: BillingFrequency;
  visitsPerYear: number;
  amcCoverage: AmcCoverage;
  renewalReminderDays: number;
  reminderEmail: string | null;
  contractStatus: ContractStatus;
  terminationReason: string | null;
  remarks: string | null;
  facilityAssetIds: number[];
  visitsMade: number;
}

export type SaveContract = Omit<AmcContract, 'amcContractId' | 'contractStatus' | 'terminationReason' | 'visitsMade'>;

export interface AmcVisit {
  amcVisitId: number;
  amcContractId: number;
  visitDate: string;
  visitKind: VisitKind;
  facilityAssetId: number | null;
  remarks: string | null;
  workOrderId: number | null;
  workOrderNo: string | null;
}

export interface RecordVisit {
  visitDate: string;
  visitKind: VisitKind;
  facilityAssetId: number | null;
  remarks: string | null;
  raiseWorkOrder: boolean;
}

export interface VendorOption {
  contactId: number;
  contactCode: string;
  displayName: string;
}

export const CONTRACT_STATUSES: readonly { value: ContractStatus; label: string }[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Active', label: 'Active' },
  { value: 'Expired', label: 'Expired' },
  { value: 'Terminated', label: 'Terminated' },
];

export const BILLING_FREQUENCIES: readonly { value: BillingFrequency; label: string }[] = [
  { value: 'Upfront', label: 'Upfront' },
  { value: 'Quarterly', label: 'Quarterly' },
  { value: 'HalfYearly', label: 'Half-yearly' },
  { value: 'Annual', label: 'Annual' },
];

export const COVERAGES: readonly { value: AmcCoverage; label: string }[] = [
  { value: 'Comprehensive', label: 'Comprehensive (parts covered)' },
  { value: 'NonComprehensive', label: 'Non-comprehensive (labour only)' },
];

export const VISIT_KINDS: readonly { value: VisitKind; label: string }[] = [
  { value: 'Scheduled', label: 'Scheduled' },
  { value: 'Breakdown', label: 'Breakdown' },
];

/** Only a draft changes its terms; an active contract changes only its assets, reminder and remarks. */
export const termsEditable = (status: ContractStatus): boolean => status === 'Draft';
export const takesChanges = (status: ContractStatus): boolean => status === 'Draft' || status === 'Active';

/** Days until a contract ends, counted from `today` (both ISO dates). Negative once it has ended. */
export function daysLeft(endDate: string, today: string): number {
  return Math.round((Date.parse(endDate) - Date.parse(today)) / 86_400_000);
}
