import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Blocks unauthenticated users out to /login. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isAuthenticated() ? true : router.parseUrl('/login');
};

/**
 * The trial-expiry gate. Sits above every feature route: an expired licence
 * cancels navigation and lands on the empty expired page instead — so a
 * hand-typed URL like /accounting/journal shows nothing. The server enforces
 * the same rule with 403 LicenseExpired; this guard is only the UX half.
 */
export const licenseActiveGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.isLicenseExpired() ? router.parseUrl('/expired') : true;
};

/**
 * Refuses a route the user holds no permission for, sending them Home instead of
 * to a screen that will answer 403 on its first request.
 *
 * The permission comes from the route's own `data.permission`, so a route
 * declares what it needs beside where it goes. A route that declares nothing is
 * allowed — the same default the shell uses for Home.
 *
 * **The UX half only**, exactly like licenseActiveGuard. The token is in the
 * browser and a determined user can edit what the browser reads out of it; the
 * server checks the same claims against a signature on every request, and that
 * is the check that decides anything.
 */
export const permissionGuard: CanActivateFn = (route) => {
  const required = route.data?.['permission'] as string | undefined;
  if (!required) {
    return true;
  }

  const auth = inject(AuthService);
  const router = inject(Router);

  return auth.has(required) ? true : router.parseUrl('/dashboard');
};
