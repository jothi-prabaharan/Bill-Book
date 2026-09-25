import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { APP_URLS } from '@bill-book/app-shell';
import { APP_ID, authInterceptor } from '@bill-book/auth';
import {
  API_BASE_URL,
  apiBaseUrlInterceptor,
  defaultAppUrls,
  resolveApiBaseUrl,
  resolveAppUrls,
} from '@bill-book/api-client';
import { environment } from '../environments/environment';
import { appRoutes } from './app.routes';

/**
 * `apps/school` (S0, TK-60): the School app. It shares every lib with
 * `apps/web`; what makes it School is `APP_ID`, which sign-in sends, the menu
 * filters by, and the shell's page guard checks the session against.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(appRoutes),
    { provide: APP_ID, useValue: 'School' },

    // The app switcher's destinations: the deployment's config.js, or the
    // development servers when served from localhost.
    { provide: APP_URLS, useValue: resolveAppUrls(defaultAppUrls()) },

    { provide: API_BASE_URL, useValue: resolveApiBaseUrl(environment.apiBaseUrl) },
    provideHttpClient(withInterceptors([apiBaseUrlInterceptor, authInterceptor])),
  ],
};
