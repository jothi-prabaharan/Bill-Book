import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { PortalSession } from './portal-session';

/**
 * Sends the contact's portal token on the portal's own API calls (TK-69).
 * Registered after the staff interceptor so that, on `/api/portal/`, the
 * portal token is the one that goes.
 */
export const portalTokenInterceptor: HttpInterceptorFn = (req, next) => {
  const token = inject(PortalSession).token();
  return token && req.url.includes('/api/portal/')
    ? next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
    : next(req);
};
