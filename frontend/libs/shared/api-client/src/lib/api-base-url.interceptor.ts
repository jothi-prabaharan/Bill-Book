import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from './api-base-url';

/**
 * Rewrites root-relative API calls onto the configured origin.
 *
 * Call sites use '/api/...' so the dev server proxy can intercept them. Deployed
 * environments have no such proxy, so the origin from the environment file is
 * applied here rather than at every call site.
 */
export const apiBaseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  const baseUrl = inject(API_BASE_URL);

  if (!baseUrl || !req.url.startsWith('/api/')) {
    return next(req);
  }

  return next(req.clone({ url: `${baseUrl.replace(/\/$/, '')}${req.url}` }));
};
