import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import {
  DEFAULT_FORMAT_SETTINGS,
  FormatSettings,
  formatDate,
  formatMoney,
  formatNumber,
} from './format-settings';

/**
 * The branch's formats, fetched once and shared by every screen.
 *
 * **Fetched, not decoded.** The formats belong to the organization, and the
 * organization is named by the token the API already holds — so nothing here
 * reads the JWT, and no screen has to know an org id to render a date. That is
 * also why the endpoint takes no route parameter.
 *
 * **One in-flight request, not one per screen.** `load()` keeps the promise
 * rather than a boolean, so eight components constructed in the same tick share
 * a single GET instead of racing eight. Callers may await it or ignore it: the
 * signal starts at the shipped defaults, so a template that renders before the
 * response lands shows the common case rather than blanks.
 */
@Injectable({ providedIn: 'root' })
export class FormatSettingsService {
  private readonly http = inject(HttpClient);

  private readonly settingsSignal = signal<FormatSettings>(DEFAULT_FORMAT_SETTINGS);

  /** In flight or resolved; null until the first `load()`. */
  private inFlight: Promise<FormatSettings> | null = null;

  /** The formats as they stand — defaults until the fetch resolves. */
  readonly settings = this.settingsSignal.asReadonly();

  /** True once the server's answer has replaced the defaults. */
  readonly isLoaded = computed(() => this.settingsSignal() !== DEFAULT_FORMAT_SETTINGS);

  async load(): Promise<FormatSettings> {
    this.inFlight ??= this.fetch();
    return this.inFlight;
  }

  /**
   * Drops the cache so the next `load()` asks again. Switching organization
   * changes the branch, and a branch may format differently from the one signed
   * out of.
   */
  reset(): void {
    this.inFlight = null;
    this.settingsSignal.set(DEFAULT_FORMAT_SETTINGS);
  }

  formatDate(value: string | null | undefined): string {
    return formatDate(value, this.settingsSignal().datePattern);
  }

  formatMoney(value: number | null | undefined): string {
    return formatMoney(value, this.settingsSignal());
  }

  formatQuantity(value: number | null | undefined): string {
    const settings = this.settingsSignal();
    return formatNumber(value, settings.quantityDecimals, settings.currencyMask);
  }

  formatUnitPrice(value: number | null | undefined): string {
    const settings = this.settingsSignal();
    return formatNumber(value, settings.unitPriceDecimals, settings.currencyMask);
  }

  private async fetch(): Promise<FormatSettings> {
    try {
      const settings = await new Promise<FormatSettings>((resolve, reject) => {
        this.http.get<FormatSettings>('/api/formats').subscribe({
          next: resolve,
          error: reject,
        });
      });

      this.settingsSignal.set(settings);
      return settings;
    } catch {
      // A screen that cannot draw an amount is worse than one drawing it in the
      // shipped default, so a failed fetch leaves the defaults standing. The
      // promise is cleared so a later screen retries rather than inheriting the
      // failure forever.
      this.inFlight = null;
      return DEFAULT_FORMAT_SETTINGS;
    }
  }
}
