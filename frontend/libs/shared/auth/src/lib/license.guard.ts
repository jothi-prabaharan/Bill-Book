import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { APP_ID } from './app-id';
import { AuthService } from './auth.service';
import { accessOf, decideAccess } from './page-access';
import { SessionContextService, isLicenceOpen } from './session-context.service';

/** Blocks unauthenticated users out to /login. */
export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated()) {
    return router.parseUrl('/login');
  }

  try {
    const session = await auth.fetchSessionState();
    
    if (!session.organizations || session.organizations.length === 0) {
      auth.logout();
      return router.createUrlTree(['/login'], { queryParams: { error: 'revoked' } });
    }

    const localOrgId = localStorage.getItem('bb.orgId');
    const hasAccessToLocalOrg = session.organizations.some(o => o.orgId === localOrgId);

    if (!hasAccessToLocalOrg || (session.lastAccessedOrgId && session.lastAccessedOrgId !== localOrgId)) {
      const targetOrgId = session.lastAccessedOrgId || session.organizations[0].orgId;
      await auth.switchOrganization(targetOrgId);
    }

    return true;
  } catch (_err) {
    auth.logout();
    return router.parseUrl('/login');
  }
};

/**
 * The licence gate, over the session context (TK-44): the current app's
 * licence must be Active or Trial, or the user lands on /expired. The shell's
 * `pageGuard` makes the same check as its second step; this remains for routes
 * outside the shell.
 */
export const licenseActiveGuard: CanActivateFn = async () => {
  const session = inject(SessionContextService);
  const router = inject(Router);

  try {
    const context = await session.ensure();
    return isLicenceOpen(context.licenseStatus) ? true : router.parseUrl('/expired');
  } catch {
    return router.parseUrl('/login');
  }
};

/**
 * Refuses a route the user holds no permission for, over the session context
 * (TK-44). **Deny by default**: a route that declares no `data.access` is
 * refused. Pages under the shell get this from `pageGuard`, which `shellRoutes`
 * attaches; this is the same rule for a route outside it.
 */
export const permissionGuard: CanActivateFn = async (route) => {
  const auth = inject(AuthService);
  const session = inject(SessionContextService);
  const router = inject(Router);
  const app = inject(APP_ID);

  let context = null;
  try {
    context = auth.isAuthenticated() ? await session.ensure() : null;
  } catch {
    context = null;
  }

  const refusal = decideAccess(auth.isAuthenticated(), context, app, accessOf(route));
  if (refusal === null) {
    return true;
  }
  return refusal.kind === 'signIn'
    ? router.parseUrl('/login')
    : router.createUrlTree(['/no-access'], {
        queryParams: refusal.kind === 'permission' ? { need: refusal.permission } : { reason: refusal.kind },
      });
};
