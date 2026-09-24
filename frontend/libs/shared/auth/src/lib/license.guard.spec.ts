import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  Route,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { describe, beforeEach, expect, it } from 'vitest';
import { AuthService } from './auth.service';
import { authGuard, licenseActiveGuard, permissionGuard } from './license.guard';
import { PageAccess } from './page-access';
import { SessionContext, SessionContextService } from './session-context.service';

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

  const run = (guard: CanActivateFn): Promise<boolean | UrlTree> =>
    Promise.resolve(TestBed.runInInjectionContext(() => guard(route, state)) as boolean | UrlTree | Promise<boolean | UrlTree>);

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
    const withLicence = (licenseStatus: string) => {
      TestBed.overrideProvider(SessionContextService, { useValue: stubSession({ licenseStatus }) });
    };

    it.each(['Active', 'Trial'])('lets a %s licence through', async (status) => {
      withLicence(status);

      expect(await run(licenseActiveGuard)).toBe(true);
    });

    it.each(['Expired', 'Suspended', 'NotLicensed'])('sends a %s licence to the expired page', async (status) => {
      withLicence(status);

      const result = await run(licenseActiveGuard);

      expect(result).toBeInstanceOf(UrlTree);
      expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/expired');
    });
  });

  describe('permissionGuard', () => {
    const routeWith = (access: PageAccess | undefined): ActivatedRouteSnapshot => {
      const snapshot = new ActivatedRouteSnapshot();
      (snapshot as { routeConfig: Route | null }).routeConfig = access === undefined ? {} : { data: { access } };
      return snapshot;
    };

    const guard = async (access: PageAccess | undefined, permissions: string[]): Promise<boolean | UrlTree> => {
      auth.accessToken.set('a-token');
      TestBed.overrideProvider(SessionContextService, { useValue: stubSession({ permissions }) });
      return (await TestBed.runInInjectionContext(() => permissionGuard(routeWith(access), state))) as boolean | UrlTree;
    };

    const url = (result: boolean | UrlTree) => TestBed.inject(Router).serializeUrl(result as UrlTree);

    it('lets a route through when the user holds its permission', async () => {
      expect(await guard({ permission: 'inventory.view' }, ['inventory.view', 'inventory.edit'])).toBe(true);
    });

    it('shows the no-access page, naming the permission, rather than bouncing to Home', async () => {
      expect(url(await guard({ permission: 'settings.view' }, ['contacts.view']))).toBe('/no-access?need=settings.view');
    });

    it('refuses a route that declares nothing: deny by default', async () => {
      expect(url(await guard(undefined, ['settings.view']))).toBe('/no-access?reason=undeclared');
    });

    it('lets any signed-in user open a signed-in page', async () => {
      expect(await guard({ signedIn: true }, [])).toBe(true);
    });

    it('does not let .view stand in for .edit', async () => {
      expect(await guard({ permission: 'accounting.edit' }, ['accounting.view'])).toBeInstanceOf(UrlTree);
    });
  });
});

/** A session context that answers at once, for the guards. */
function stubSession(overrides: Partial<SessionContext>): Pick<SessionContextService, 'ensure'> {
  const context: SessionContext = {
    displayName: 'Priya',
    email: 'priya@example.com',
    branchName: 'Chennai',
    branchCode: 'CHN',
    app: 'RetailErp',
    licenseStatus: 'Active',
    licenseExpiry: null,
    expiryIsBranchLevel: false,
    permissions: [],
    apps: [],
    ...overrides,
  };
  return { ensure: () => Promise.resolve(context) };
}
