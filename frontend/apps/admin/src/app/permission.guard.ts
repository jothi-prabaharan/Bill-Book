import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '@bill-book/auth';

/**
 * Signed in and holding platform.view. This app has exactly one authenticated
 * area, so unlike the shared permissionGuard (which falls back to a
 * `/dashboard` this app does not have), both checks send an unqualified
 * visitor to the same place: `/login`.
 */
export const platformGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (!auth.isAuthenticated() || !auth.has('platform.view')) {
    return router.parseUrl('/login');
  }

  return true;
};
