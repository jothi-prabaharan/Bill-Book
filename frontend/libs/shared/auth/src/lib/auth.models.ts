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
  licenseExpiry: string | null;
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
