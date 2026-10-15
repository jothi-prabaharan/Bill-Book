import { Routes } from '@angular/router';
import { SignupPage } from '@bill-book/auth';
import { HomePageComponent } from './home/home-page.component';

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
          const { protocol, hostname, port } = window.location;
          let targetPort = '4200';
          if (port && port.endsWith('202')) {
            targetPort = port.replace(/202$/, '200');
          } else if (port === '5202') targetPort = '5200';
          else if (port === '6202') targetPort = '6200';
          else if (port === '7202') targetPort = '7200';

          // Prevent loop: strip any repeated app. or www. prefixes, do not prepend app. if already on app.
          const cleanHost = hostname.replace(/^(app\.)+/, '').replace(/^www\./, '');
          const appHost = hostname.startsWith('app.') ? hostname : `app.${cleanHost}`;

          const targetUrl =
            hostname === 'localhost' || hostname === '127.0.0.1' || port
              ? `${protocol}//${hostname}:${targetPort}/login`
              : `${protocol}//${appHost}/login`;

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


