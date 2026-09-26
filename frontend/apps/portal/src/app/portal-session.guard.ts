import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PortalSession } from './portal-session';

/**
 * A portal page opens only with a live session (TK-94): the token is renewed
 * from the kept code when it is spent, and a withdrawn link goes to the expired page.
 */
export const portalSessionGuard: CanActivateFn = async () => {
  const router = inject(Router);
  return (await inject(PortalSession).ensure()) ? true : router.parseUrl('/expired');
};
