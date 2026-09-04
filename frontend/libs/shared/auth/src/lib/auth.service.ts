import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { readPermissions } from './token-claims';
import {
  AccessibleOrg,
  Country,
  CustomerStatus,
  LoginResponse,
  SignupRequest,
  SignupResponse,
  StateRow,
  TokenResponse,
  Currency,
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

  /**
   * What this user may do in the branch they are signed in to, read off the
   * token rather than stored separately — switching branch replaces the token,
   * so the permissions follow without anything having to remember to clear them.
   */
  readonly permissions = computed(() => new Set(readPermissions(this.accessToken())));

  /** Whether the user holds a permission, by its full code. */
  has(permission: string): boolean {
    return this.permissions().has(permission);
  }

  /**
   * Whether the user can see a module at all. The menu asks this; anything
   * finer belongs to the screen, and the server decides either way.
   */
  canView(module: string): boolean {
    return this.has(`${module}.view`);
  }

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

  /**
   * Trades the stored refresh token for a fresh pair.
   *
   * **The refresh token is spent by this call.** The server rotates it: the one
   * sent is revoked and a new one comes back, so the new one must be stored
   * before anything else can use it. Presenting the old one again is read as
   * reuse and ends the whole session — which is the intended behaviour when a
   * token has been stolen, and a bug if this method ever let two refreshes run
   * at once. `inFlight` is why it cannot: a second caller awaits the first
   * rather than sending the same token a second time.
   *
   * Returns the new access token, or null if the session is over. Null is not
   * an error to retry; it means sign in again.
   */
  async refresh(): Promise<string | null> {
    this.inFlight ??= this.rotate();

    try {
      return await this.inFlight;
    } finally {
      this.inFlight = null;
    }
  }

  /** The one refresh in flight, so a burst of 401s sends one request. */
  private inFlight: Promise<string | null> | null = null;

  private async rotate(): Promise<string | null> {
    const refreshToken = localStorage.getItem(REFRESH_KEY);

    if (refreshToken === null) {
      // An access token with no refresh token beside it is a session that
      // cannot continue — storage was cleared, or this is an older sign-in from
      // before rotation. Either way it ends here rather than looping.
      this.clear();

      return null;
    }

    try {
      const response = await firstValueFrom(
        this.http.post<TokenResponse>('/api/auth/refresh', { refreshToken }),
      );

      localStorage.setItem(ACCESS_KEY, response.accessToken);
      localStorage.setItem(REFRESH_KEY, response.refreshToken);
      this.storeLicense(response);
      this.accessToken.set(response.accessToken);

      return response.accessToken;
    } catch {
      // Expired, revoked, or replayed — all the same to the client, and all
      // mean the same thing: this session is over.
      this.clear();

      return null;
    }
  }

  /**
   * Signs out, telling the server so the token family is revoked rather than
   * left live until it expires.
   *
   * **Local state is cleared whatever the server says.** A logout that left the
   * token in storage because the network was down would leave someone signed in
   * on a shared machine, which is the case logout exists for.
   */
  async signOut(): Promise<void> {
    const refreshToken = localStorage.getItem(REFRESH_KEY);

    this.clear();

    if (refreshToken !== null) {
      try {
        await firstValueFrom(this.http.post('/api/auth/logout', { refreshToken }));
      } catch {
        // Nothing to do about it, and nothing to tell the user: they are
        // signed out here either way.
      }
    }
  }

  /** Clears local session state. Sign-out without the server round trip. */
  logout(): void {
    this.clear();
  }

  private clear(): void {
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

  currencies(): Promise<Currency[]> {
    return firstValueFrom(this.http.get<Currency[]>('/api/master/currencies'));
  }
}
