import { signal } from '@angular/core';
import { FormControl, FormRecord } from '@angular/forms';
import { ratesOf } from '@bill-book/reporting-core';

/** What the rate master says for a currency pair on a date: the rate, or null when none is on file. */
export type MasterRateLookup = (currency: string, baseCurrency: string, on: string) => Promise<number | null>;

/**
 * The revaluation rate fields of the two Foreign Currency Gain or Loss pages (TK-220,
 * TK-221): one rate for each currency with a balance, each starting from the rate master's
 * rate for the as-at date and changeable by the person running the report. A rate already
 * typed is kept when the date changes; a currency that no longer has a balance loses its
 * field. Nothing here posts or stores a rate.
 */
export class FxRateFields {
  /** One field for each currency, by currency code. */
  readonly controls = new FormRecord<FormControl<number | null>>({});

  /** Where each currency's starting rate came from, for the hint under its field. */
  readonly sources = signal<Record<string, string>>({});

  /** `formatDate` writes the as-at date in the branch's pattern, for the hint under a field. */
  constructor(
    private readonly lookup: MasterRateLookup,
    private readonly formatDate: (iso: string) => string,
  ) {}

  /** The rates as a request sends them: only a figure above nought, by upper-case code. */
  values(): Record<string, number> {
    return ratesOf(this.controls.getRawValue());
  }

  /** Gives each currency a field and takes away the fields of currencies that are no longer listed. */
  sync(codes: string[]): void {
    for (const code of Object.keys(this.controls.controls)) {
      if (!codes.includes(code)) {
        this.controls.removeControl(code);
      }
    }

    for (const code of codes) {
      if (!this.controls.contains(code)) {
        this.controls.addControl(code, new FormControl<number | null>(null));
      }
    }
  }

  /**
   * Starts each field from the rate master's rate for the date, unless the person has typed
   * one. `current` says whether the answer is still wanted: a slow answer for an earlier date
   * must not overwrite the fields of a later one.
   */
  async startFromMaster(codes: string[], baseCurrency: string, on: string, current: () => boolean): Promise<void> {
    const found = await Promise.all(
      codes.map(async (code) => ({ code, rate: await this.lookup(code, baseCurrency, on).catch(() => null) })),
    );

    if (!current()) {
      return;
    }

    const sources: Record<string, string> = {};
    for (const { code, rate } of found) {
      const control = this.controls.controls[code];
      if (!control) {
        continue;
      }

      if (control.dirty) {
        sources[code] = 'The rate you entered.';
      } else if (rate !== null) {
        control.setValue(rate, { emitEvent: false });
        sources[code] = `The rate master's rate for ${this.formatDate(on)}.`;
      } else {
        control.setValue(null, { emitEvent: false });
        sources[code] = `No rate on file for ${this.formatDate(on)}: enter one.`;
      }
    }

    this.sources.set(sources);
  }

  /** The words under a currency's field. */
  hint(code: string): string {
    return this.sources()[code] ?? '';
  }
}
