import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { FormatSettingsService } from '@bill-book/currency-format';
import { reportAmount } from './report-format';

/**
 * An amount on a report (TK-262): the branch's money format with its decimals always shown, and
 * the currency code after it when `currency` is given. An amount in the branch's own currency
 * carries the branch's symbol; one in another currency is the bare number with its code.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-report-amount',
  standalone: true,
  template: `<span class="money">{{ text() }}</span>@if (currency() && foreign()) {<span class="money__code">{{ currency() }}</span>}`,
  styles: [
    `
      :host { display: inline-flex; align-items: baseline; justify-content: flex-end; gap: 4px; font-variant-numeric: tabular-nums; white-space: nowrap; }
      .money__code { font-size: 10px; letter-spacing: 0.04em; color: var(--color-text-subtle); }
    `,
  ],
})
export class ReportAmountComponent {
  private readonly formats = inject(FormatSettingsService);

  readonly value = input<number | null | undefined>(null);

  /** The amount's currency, shown after the figure when it is not the branch's. Empty is the branch's own. */
  readonly currency = input<string | null | undefined>('');

  protected readonly foreign = computed(() => {
    const code = this.currency();
    return !!code && code !== this.formats.settings().currencyCode;
  });

  protected readonly text = computed(() => reportAmount(this.value(), this.formats.settings(), this.currency()));
}
