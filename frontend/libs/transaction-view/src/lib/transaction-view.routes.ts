import { Routes } from '@angular/router';
import { TRANSACTION_KINDS } from './transaction-kinds';

/**
 * The view page's route for each transaction, at the module's view permission: `sales/quotes/:id/view`,
 * `purchase/bills/:id/view`, `banking/receive-money/:id/view`, `accounting/journals/:id/view`.
 *
 * **One page, many routes**, built from the kind table so a route and a link to it cannot disagree. The web
 * app spreads these ahead of the sales and purchase screens, which are lazy and would otherwise be asked for
 * a path they do not know.
 */
export const transactionViewRoutes: Routes = TRANSACTION_KINDS.map((info) => ({
  path: [...info.base, ':id', 'view'].join('/').replace(/^\//, ''),
  loadComponent: () => import('./transaction-view.page').then((m) => m.TransactionViewPage),
  data: { viewKind: info.kind, depth: 2, access: { permission: `${info.module}.view` } },
}));
