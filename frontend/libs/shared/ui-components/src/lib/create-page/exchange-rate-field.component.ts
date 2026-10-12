import { ChangeDetectionStrategy, Component, computed, effect, inject, input, model, signal, untracked } from '@angular/core';
import { DocumentChoicesService } from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import { RateState, rateNote, rateText } from './create-page.model';

/**
 * A create page's read-only **Exchange rate** (TK-192, Shell.dc.html 776–786).
 *
 * The rate comes from the currency master for the document's currency and
 * date, and the page cannot change it: the design's pencil, which updated the
 * master from a document, is left out by the owner's decision of 12 October
 * 2026, because only a platform operator may change rates. With no rate on file
 * the field says so and `rate` is null, and the host refuses to save a
 * foreign-currency document without one.
 *
 * A saved document keeps the rate it was saved with — `locked` — and is never
 * looked up again: a rate is a snapshot at the transaction date, and a live
 * lookup would reprice history (CLAUDE.md, Multi-currency).
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-exchange-rate-field',
  standalone: true,
  templateUrl: './exchange-rate-field.component.html',
  styleUrl: './exchange-rate-field.component.scss',
})
export class ExchangeRateFieldComponent {
  private readonly choices = inject(DocumentChoicesService);
  private readonly formats = inject(FormatSettingsService);

  readonly label = input<string>('Exchange rate');
  readonly fieldId = input<string>('create-rate');

  /** The document's currency; empty or the base currency means a rate of 1. */
  readonly currency = input<string | null>(null);
  readonly date = input<string | null>(null);

  /** The rate the document carries, in base currency per unit of its own. */
  readonly rate = model<number | null>(null);

  /** A saved document's own rate, shown as it is and never looked up again. */
  readonly locked = input<boolean>(false);

  protected readonly state = signal<RateState>('base');
  private readonly rateDate = signal<string | null>(null);

  private readonly baseCurrency = computed(() => this.formats.settings().currencyCode);

  protected readonly shown = computed(() => rateText(this.rate(), this.state()));

  protected readonly note = computed(() =>
    rateNote({
      state: this.state(),
      currency: this.currency()?.trim().toUpperCase() || null,
      symbol: this.formats.settings().currencySymbol,
      rate: this.shown(),
      rateDate: this.formats.formatDate(this.rateDate()),
    }),
  );

  protected readonly isProblem = computed(() => this.state() === 'missing' || this.state() === 'failed');

  /** Answers that arrive out of order — a currency changed twice — are dropped. */
  private sequence = 0;

  constructor() {
    effect(() => {
      const currency = this.currency()?.trim().toUpperCase() || null;
      const date = this.date();
      const base = this.baseCurrency();
      const locked = this.locked();
      untracked(() => {
        this.sequence++;
        if (locked) {
          this.state.set('locked');
          return;
        }
        if (!currency || currency === base) {
          this.state.set('base');
          this.rate.set(1);
          return;
        }
        void this.load(currency, base, date);
      });
    });
  }

  private async load(currency: string, base: string, date: string | null): Promise<void> {
    const mine = this.sequence;
    this.state.set('loading');
    try {
      const found = await this.choices.exchangeRate(currency, base, date);
      if (mine !== this.sequence) return;
      this.rateDate.set(found?.rateDate ?? null);
      this.rate.set(found?.rate ?? null);
      this.state.set(found ? 'found' : 'missing');
    } catch {
      if (mine !== this.sequence) return;
      this.rate.set(null);
      this.state.set('failed');
    }
  }
}
