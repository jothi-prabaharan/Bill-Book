import { Routes } from '@angular/router';
import { HomePageComponent } from './home/home-page.component';

export const appRoutes: Routes = [
  {
    path: '',
    component: HomePageComponent,
    title: 'Bill-Book — Retail ERP, POS & Indian Accounting SaaS',
  },
  {
    path: 'signup',
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

          const targetUrl =
            hostname === 'localhost' || hostname === '127.0.0.1' || port
              ? `${protocol}//${hostname}:${targetPort}/signup`
              : `${protocol}//app.${hostname.replace(/^www\./, '')}/signup`;

          window.location.href = targetUrl;
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

