import { Route } from '@angular/router';
import { RestaurantSettingsPage } from './restaurant-settings/restaurant-settings.page';
import { RestaurantMenuPage } from './restaurant-menu/restaurant-menu.page';
import { RestaurantTablesPage } from './restaurant-tables/restaurant-tables.page';
import { RestaurantOrderPage } from './restaurant-order/restaurant-order.page';
import { KitchenDisplayPage } from './kitchen-display/kitchen-display.page';
import { RestaurantBillingPage } from './restaurant-billing/restaurant-billing.page';

export const restaurantUiRoutes: Route[] = [
  { path: 'settings', component: RestaurantSettingsPage },
  { path: 'menu', component: RestaurantMenuPage },
  { path: 'tables', component: RestaurantTablesPage },
  { path: 'order', component: RestaurantOrderPage },
  { path: 'kds', component: KitchenDisplayPage },
  { path: 'billing', component: RestaurantBillingPage }
];
