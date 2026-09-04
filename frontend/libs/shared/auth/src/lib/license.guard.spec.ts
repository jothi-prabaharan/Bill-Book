import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { describe, beforeEach, expect, it } from 'vitest';
import { AuthService } from './auth.service';
import { authGuard, licenseActiveGuard, permissionGuard } from './license.guard';

/**
 * These three guards decide whether a page is reachable, so a mistake in any of
 * them is a page that opens when it should not. They are a few lines each, which
 * is exactly why they are worth a test: nobody re-reads a few lines.
 *
 * The server enforces both rules independently — this is the UX half only — but
 * a guard that lets an expired licence through means the user sees a screen
 * that then fails on every request, which reads as the product being broken
 * rather than as the licence having lapsed.
 */
describe('route guards', () => {
  let auth: AuthService;

  // Both guards ignore the route and state they are handed — they answer from
  // AuthService alone — so empty ones are honest stand-ins rather than a mock
  // of anything.
  const route = new ActivatedRouteSnapshot();
  const state = { url: '/somewhere' } as RouterStateSnapshot;

  const run = (guard: CanActivateFn): boolean | UrlTree =>
    TestBed.runInInjectionContext(() => guard(route, state)) as boolean | UrlTree;

  /** An access token whose payload carries the given permissions. */
  const tokenWith = (permissions: string[]): string => {
    const json = JSON.stringify({ permission: permissions });
    const b64 = btoa(String.fromCharCode(...new TextEncoder().encode(json)))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
    return `header.${b64}.signature`;
  };

  const runWithData = (data: Record<string, unknown>): boolean | UrlTree => {
    const withData = new ActivatedRouteSnapshot();
    withData.data = data;
    return TestBed.runInInjectionContext(() =>
      permissionGuard(withData, state),
    ) as boolean | UrlTree;
  };

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    auth = TestBed.inject(AuthService);
  });

  describe('authGuard', () => {
    it('lets a signed-in user through when session state returns valid orgs', async () => {
      auth.accessToken.set('a-token');
      localStorage.setItem('bb.orgId', 'org-1');

      const runPromise = run(authGuard);
      
      const httpMock = TestBed.inject(HttpTestingController);
      httpMock.expectOne('/api/auth/session').flush({
        lastAccessedOrgId: 'org-1',
        organizations: [{ orgId: 'org-1', orgName: 'HQ', roleName: 'Owner' }]
      });

      expect(await runPromise).toBe(true);
    });

    it('sends a signed-out user to login', async () => {
      auth.accessToken.set(null);

      const result = await run(authGuard);

      expect(result).toBeInstanceOf(UrlTree);
      expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/login');
    });
  });

  describe('licenseActiveGuard', () => {
    it('lets an active licence through', () => {
      auth.licenseStatus.set('Active');

      expect(run(licenseActiveGuard)).toBe(true);
    });

    it('lets a trial through — a trial is not an expired licence', () => {
      auth.licenseStatus.set('Trial');

      expect(run(licenseActiveGuard)).toBe(true);
    });

    it('redirects an expired licence to the expired page', () => {
      auth.licenseStatus.set('Expired');

      const result = run(licenseActiveGuard);

      expect(result).toBeInstanceOf(UrlTree);
      expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/expired');
    });

    it('lets an unknown status through rather than locking the user out', () => {
      // Only the exact string 'Expired' blocks. A status this build has not
      // heard of — a tier added server-side after the app shipped — must not
      // strand a paying customer on the expired page; the server refuses what
      // it should refuse.
      auth.licenseStatus.set('SomeFutureTier');

      expect(run(licenseActiveGuard)).toBe(true);
    });

    it('lets a user with no licence status through', () => {
      // Nothing stored yet, which is the state before the first sign-in
      // completes. authGuard is what stops that user, not this one.
      auth.licenseStatus.set(null);

      expect(run(licenseActiveGuard)).toBe(true);
    });
  });

  describe('permissionGuard', () => {
    it('lets a route through when the user holds its permission', () => {
      auth.accessToken.set(tokenWith(['inventory.view', 'inventory.edit']));

      expect(runWithData({ permission: 'inventory.view' })).toBe(true);
    });

    it('sends a user without the permission Home rather than to a broken screen', () => {
      auth.accessToken.set(tokenWith(['contacts.view']));

      const result = runWithData({ permission: 'settings.view' });

      expect(result).toBeInstanceOf(UrlTree);
      expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/dashboard');
    });

    it('allows a route that declares no permission', () => {
      // Home declares none, and every role has to be able to land somewhere.
      auth.accessToken.set(tokenWith([]));

      expect(runWithData({})).toBe(true);
    });

    it('does not treat a permission in another module as a match', () => {
      // inventory.view must not open a settings screen. Obvious, and exactly
      // the kind of thing a prefix comparison would get wrong.
      auth.accessToken.set(tokenWith(['inventory.view']));

      expect(runWithData({ permission: 'settings.view' })).toBeInstanceOf(UrlTree);
    });

    it('does not let .view stand in for .edit', () => {
      auth.accessToken.set(tokenWith(['accounting.view']));

      expect(runWithData({ permission: 'accounting.edit' })).toBeInstanceOf(UrlTree);
    });
  });
});
