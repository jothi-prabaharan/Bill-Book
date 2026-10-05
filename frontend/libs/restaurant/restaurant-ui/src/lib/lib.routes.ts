import { Route } from '@angular/router';

/**
 * The Restaurant rail (TK-143), one route per menu row in Master's seed. Every
 * route needs a Restaurant branch; Master's menu also hides tables, the
 * kitchen screen and recipes while their switch in Settings › Restaurant is off.
 */
const restaurant = (permission = 'restaurant.view') => ({ access: { permission, businessTypes: ['Restaurant'] } });

export const restaurantUiRoutes: Route[] = [
  { path: '', pathMatch: 'full', redirectTo: 'orders' },
  {
    path: 'settings',
    loadComponent: () => import('./restaurant-settings/restaurant-settings.page').then((m) => m.RestaurantSettingsPage),
    data: restaurant(),
  },
  {
    path: 'orders',
    loadComponent: () => import('./restaurant-order/restaurant-order.page').then((m) => m.RestaurantOrderPage),
    data: restaurant(),
  },
  {
    path: 'tables',
    loadComponent: () => import('./restaurant-tables/restaurant-tables.page').then((m) => m.RestaurantTablesPage),
    data: restaurant(),
  },
  {
    path: 'kitchen',
    loadComponent: () => import('./kitchen-display/kitchen-display.page').then((m) => m.KitchenDisplayPage),
    data: restaurant(),
  },
  {
    path: 'billing',
    loadComponent: () => import('./restaurant-billing/restaurant-billing.page').then((m) => m.RestaurantBillingPage),
    data: restaurant(),
  },
  {
    path: 'menu',
    loadComponent: () => import('./restaurant-menu/restaurant-menu.page').then((m) => m.RestaurantMenuPage),
    data: restaurant(),
  },
  {
    path: 'recipes',
    loadComponent: () => import('./restaurant-recipes/restaurant-recipes.page').then((m) => m.RestaurantRecipesPage),
    data: restaurant(),
  },
  {
    path: 'reports',
    loadComponent: () => import('./restaurant-reports/restaurant-reports.page').then((m) => m.RestaurantReportsPage),
    data: restaurant(),
  },
];
