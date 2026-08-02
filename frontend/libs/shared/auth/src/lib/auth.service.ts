import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AccessibleOrg,
  Country,
  CustomerStatus,
  LoginResponse,
  SignupRequest,
  SignupResponse,
  StateRow,
  TokenResponse,
} from './auth.models';

const ACCESS_KEY = 'bb.access';
const REFRESH_KEY = 'bb.refresh';
const LICENSE_KEY = 'bb.license';
const EXPIRY_KEY = 'bb.licenseExpiry';
const EXPIRY_SCOPE_KEY = 'bb.expiryScope';
const ORG_KEY = 'bb.orgId';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  /** Pre-auth state between login step one and org selection. */
  readonly preAuthToken = signal<string | null>(null);
  readonly organizations = signal<AccessibleOrg[]>([]);

  readonly accessToken = signal<string | null>(localStorage.getItem(ACCESS_KEY));
  readonly licenseStatus = signal<string | null>(localStorage.getItem(LICENSE_KEY));

  /** The date access to the current branch ends, as an ISO date or null. */
  readonly licenseExpiry = signal<string | null>(localStorage.getItem(EXPIRY_KEY));

  /**
   * Whether that date is the branch's own rather than the account's licence.
   * Only ever narrows what the expired page says; nothing is gated on it.
   */
  readonly expiryIsBranchLevel = signal<boolean>(
    localStorage.getItem(EXPIRY_SCOPE_KEY) === 'branch',
  );

  readonly isAuthenticated = computed(() => this.accessToken() !== null);
  readonly isLicenseExpired = computed(() => this.licenseStatus() === 'Expired');

  async login(email: string, password: string): Promise<LoginResponse> {
    const response = await firstValueFrom(
      this.http.post<LoginResponse>('/api/auth/login', { email, password }),
    );
    this.preAuthToken.set(response.preAuthToken);
    this.organizations.set(response.organizations);
    return response;
  }

  async selectOrganization(orgId: string): Promise<TokenResponse> {
    const preAuth = this.preAuthToken();
    const response = await firstValueFrom(
      this.http.post<TokenResponse>(
        '/api/auth/select-organization',
        { orgId },
        { headers: { 'X-PreAuth-Token': preAuth ?? '' } },
      ),
    );
    localStorage.setItem(ACCESS_KEY, response.accessToken);
    localStorage.setItem(REFRESH_KEY, response.refreshToken);
    localStorage.setItem(ORG_KEY, orgId);
    this.storeLicense(response);
    this.accessToken.set(response.accessToken);
    this.preAuthToken.set(null);
    return response;
  }

  /** The branches this user may work in, read after login for the switcher. */
  async accessibleOrganizations(): Promise<AccessibleOrg[]> {
    return firstValueFrom(this.http.get<AccessibleOrg[]>('/api/auth/organizations'));
  }

  /**
   * Moves to another branch without signing out. A branch is a separate set of
   * books, so this issues a new token carrying that org and the permissions
   * held there — the old token keeps naming the old branch and is replaced.
   */
  async switchOrganization(orgId: string): Promise<TokenResponse> {
    const response = await firstValueFrom(
      this.http.post<TokenResponse>('/api/auth/switch-organization', { orgId }),
    );

    localStorage.setItem(ACCESS_KEY, response.accessToken);
    localStorage.setItem(REFRESH_KEY, response.refreshToken);
    localStorage.setItem(ORG_KEY, orgId);
    this.storeLicense(response);
    this.accessToken.set(response.accessToken);
    return response;
  }

  logout(): void {
    localStorage.removeItem(ACCESS_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(LICENSE_KEY);
    localStorage.removeItem(EXPIRY_KEY);
    localStorage.removeItem(EXPIRY_SCOPE_KEY);
    this.accessToken.set(null);
    this.licenseStatus.set(null);
    this.licenseExpiry.set(null);
    this.expiryIsBranchLevel.set(false);
    this.organizations.set([]);
  }

  /**
   * Licence state travels with the branch, not with the user, so switching
   * branch replaces all three values rather than merging them — an expired
   * Chennai must not leave its date showing after moving to a live Bangalore.
   */
  private storeLicense(response: TokenResponse): void {
    localStorage.setItem(LICENSE_KEY, response.licenseStatus);
    this.licenseStatus.set(response.licenseStatus);

    if (response.licenseExpiry) {
      localStorage.setItem(EXPIRY_KEY, response.licenseExpiry);
    } else {
      localStorage.removeItem(EXPIRY_KEY);
    }
    this.licenseExpiry.set(response.licenseExpiry ?? null);

    if (response.expiryIsBranchLevel) {
      localStorage.setItem(EXPIRY_SCOPE_KEY, 'branch');
    } else {
      localStorage.removeItem(EXPIRY_SCOPE_KEY);
    }
    this.expiryIsBranchLevel.set(response.expiryIsBranchLevel === true);
  }

  // ---- Forgot password (OTP) ----------------------------------------------

  forgotPassword(email: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post('/api/auth/forgot-password', { email, channel: 1 }),
    );
  }

  verifyOtp(email: string, code: string): Promise<unknown> {
    return firstValueFrom(this.http.post('/api/auth/verify-otp', { email, code }));
  }

  resetPassword(email: string, code: string, newPassword: string, confirmPassword: string): Promise<unknown> {
    return firstValueFrom(
      this.http.post('/api/auth/reset-password', { email, code, newPassword, confirmPassword }),
    );
  }

  // ---- Signup -------------------------------------------------------------

  signup(request: SignupRequest): Promise<SignupResponse> {
    return firstValueFrom(this.http.post<SignupResponse>('/api/customers/signup', request));
  }

  customerStatus(customerId: string): Promise<CustomerStatus> {
    return firstValueFrom(this.http.get<CustomerStatus>(`/api/customers/${customerId}/status`));
  }

  // ---- Reference data -----------------------------------------------------

  countries(): Promise<Country[]> {
    return firstValueFrom(this.http.get<Country[]>('/api/master/countries'));
  }

  states(countryId: number): Promise<StateRow[]> {
    return firstValueFrom(this.http.get<StateRow[]>(`/api/master/countries/${countryId}/states`));
  }
}
