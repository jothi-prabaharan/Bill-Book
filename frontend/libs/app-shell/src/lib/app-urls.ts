import { InjectionToken } from '@angular/core';
import { AppId } from '@bill-book/auth';

/**
 * Where each app lives for this deployment, for the app switcher (TK-44). An
 * app with no URL is listed but cannot be opened. Apps provide it in their
 * config; nothing is assumed about hosts or ports.
 */
export const APP_URLS = new InjectionToken<Partial<Record<AppId, string>>>('APP_URLS', {
  providedIn: 'root',
  factory: () => ({}),
});
