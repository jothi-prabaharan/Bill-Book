/**
 * The shared employee master, as the Hrm service serves it (H1, TK-48).
 * Enums travel by name; dates are ISO `yyyy-MM-dd` strings.
 */

export type Gender = 'NotStated' | 'Male' | 'Female' | 'Other';
export type MaritalStatus = 'NotStated' | 'Single' | 'Married' | 'Widowed' | 'Divorced';
export type EmploymentType = 'Permanent' | 'Probation' | 'Contract' | 'PartTime' | 'Intern' | 'Consultant';
export type EmployeeStatus = 'Onboarding' | 'Active' | 'OnNotice' | 'Exited';
export type AddressKind = 'Current' | 'Permanent';
export type Relationship = 'Spouse' | 'Child' | 'Father' | 'Mother' | 'Sibling' | 'Friend' | 'Other';
export type NominationKind = 'Pf' | 'Gratuity' | 'Insurance';
export type EmployeeDocumentKind = 'Pan' | 'Aadhaar' | 'Passport' | 'Resume' | 'OfferLetter' | 'Certificate' | 'Other';
export type AnnouncementAudience = 'Everyone' | 'Department' | 'Location' | 'Grade';

/** The organisation masters, one route segment each. */
export type OrgMasterKind = 'departments' | 'designations' | 'grades' | 'cost-centres' | 'work-locations';

export interface OrgMasterRow {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
  headEmployeeId?: number | null;
  parentDepartmentId?: number | null;
  sortOrder?: number | null;
  noticePeriodDays?: number | null;
  stateId?: number | null;
  addressLine1?: string | null;
  city?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  geoFenceMetres?: number | null;
}

export interface EmployeeListItem {
  employeeId: number;
  employeeCode: string;
  fullName: string;
  departmentName: string;
  designationName: string;
  workLocationName: string;
  joiningDate: string;
  employeeStatus: EmployeeStatus;
  employmentType: EmploymentType;
  phone: string;
  maskedPan: string | null;
  maskedAadhaar: string | null;
  hasLogin: boolean;
}

export interface EmployeeListPage {
  items: EmployeeListItem[];
  total: number;
}

export interface EmployeeAddress {
  addressKind: AddressKind;
  addressLine1: string;
  addressLine2?: string | null;
  city?: string | null;
  stateId?: number | null;
  postalCode?: string | null;
}

export interface EmployeeContact {
  name: string;
  relationship: Relationship;
  phone: string;
  isPrimary: boolean;
}

export interface EmployeeFamilyMember {
  employeeFamilyMemberId?: number | null;
  name: string;
  relationship: Relationship;
  dateOfBirth?: string | null;
  isDependent: boolean;
  isEsiCovered: boolean;
}

/** A nominee names its family member by position in the family list. */
export interface EmployeeNominee {
  familyMemberIndex: number;
  nominationKind: NominationKind;
  sharePercent: number;
}

export interface EmployeeEducation {
  qualification: string;
  institution: string;
  yearOfPassing: number;
  grade?: string | null;
}

export interface PreviousEmployment {
  employer: string;
  fromDate: string;
  toDate: string;
  lastDesignation?: string | null;
}

export interface EmployeeBankDetail {
  employeeBankDetailId?: number | null;
  accountHolder: string;
  accountNo: string;
  ifsc: string;
  bankName: string;
  isPrimary: boolean;
}

export interface EmployeeDocument {
  documentKind: EmployeeDocumentKind;
  attachmentKey: string;
  validUntil?: string | null;
}

export interface AssetIssue {
  assetName: string;
  assetTag?: string | null;
  issuedDate: string;
  returnedDate?: string | null;
  recoveryAmount?: number | null;
}

/** What `POST` and `PUT api/hrm/employees` take. */
export interface SaveEmployee {
  firstName: string;
  middleName?: string | null;
  lastName?: string | null;
  dateOfBirth: string;
  gender: Gender;
  maritalStatus: MaritalStatus;
  bloodGroup?: string | null;
  departmentId: number;
  designationId: number;
  gradeId: number;
  workLocationId: number;
  costCentreId?: number | null;
  reportsToEmployeeId?: number | null;
  joiningDate: string;
  probationEndDate?: string | null;
  confirmationDate?: string | null;
  noticePeriodDays?: number | null;
  employmentType: EmploymentType;
  employeeStatus: EmployeeStatus;
  exitDate?: string | null;
  userId?: string | null;
  workEmail?: string | null;
  personalEmail?: string | null;
  phone: string;
  pan?: string | null;
  aadhaar?: string | null;
  uan?: string | null;
  pfNumber?: string | null;
  esiNumber?: string | null;
  isPfApplicable: boolean;
  isEsiApplicable: boolean;
  isPtApplicable: boolean;
  isLwfApplicable: boolean;
  effectiveDate?: string | null;
  remarks?: string | null;
  addresses: EmployeeAddress[];
  contacts: EmployeeContact[];
  familyMembers: EmployeeFamilyMember[];
  nominees: EmployeeNominee[];
  education: EmployeeEducation[];
  previousEmployments: PreviousEmployment[];
  bankDetails: EmployeeBankDetail[];
  documents: EmployeeDocument[];
  assetIssues: AssetIssue[];
}

export interface EmploymentHistoryRow {
  effectiveDate: string;
  changeKind: string;
  departmentId: number;
  designationId: number;
  gradeId: number;
  workLocationId: number;
  reportsToEmployeeId: number | null;
  remarks: string | null;
}

export interface EmployeeDetail extends SaveEmployee {
  employeeId: number;
  employeeCode: string;
  /** False when PAN, Aadhaar and account numbers came back masked. */
  sensitiveShown: boolean;
  history: EmploymentHistoryRow[];
}

export interface Announcement {
  announcementId?: number;
  title: string;
  body: string;
  publishDate: string;
  expiryDate?: string | null;
  audience: AnnouncementAudience;
  audienceRefId?: number | null;
  isPinned: boolean;
}

export interface PolicyDocument {
  policyDocumentId?: number;
  title: string;
  attachmentKey: string;
  effectiveDate: string;
  isAcknowledgementRequired: boolean;
  isActive: boolean;
  acknowledgements?: number;
}

export interface MyProfile {
  employeeId: number;
  employeeCode: string;
  firstName: string;
  middleName?: string | null;
  lastName?: string | null;
  fullName: string;
  dateOfBirth: string;
  gender: Gender | string;
  maritalStatus: MaritalStatus | string;
  bloodGroup?: string | null;
  phone?: string | null;
  workEmail?: string | null;
  personalEmail?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  pan?: string | null;
  aadhaar?: string | null;
  uan?: string | null;
  departmentId: number;
  departmentName?: string | null;
  designationId: number;
  designationName?: string | null;
  gradeId: number;
  gradeName?: string | null;
  workLocationId: number;
  workLocationName?: string | null;
  reportsToEmployeeId?: number | null;
  reportsToName?: string | null;
  joiningDate: string;
  confirmationDate?: string | null;
  employmentType: EmploymentType | string;
  employeeStatus: EmployeeStatus | string;
  addresses: EmployeeAddress[];
  contacts: EmployeeContact[];
  familyMembers: EmployeeFamilyMember[];
  education: EmployeeEducation[];
  previousEmployments: PreviousEmployment[];
  bankDetails: EmployeeBankDetail[];
  documents: EmployeeDocument[];
}

export interface UpdateMyProfile {
  phone?: string | null;
  personalEmail?: string | null;
  bloodGroup?: string | null;
  maritalStatus?: MaritalStatus | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
}

export interface TeamMember {
  employeeId: number;
  employeeCode: string;
  fullName: string;
  departmentName?: string | null;
  designationName?: string | null;
  workLocationName?: string | null;
  phone?: string | null;
  workEmail?: string | null;
  joiningDate: string;
  level: number;
  reportsToEmployeeId?: number | null;
  reportsToName?: string | null;
  status: string;
}

export interface TeamSummary {
  totalMembers: number;
  directReports: number;
  indirectReports: number;
}

