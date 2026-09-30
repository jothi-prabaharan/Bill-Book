import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { authInterceptor } from '@bill-book/auth';
import {
  API_BASE_URL,
  apiBaseUrlInterceptor,
  resolveApiBaseUrl,
} from '@bill-book/api-client';
import { environment } from '../environments/environment';
import { appRoutes } from './app.routes';

/**
 * `apps/payroll` (H0.6, TK-47): the Payroll app. It shares every lib with
 * `apps/web`; what makes it Payroll is `APP_ID`, which sign-in sends, the menu
 * filters by, and the shell's page guard checks the session against.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(appRoutes),

    { provide: API_BASE_URL, useValue: resolveApiBaseUrl(environment.apiBaseUrl) },
    provideHttpClient(withInterceptors([apiBaseUrlInterceptor, authInterceptor])),
  ],
};
