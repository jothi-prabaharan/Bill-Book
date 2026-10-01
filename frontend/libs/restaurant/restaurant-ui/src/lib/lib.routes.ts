import { Route } from '@angular/router';
import { RestaurantSettingsPage } from './restaurant-settings/restaurant-settings.page';
import { RestaurantMenuPage } from './restaurant-menu/restaurant-menu.page';

export const restaurantUiRoutes: Route[] = [
  { path: 'settings', component: RestaurantSettingsPage },
  { path: 'menu', component: RestaurantMenuPage }
];
