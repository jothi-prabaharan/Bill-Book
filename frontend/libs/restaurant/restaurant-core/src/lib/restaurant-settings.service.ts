import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { RESTAURANT_SETTINGS_API, RestaurantSettingsView, effectiveSettings } from './restaurant-settings';

/**
 * The branch's restaurant settings, read once and shared by every restaurant
 * screen. Saving through it refreshes the copy everyone reads.
 */
@Injectable({ providedIn: 'root' })
export class RestaurantSettingsService {
  private readonly http = inject(HttpClient);
  private pending: Promise<RestaurantSettingsView> | null = null;

  /** The settings as last read or saved; null until the first read. */
  readonly settings = signal<RestaurantSettingsView | null>(null);

  /** Reads the settings once; later calls share the first read. */
  load(force = false): Promise<RestaurantSettingsView> {
    if (force || !this.pending) {
      this.pending = firstValueFrom(this.http.get<RestaurantSettingsView>(RESTAURANT_SETTINGS_API)).then((s) => {
        this.settings.set(s);
        return s;
      });
      this.pending.catch(() => (this.pending = null));
    }

    return this.pending;
  }

  async save(settings: RestaurantSettingsView): Promise<RestaurantSettingsView> {
    const saved = await firstValueFrom(this.http.put<RestaurantSettingsView>(RESTAURANT_SETTINGS_API, effectiveSettings(settings)));
    this.settings.set(saved);
    this.pending = Promise.resolve(saved);
    return saved;
  }
}
