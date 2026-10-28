import { Routes } from '@angular/router';
import { SignupPage } from '@bill-book/auth';
import { HomePageComponent } from './home/home-page.component';

export interface WindowLocationLike {
  protocol: string;
  hostname: string;
  port: string;
  search?: string;
}

/**
 * Resolves the destination URL for the ERP web application login screen.
 * Handles local ports, staging subdomains (stg -> stg-erp), UAT (uat -> uat-erp),
 * and production apex/www (eternalpathway.in -> app.eternalpathway.in).
 *
 * Prevents invalid nested subdomains such as app.stg.eternalpathway.in that cause
 * SSL wildcard certificate privacy errors (*.eternalpathway.in only matches single-level subdomains).
 */
export function resolveErpLoginUrl(loc: WindowLocationLike, path = '/login'): string {
  const { protocol, hostname, port, search = '' } = loc;
  const targetPath = `${path}${search}`;

  // 1. Localhost or explicit port-based environments:
  // Map website port (*202) -> ERP web app (*200) e.g. 5202 -> 5200, 4202 -> 4200
  if (hostname === 'localhost' || hostname === '127.0.0.1' || (port && port !== '80' && port !== '443')) {
    let targetPort = '4200';
    if (port && port.endsWith('202')) {
      targetPort = port.replace(/202$/, '200');
    } else if (port === '5202') {
      targetPort = '5200';
    } else if (port === '6202') {
      targetPort = '6200';
    } else if (port === '7202') {
      targetPort = '7200';
    }
    return `${protocol}//${hostname}:${targetPort}${targetPath}`;
  }

  // 2. Staging subdomains:
  // stg => website, stg-erp => ERP app, stg-doc => doc site
  // Prevents invalid multi-level subdomains like app.stg.eternalpathway.in
  if (/^(app\.)?stg(-doc)?\./i.test(hostname)) {
    const targetHost = hostname.replace(/^(app\.)?stg(-doc)?\./i, 'stg-erp.');
    return `${protocol}//${targetHost}${targetPath}`;
  }

  // 3. UAT subdomains:
  if (/^(app\.)?uat(-doc)?\./i.test(hostname)) {
    const targetHost = hostname.replace(/^(app\.)?uat(-doc)?\./i, 'uat-erp.');
    return `${protocol}//${targetHost}${targetPath}`;
  }

  // 4. Already on an ERP subdomain
  if (/^(stg-erp|uat-erp|erp|app)\./i.test(hostname)) {
    return `${protocol}//${hostname}${targetPath}`;
  }

  // 5. Production / Apex / www domain: eternalpathway.in -> app.eternalpathway.in
  const cleanHost = hostname.replace(/^www\./i, '');
  return `${protocol}//app.${cleanHost}${targetPath}`;
}

export const appRoutes: Routes = [
  {
    path: '',
    component: HomePageComponent,
    title: 'Bill-Book — Retail ERP, POS & Indian Accounting SaaS',
  },
  {
    path: 'signup',
    component: SignupPage,
    title: 'Start 14-Day Free Trial — Bill-Book',
  },
  {
    path: 'trial',
    redirectTo: 'signup',
  },
  {
    path: 'register',
    redirectTo: 'signup',
  },
  {
    path: 'login',
    canActivate: [
      () => {
        if (typeof window !== 'undefined') {
          const targetUrl = resolveErpLoginUrl(window.location);
          if (window.location.href !== targetUrl) {
            window.location.href = targetUrl;
          }
        }
        return false;
      },
    ],
    children: [],
  },
  {
    path: '**',
    redirectTo: '',
  },
];
