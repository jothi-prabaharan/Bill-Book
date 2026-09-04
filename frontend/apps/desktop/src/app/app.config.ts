import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  API_BASE_URL,
  apiBaseUrlInterceptor,
  resolveApiBaseUrl,
} from '@bill-book/api-client';
import { provideRouter, withHashLocation } from '@angular/router';
import { appRoutes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    // The desktop shell is the one app that is *never* same-origin: it loads
    // from the filesystem, so an unrewritten '/api/...' resolves against
    // file:// and fails. It has no build-time default worth writing either —
    // a till talks to whichever server that shop runs — so the origin comes
    // from the runtime config the installer writes.
    { provide: API_BASE_URL, useValue: resolveApiBaseUrl('') },
    provideHttpClient(withInterceptors([apiBaseUrlInterceptor])),
    provideRouter(appRoutes, withHashLocation())
  ]
};
