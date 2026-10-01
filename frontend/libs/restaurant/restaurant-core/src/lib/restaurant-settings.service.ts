import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RestaurantSettingsView } from './restaurant-settings';

@Injectable({ providedIn: 'root' })
export class RestaurantSettingsService {
  private readonly http = inject(HttpClient);


  getSettings(): Observable<RestaurantSettingsView> {
    return this.http.get<RestaurantSettingsView>('/api/restaurant/settings');
  }

  updateSettings(settings: RestaurantSettingsView): Observable<RestaurantSettingsView> {
    return this.http.put<RestaurantSettingsView>('/api/restaurant/settings', settings);
  }
}

