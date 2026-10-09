import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService } from './auth.service';

/**
 * What is worth testing here is the session state, not the HTTP calls — the
 * signals this service holds are what every guard and interceptor reads, and
 * what survives a page reload. A token left in storage after signing out is a
 * session that comes back when the tab is reopened.
 */
describe('AuthService', () => {
  let auth: AuthService;
  let httpMock: HttpTestingController;

  const configure = (): void => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  };

  /** Stands in for closing the browser: a cookie with no expiry goes with it. */
  const closeBrowser = (): void => {
    document.cookie = 'bb.session=; path=/; max-age=0';
  };

  beforeEach(() => {
    localStorage.clear();
    closeBrowser();
    configure();
  });

  describe('keep me signed in', () => {
    const signIn = async (keepSignedIn: boolean): Promise<void> => {
      const login = auth.login('someone@example.com', 'a-password', keepSignedIn);
      httpMock.expectOne('/api/auth/login').flush({
        accessToken: 'access',
        refreshToken: 'refresh',
        currentOrgId: 'org-1',
        organizations: [],
        licenseStatus: 'Active',
      });
      await login;
    };

    it('keeps a ticked sign-in after the browser is closed', async () => {
      await signIn(true);

      closeBrowser();
      configure();

      expect(auth.isAuthenticated()).toBe(true);
      expect(localStorage.getItem('bb.refresh')).toBe('refresh');
    });

    it('keeps an unticked sign-in across a reload', async () => {
      // The browser is still open, so the session cookie is still there.
      await signIn(false);

      configure();

      expect(auth.isAuthenticated()).toBe(true);
    });

    it('ends an unticked sign-in once the browser has been closed', async () => {
      await signIn(false);

      closeBrowser();
      configure();

      expect(auth.isAuthenticated()).toBe(false);
      expect(localStorage.getItem('bb.access')).toBeNull();
      expect(localStorage.getItem('bb.refresh')).toBeNull();
    });

    it('keeps a session signed in before the box worked', () => {
      // No bb.remember at all: an older sign-in, which was always kept.
      localStorage.setItem('bb.access', 'stored-token');
      localStorage.setItem('bb.refresh', 'stored-refresh');

      configure();

      expect(auth.isAuthenticated()).toBe(true);
    });

    it('forgets the choice on sign-out', async () => {
      await signIn(false);

      auth.logout();

      expect(localStorage.getItem('bb.remember')).toBeNull();
      expect(document.cookie).not.toContain('bb.session=1');
    });
  });

  describe('login auto-routing', () => {
    it('holds the access token and the accessible orgs immediately upon login', async () => {
      const login = auth.login('someone@example.com', 'a-password');

      httpMock.expectOne('/api/auth/login').flush({
        accessToken: 'access',
        refreshToken: 'refresh',
        currentOrgId: 'org-1',
        organizations: [{ orgId: 'org-1', orgName: 'Head Office', roleName: 'Owner' }],
        licenseStatus: 'Active',
      });
      await login;

      expect(auth.organizations()).toHaveLength(1);
      expect(auth.isAuthenticated()).toBe(true);
      expect(localStorage.getItem('bb.access')).toBe('access');
      expect(localStorage.getItem('bb.orgId')).toBe('org-1');
    });
  });

  describe('switching branch', () => {
    it('replaces the token, because a branch is a separate set of books', async () => {
      auth.accessToken.set('token-for-org-1');
      auth.licenseStatus.set('Active');

      const move = auth.switchOrganization('org-2');
      httpMock.expectOne('/api/auth/switch-organization').flush({
        accessToken: 'token-for-org-2',
        refreshToken: 'refresh-2',
        licenseStatus: 'Active',
      });
      await move;

      // The old token still names the old branch and its permissions there.
      // Keeping it would leave the app reading one branch and writing another.
      expect(auth.accessToken()).toBe('token-for-org-2');
      expect(localStorage.getItem('bb.orgId')).toBe('org-2');
    });
  });

  describe('logout', () => {
    it('leaves nothing behind in storage', async () => {
      const login = auth.login('someone@example.com', 'a-password');
      httpMock.expectOne('/api/auth/login').flush({
        accessToken: 'access',
        refreshToken: 'refresh',
        currentOrgId: 'org-1',
        licenseStatus: 'Active',
        organizations: []
      });
      await login;

      auth.logout();

      expect(auth.isAuthenticated()).toBe(false);
      expect(auth.licenseStatus()).toBeNull();
      expect(localStorage.getItem('bb.access')).toBeNull();
      expect(localStorage.getItem('bb.refresh')).toBeNull();
      expect(localStorage.getItem('bb.license')).toBeNull();
    });
  });

  describe('branch expiry', () => {
    const tokenFor = (
      licenseStatus: string,
      licenseExpiry: string | null,
      expiryIsBranchLevel: boolean,
    ) => ({
      accessToken: 'access',
      refreshToken: 'refresh',
      currentOrgId: 'org-1',
      organizations: [],
      licenseStatus,
      licenseExpiry,
      expiryIsBranchLevel,
    });

    const selectInto = async (token: ReturnType<typeof tokenFor>): Promise<void> => {
      const login = auth.login('someone@example.com', 'a-password');
      httpMock.expectOne('/api/auth/login').flush(token);
      await login;
    };

    it('keeps the effective expiry date and whose date it is', async () => {
      await selectInto(tokenFor('Expired', '2026-07-31', true));

      expect(auth.licenseExpiry()).toBe('2026-07-31');
      expect(auth.expiryIsBranchLevel()).toBe(true);
    });

    it('reports an account-level expiry as not branch-level', async () => {
      await selectInto(tokenFor('Expired', '2026-07-31', false));

      expect(auth.expiryIsBranchLevel()).toBe(false);
    });

    it('clears the branch flag when switching to a branch that is fine', async () => {
      await selectInto(tokenFor('Expired', '2026-07-31', true));

      const move = auth.switchOrganization('org-2');
      httpMock
        .expectOne('/api/auth/switch-organization')
        .flush(tokenFor('Active', '2027-03-31', false));
      await move;

      // Licence state belongs to the branch, not the user. A leftover flag here
      // would tell someone working in a live branch that it had closed.
      expect(auth.expiryIsBranchLevel()).toBe(false);
      expect(auth.licenseExpiry()).toBe('2027-03-31');
      expect(auth.isLicenseExpired()).toBe(false);
      expect(localStorage.getItem('bb.expiryScope')).toBeNull();
    });

    it('leaves nothing behind on logout', async () => {
      await selectInto(tokenFor('Expired', '2026-07-31', true));

      auth.logout();

      expect(auth.licenseExpiry()).toBeNull();
      expect(auth.expiryIsBranchLevel()).toBe(false);
      expect(localStorage.getItem('bb.licenseExpiry')).toBeNull();
      expect(localStorage.getItem('bb.expiryScope')).toBeNull();
    });

    it('survives a reload', async () => {
      await selectInto(tokenFor('Expired', '2026-07-31', true));

      configure();

      expect(auth.licenseExpiry()).toBe('2026-07-31');
      expect(auth.expiryIsBranchLevel()).toBe(true);
    });
  });

  describe('restoring a session', () => {
    it('reads the token and licence back out of storage on construction', () => {
      localStorage.setItem('bb.access', 'stored-token');
      localStorage.setItem('bb.license', 'Expired');

      // A fresh injector, standing in for a page reload.
      configure();

      expect(auth.isAuthenticated()).toBe(true);
      expect(auth.isLicenseExpired()).toBe(true);
    });
  });
  });




