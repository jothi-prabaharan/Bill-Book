export interface AccessibleOrg {
  orgId: string;
  orgName: string;
  roleName: string;
}

export interface LoginResponse {
  preAuthToken: string;
  expiresInSeconds: number;
  organizations: AccessibleOrg[];
  requiresOrgSelection: boolean;
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
}

export interface SignupResponse {
  customerId: string;
  customerCode: string;
  message: string;
}

export interface CustomerStatus {
  customerId: string;
  customerStatus: string;
  databaseStatus: string;
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
