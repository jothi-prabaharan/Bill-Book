import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter, withHashLocation } from '@angular/router';
import { appRoutes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(),
    provideRouter(appRoutes, withHashLocation())
  ]
};
