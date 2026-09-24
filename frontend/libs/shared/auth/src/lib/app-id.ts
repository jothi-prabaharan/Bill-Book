import { InjectionToken } from '@angular/core';

/** The four products a customer can buy (H0.1), named as the server names them. */
export type AppId = 'RetailErp' | 'School' | 'Hrms' | 'Payroll';

export const APP_IDS: readonly AppId[] = ['RetailErp', 'School', 'Hrms', 'Payroll'];

/** How each app is named on a screen. */
export const APP_LABELS: Record<AppId, string> = {
  RetailErp: 'RetailErp',
  School: 'School',
  Hrms: 'HRMS',
  Payroll: 'Payroll',
};

/**
 * Which app this build is (H0.3, TK-44). Each app provides it in its
 * `app.config.ts`; sign-in sends it, the menu asks for it, and the shell's page
 * guard checks the session against it.
 *
 * Defaults to RetailErp, so `apps/web` and anything that predates apps behaves
 * exactly as before without providing it.
 */
export const APP_ID = new InjectionToken<AppId>('APP_ID', {
  providedIn: 'root',
  factory: () => 'RetailErp',
});
