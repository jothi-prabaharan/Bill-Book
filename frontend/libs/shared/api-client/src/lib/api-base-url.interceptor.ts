import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { API_BASE_URL } from './api-base-url';
import { APP_HEADER, PLATFORM_HEADER, resolveAppClient, resolveAppPlatform } from './client-platform';

/**
 * Rewrites root-relative API calls onto the configured origin, and attaches
 * application context headers:
 * - `X-App`: 'electron' | 'ionic' | 'web'
 * - `X-Platform`: 'win' | 'chrome' | 'edge' | 'android' | 'ios' | 'ubuntu' | 'mac'
 *
 * Call sites use '/api/...' so the dev server proxy can intercept them. Deployed
 * environments have no such proxy, so the origin from the environment file is
 * applied here rather than at every call site.
 */
export const apiBaseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  const baseUrl = inject(API_BASE_URL);

  const isApi = req.url.startsWith('/api/') || (!!baseUrl && req.url.startsWith(baseUrl));

  if (!isApi) {
    return next(req);
  }

  const client = resolveAppClient();
  const platform = resolveAppPlatform(client);

  const url = baseUrl && req.url.startsWith('/api/')
    ? `${baseUrl.replace(/\/$/, '')}${req.url}`
    : req.url;

  return next(
    req.clone({
      url,
      setHeaders: {
        [APP_HEADER]: client,
        [PLATFORM_HEADER]: platform,
      },
    }),
  );
};
