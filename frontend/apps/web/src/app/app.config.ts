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

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(appRoutes),

    // The app owns environment config; libs receive it through this token.
    // The deployment's value when it set one, the build's otherwise. One
    // artifact can then be promoted from UAT to production without a rebuild,
    // and no hostname has to live in the repository. See resolveApiBaseUrl.
    { provide: API_BASE_URL, useValue: resolveApiBaseUrl(environment.apiBaseUrl) },

    // Order matters: rewrite the URL onto the configured origin first, then
    // attach the bearer token.
    provideHttpClient(withInterceptors([apiBaseUrlInterceptor, authInterceptor])),
  ],
};
