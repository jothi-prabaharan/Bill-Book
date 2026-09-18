// Components
export * from './lib/shell/shell.component';
export * from './lib/nav/shell-nav.component';
export * from './lib/topbar/shell-topbar.component';
export * from './lib/breadcrumb/shell-breadcrumb.component';
export * from './lib/subpanel/shell-subpanel.component';

// Services
export * from './lib/menu.service';
export * from './lib/panel-state.service';
export * from './lib/board-state.service';
export * from './lib/favourites.service';
export * from './lib/notifications.service';

// Navigation contracts: the server's menu tree, and the static fallback
export * from './lib/menu.models';
export * from './lib/shell-screens';

// Models / Types
export type { NavItem } from './lib/nav/shell-nav.component';
export type { DocGroup, DocGroupItem } from './lib/topbar/shell-topbar.component';
export type { BreadcrumbItem, ExportFormat } from './lib/breadcrumb/shell-breadcrumb.component';
