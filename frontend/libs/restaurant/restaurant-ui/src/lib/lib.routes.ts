import { Route } from '@angular/router';
import { RestaurantSettingsPage } from './restaurant-settings/restaurant-settings.page';

export const restaurantUiRoutes: Route[] = [
  { path: 'settings', component: RestaurantSettingsPage }
];
