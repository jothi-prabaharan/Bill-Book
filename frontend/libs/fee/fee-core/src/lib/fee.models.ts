/** The fee service's shapes (S4, TK-64), as the API sends them: enums by name. */

export type FeeFrequency = 'OneTime' | 'Monthly' | 'Quarterly' | 'Termly' | 'Annual';
export type ConcessionKind = 'Percent' | 'Amount';
export type FeeDocumentStatus = 'Draft' | 'Posted' | 'Void';
export type PaymentMode = 'Cash' | 'Cheque' | 'Upi' | 'Card' | 'BankTransfer';

export interface FeeHead {
  feeHeadId: number;
  code: string;
  name: string;
  incomeAccountId: number | null;
  isRefundable: boolean;
  hsnSacCode: string | null;
  isActive: boolean;
}

export interface FeeStructureLine {
  feeHeadId: number;
  amount: number;
  frequency: FeeFrequency;
  dueDay: number;
}

export interface FeeStructure {
  feeStructureId: number;
  academicYearId: number;
  schoolClassId: number;
  name: string;
  firstMonth: number;
  isActive: boolean;
  lines: FeeStructureLine[];
}

export type SaveFeeStructure = Omit<FeeStructure, 'feeStructureId'>;

export interface Concession {
  feeConcessionId: number;
  studentId: number;
  feeHeadId: number;
  concessionKind: ConcessionKind;
  value: number;
  reason: string;
  validFrom: string;
  validTo: string;
  isApproved: boolean;
}

export type SaveConcession = Omit<Concession, 'feeConcessionId'>;

export interface FeeDemand {
  feeDemandId: number;
  demandNo: string | null;
  studentId: number;
  enrolmentId: number;
  feeStructureId: number;
  periodKey: string;
  contactId: number;
  demandDate: string;
  dueDate: string;
  documentStatus: FeeDocumentStatus;
  currencyCode: string;
  totalAmount: number;
  concessionAmount: number;
  netAmount: number;
  paidAmount: number;
  openAmount: number;
  lines: { feeHeadId: number; amount: number; concessionAmount: number }[];
}

export interface GenerateResult {
  created: number;
  alreadyRaised: number;
  skipped: number;
  notes: string[];
}

export interface Allocation {
  feeDemandId: number;
  amount: number;
}

export interface SaveReceipt {
  contactId: number;
  receiptDate: string;
  paymentMode: PaymentMode;
  bankAccountId: number;
  amount: number;
  reference: string | null;
  allocations: Allocation[];
}

export interface FeeReceipt extends SaveReceipt {
  feeReceiptId: number;
  receiptNo: string;
  unallocatedAmount: number;
  documentStatus: FeeDocumentStatus;
}

export interface PostableAccount {
  accountId: number;
  accountCode: string;
  accountName: string;
  accountTypeId: number;
}

export interface BankAccountOption {
  bankAccountId: number;
  accountName: string;
  maskedNumber: string;
  isCash: boolean;
}
