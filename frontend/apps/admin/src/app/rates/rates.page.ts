import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '@bill-book/auth';
import {
  BbSelectOption,
  DateInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { ExchangeRateRow, MetalRateRow, RatesService } from './rates.service';

function today(): string {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

/**
 * Rates — hand entry of exchange and metal rates (TK-24).
 *
 * The rows are global: every customer's documents read them, which is why only
 * a platform operator enters them. A rate entered for a date answers for every
 * later date until a newer one is entered, and a hand-entered rate outranks a
 * fetched one on the same date, so a wrong scraped figure is corrected by
 * entering the right one rather than by deleting anything.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-rates-page',
  standalone: true,
  imports: [FormsModule, SelectComponent, TextInputComponent, DateInputComponent, NumberInputComponent],
  templateUrl: './rates.page.html',
  styleUrl: './rates.page.scss',
})
export class RatesPage implements OnInit {
  private readonly rates = inject(RatesService);
  private readonly auth = inject(AuthService);

  protected readonly metals: BbSelectOption<string>[] = [
    { value: 'Gold', label: 'Gold' },
    { value: 'Silver', label: 'Silver' },
    { value: 'Platinum', label: 'Platinum' },
  ];

  protected readonly exchangeRows = signal<ExchangeRateRow[]>([]);
  protected readonly metalRows = signal<MetalRateRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);

  protected readonly canEdit = signal(false);

  exchange = { fromCurrencyCode: 'USD', toCurrencyCode: 'INR', rateDate: today(), rate: null as number | null };
  metal = { metal: 'Gold', purityCode: '24K', rateDate: today(), ratePerGram: null as number | null };

  ngOnInit(): void {
    this.canEdit.set(this.auth.has('platform.edit'));
    void this.load();
  }

  async load(): Promise<void> {
    this.busy.set(true);
    try {
      const [exchange, metal] = await Promise.all([this.rates.exchangeHistory(), this.rates.metalHistory()]);
      this.exchangeRows.set(exchange);
      this.metalRows.set(metal);
    } catch {
      this.fail('Could not load the rates.');
    } finally {
      this.busy.set(false);
    }
  }

  async saveExchange(): Promise<void> {
    const { fromCurrencyCode, toCurrencyCode, rateDate, rate } = this.exchange;
    if (!fromCurrencyCode.trim() || !toCurrencyCode.trim() || !rateDate || !rate || rate <= 0) {
      this.fail('Enter both currencies, a date and a rate above zero.');
      return;
    }

    await this.run(
      () =>
        this.rates.saveExchange({
          fromCurrencyCode: fromCurrencyCode.trim().toUpperCase(),
          toCurrencyCode: toCurrencyCode.trim().toUpperCase(),
          rateDate,
          rate,
        }),
      'Exchange rate saved.',
    );
  }

  async saveMetal(): Promise<void> {
    const { metal, purityCode, rateDate, ratePerGram } = this.metal;
    if (!metal || !purityCode.trim() || !rateDate || !ratePerGram || ratePerGram <= 0) {
      this.fail('Enter a metal, a purity, a date and a rate per gram above zero.');
      return;
    }

    await this.run(
      () => this.rates.saveMetal({ metal, purityCode: purityCode.trim().toUpperCase(), rateDate, ratePerGram }),
      'Metal rate saved.',
    );
  }

  async removeExchange(row: ExchangeRateRow): Promise<void> {
    await this.run(() => this.rates.deleteExchange(row.exchangeRateId), 'Exchange rate removed.');
  }

  async removeMetal(row: MetalRateRow): Promise<void> {
    await this.run(() => this.rates.deleteMetal(row.metalRateId), 'Metal rate removed.');
  }

  private async run(action: () => Promise<unknown>, ok: string): Promise<void> {
    this.busy.set(true);
    this.message.set(null);
    try {
      await action();
      this.message.set(ok);
      this.messageIsError.set(false);
    } catch (err: unknown) {
      const failure = err as { error?: { message?: string } };
      this.fail(failure?.error?.message ?? 'That did not work.');
    } finally {
      this.busy.set(false);
    }
    await this.load();
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }
}
