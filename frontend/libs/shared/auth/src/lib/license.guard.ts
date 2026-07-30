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
