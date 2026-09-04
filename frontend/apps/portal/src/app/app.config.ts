import {
  ApplicationConfig,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { appRoutes } from './app.routes';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  API_BASE_URL,
  apiBaseUrlInterceptor,
  resolveApiBaseUrl,
} from '@bill-book/api-client';
import { authInterceptor } from '@bill-book/auth';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(appRoutes),
    // Same origin by default — the portal is served from behind the Gateway
    // like the main app — and overridable per deployment without a rebuild.
    // Portal had no base-url interceptor at all, so a split deployment would
    // have sent every call to the CDN serving the bundle.
    { provide: API_BASE_URL, useValue: resolveApiBaseUrl('') },
    provideHttpClient(withInterceptors([apiBaseUrlInterceptor, authInterceptor])),
  ],
};
