import { InjectionToken } from '@angular/core';

/**
 * Origin the API is served from, without a trailing slash. Empty means same origin.
 *
 * Each app provides this from its own environment file. The token exists so that
 * this library never imports app-level config, which would invert the
 * app -> lib dependency direction.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => '',
});
