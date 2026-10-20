export interface AccessibleOrg {
  orgId: string;
  orgName: string;
  roleName: string;
  /** The organisation the branch belongs to; the branch switcher groups by it. */
  customerName?: string | null;
  /** Retail, Pharma, Jewellery, School, Service or Restaurant. */
  businessType?: string;
  baseCurrency?: string | null;
  gstin?: string | null;
  city?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  postalCode?: string | null;
  stateId?: number | null;
}

export interface SessionState {
  lastAccessedOrgId: string | null;
  organizations: AccessibleOrg[];
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  accessExpiresInSeconds: number;
  licenseStatus: string;
  licenseExpiry: string | null;
  expiryIsBranchLevel: boolean;
  currentOrgId: string;
  organizations: AccessibleOrg[];
}

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  accessExpiresInSeconds: number;
  /** Trial | Active | Expired — the shell gates on this. */
  licenseStatus: string;
  /**
   * When access to the branch just selected ends — the earlier of the account's
   * licence expiry and the branch's own, so it is the date being enforced.
   */
  licenseExpiry: string | null;
  /**
   * True when it is the branch's own date rather than the account's licence.
   * The two need different words: one asks the customer to renew, the other
   * tells them this branch has closed and the account is fine.
   */
  expiryIsBranchLevel: boolean;
}

export interface SignupRequest {
  /**
   * The first branch's business type (D-50, TK-136). Omitted means Retail;
   * null means HRMS or Payroll only, which then needs one of those add-ons.
   */
  businessType?: string | null;
  /** The first branch's add-ons, comma-separated as the server reads a flags enum, e.g. `Hrms, Payroll`. */
  addOns?: string;
  displayName: string;
  email: string;
  password: string;
  mobileNumber?: string;
  companyName: string;
  organizationName: string;
  financialYearStartMonth: number;
  baseCurrency?: string;
  gstin?: string;
  pan?: string;
  tan?: string;
  tin?: string;
  cin?: string;
  udyamNumber?: string;
  countryId: number;
  stateId?: number;
  addressLine1?: string;
  addressLine2?: string;
  city?: string;
  postalCode?: string;
  /**
   * How the verification code is sent: by email to `email`, the default
   * (TK-197), or on WhatsApp or by SMS to `mobileNumber` (TK-196).
   */
  otpChannel?: OtpChannel;
  /**
   * The code sent by `otpChannel`. Absent, the server sends one and creates
   * nothing; present and right, it creates the trial.
   */
  otpCode?: string;
}

/**
 * How a one-time code is sent. Signup and password reset both take all three,
 * email the default (TK-196, TK-197).
 */
export type OtpChannel = 'WhatsApp' | 'Sms' | 'Email';

/** The answer to a signup sent without a code: one is on its way, nothing is created yet. */
export interface SignupCodeResponse {
  channel: OtpChannel;
  /** The email address as typed, or the number with all but its last four digits hidden. */
  destination: string;
  expiresInSeconds: number;
  resendAfterSeconds: number;
  message: string;
}

export interface SignupResponse {
  customerId: string;
  customerCode: string;
  message: string;
}

export interface CustomerStatus {
  customerId: string;
  customerStatus: string;
  provisioningStatus: string;
  canLogin: boolean;
}

export interface Country {
  countryId: number;
  countryCode: string;
  countryName: string;
  currencyCode: string;
  phoneCode: string | null;
}

export interface StateRow {
  stateId: number;
  stateCode: string;
  stateName: string;
}

export interface Currency {
  currencyId: number;
  code: string;
  name: string;
  symbol: string;
}
