import { Routes } from '@angular/router';

/**
 * The till (TK-155), in the web app since Electron was dropped. Mounted at
 * `/pos` for a branch with the POS add-on; the route that mounts it declares
 * the access.
 */
export const posUiRoutes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pos-terminal/pos-terminal.component').then((m) => m.PosTerminalComponent),
  },
];
