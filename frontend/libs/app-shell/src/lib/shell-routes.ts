import { Route, Routes } from '@angular/router';
import { APP_ID, AppId, PageAccess, authGuard, pageGuard } from '@bill-book/auth';
import { NoAccessPage } from './no-access/no-access.page';
import { ShellComponent } from './shell/shell.component';

export interface ShellRoutesOptions {
  /** The app mounting the shell. Provide the same value as `APP_ID` in `app.config.ts`. */
  app: AppId;
  /** The app's pages. Every one declares `data.access`, or the audit fails. */
  children: Routes;
}

/**
 * The shell route with its guards already attached (H0.3, TK-44). **An app
 * passes its pages and never lists a guard itself**, so a new app cannot forget
 * page validation.
 *
 * - `authGuard` on the shell: a signed-in user with a branch.
 * - `pageGuard` on every page: licence, app, declared access, permission, in
 *   that order (`decideAccess`).
 * - `/no-access` is added, so every app has somewhere to explain a refusal.
 */
export function shellRoutes({ app, children }: ShellRoutesOptions): Route {
  return {
    path: '',
    component: ShellComponent,
    providers: [{ provide: APP_ID, useValue: app }],
    canActivate: [authGuard],
    canActivateChild: [pageGuard],
    data: { shellRoot: true, app },
    children: [
      { path: 'no-access', component: NoAccessPage, data: { access: { signedIn: true } satisfies PageAccess } },
      ...children,
    ],
  };
}

/**
 * The routes under the shell that declare no `data.access`, themselves or
 * through a parent (TK-44). Each app's route spec asserts this is empty: the
 * frontend twin of the backend's `EndpointGuardAudit`, for the same reason. A
 * missing tag is invisible in a file nobody is reading.
 *
 * Takes an app's whole route array and finds the shell in it, or a lib's own
 * route array (then every route is a page of its own).
 */
export function auditShellRoutes(routes: Routes): string[] {
  const shell = routes.find((r) => r.data?.['shellRoot'] === true);
  const pages = shell?.children ?? routes;
  const open: string[] = [];
  walk(pages, '', false, open);
  return open;
}

function walk(routes: Routes, prefix: string, inherited: boolean, open: string[]): void {
  for (const route of routes) {
    const path = [prefix, route.path ?? ''].filter((p) => p !== '').join('/');
    const declared = inherited || route.data?.['access'] !== undefined;

    if (route.redirectTo !== undefined) {
      continue;
    }

    const isPage =
      route.component !== undefined || route.loadComponent !== undefined || route.loadChildren !== undefined;

    if (isPage && !declared) {
      open.push(path === '' ? '(empty path)' : path);
    }

    if (route.children) {
      walk(route.children, path, declared, open);
    }
  }
}
