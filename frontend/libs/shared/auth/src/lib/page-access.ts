import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router } from '@angular/router';
import { APP_ID, AppId } from './app-id';
import { AuthService } from './auth.service';
import { SessionContext, SessionContextService, isLicenceOpen } from './session-context.service';

/**
 * What a page under the shell needs (H0.3, TK-44), declared as `data.access`
 * on its route:
 *
 * - `{ permission: 'payroll.view' }`: the user must hold it in the current app;
 * - `{ signedIn: true }`: any signed-in user of the app (the dashboard, a profile);
 * - either may add `apps: [...]` when a shared lib's route may be mounted only
 *   by some apps.
 *
 * A route declares nothing: it is refused. Deny by default is the point, since
 * a page nobody remembered to tag would otherwise be open to everyone.
 */
export type PageAccess =
  | { permission: string; apps?: readonly AppId[] }
  | { signedIn: true; apps?: readonly AppId[] };

/** Why a page was refused, for the no-access page and for tests. */
export type AccessRefusal =
  | { kind: 'signIn' }
  | { kind: 'licence' }
  | { kind: 'app' }
  | { kind: 'undeclared' }
  | { kind: 'permission'; permission: string };

/**
 * The five checks, in order, stopping at the first failure. Pure, so the order
 * and every branch are tested without a router.
 */
export function decideAccess(
  signedIn: boolean,
  context: SessionContext | null,
  app: AppId,
  access: PageAccess | undefined,
): AccessRefusal | null {
  if (!signedIn || context === null) {
    return { kind: 'signIn' };
  }
  if (!isLicenceOpen(context.licenseStatus)) {
    return { kind: 'licence' };
  }
  if (access?.apps && !access.apps.includes(app)) {
    return { kind: 'app' };
  }
  if (access === undefined) {
    return { kind: 'undeclared' };
  }
  if ('permission' in access && !context.permissions.includes(access.permission)) {
    return { kind: 'permission', permission: access.permission };
  }
  return null;
}

/**
 * The route's own `data.access`, or the nearest ancestor's. A lazy module's
 * parent route declares access for every child that does not declare its own,
 * so `sales.view` on `sales` covers the sales screens.
 */
export function accessOf(route: ActivatedRouteSnapshot): PageAccess | undefined {
  for (let at: ActivatedRouteSnapshot | null = route; at !== null; at = at.parent) {
    // The shell's own route is where inheritance stops: nothing it declares
    // may stand in for a page that declares nothing.
    if (at.routeConfig?.data?.['shellRoot'] === true) {
      return undefined;
    }
    const access = at.routeConfig?.data?.['access'] as PageAccess | undefined;
    if (access !== undefined) {
      return access;
    }
  }
  return undefined;
}

/**
 * The shell's page guard. `shellRoutes` attaches it; no app lists it by hand.
 *
 * A session minted for another app (a token left by the other app on the same
 * origin) is first switched to this app, on the same branch. A user with no
 * role in this app then lands on the no-access page rather than on /login.
 */
export const pageGuard: CanActivateFn = async (route) => {
  const auth = inject(AuthService);
  const session = inject(SessionContextService);
  const router = inject(Router);
  const app = inject(APP_ID);

  if (!auth.isAuthenticated()) {
    return router.parseUrl('/login');
  }

  let context: SessionContext;
  try {
    context = await session.ensure();
    if (context.app !== app) {
      await auth.switchApp(app);
      context = await session.ensure();
    }
  } catch {
    return router.createUrlTree(['/no-access'], { queryParams: { reason: 'app' } });
  }

  const refusal = decideAccess(true, context, app, accessOf(route));
  if (refusal === null) {
    return true;
  }

  switch (refusal.kind) {
    case 'signIn':
      return router.parseUrl('/login');
    case 'licence':
      return router.parseUrl('/expired');
    case 'permission':
      return router.createUrlTree(['/no-access'], { queryParams: { need: refusal.permission } });
    default:
      return router.createUrlTree(['/no-access'], { queryParams: { reason: refusal.kind } });
  }
};
