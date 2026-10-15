import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideZoneChangeDetection } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { provideRouter, withInMemoryScrolling } from '@angular/router';
import { API_BASE_URL, apiBaseUrlInterceptor, resolveApiBaseUrl } from '@bill-book/api-client';
import { AppComponent } from './app/app.component';
import { appRoutes } from './app/app.routes';

function getDefaultApiBaseUrl(): string {
  if (typeof window === 'undefined') return 'http://localhost:4500';
  const customApi = (window as unknown as { __BB_API_URL__?: string })?.__BB_API_URL__;
  if (customApi) return customApi.replace(/\/+$/, '');

  const { protocol, hostname, port } = window.location;
  if (hostname === 'localhost' || hostname === '127.0.0.1') {
    return 'http://localhost:4500';
  }
  if (port) {
    return `${protocol}//${hostname}:4500`;
  }
  return '';
}

bootstrapApplication(AppComponent, {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(
      appRoutes,
      withInMemoryScrolling({
        scrollPositionRestoration: 'top',
        anchorScrolling: 'enabled',
      }),
    ),
    { provide: API_BASE_URL, useValue: resolveApiBaseUrl(getDefaultApiBaseUrl()) },
    provideHttpClient(withInterceptors([apiBaseUrlInterceptor])),
  ],
}).catch((err) => console.error(err));

