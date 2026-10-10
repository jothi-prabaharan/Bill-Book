import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { FormatSettingsService } from '@bill-book/currency-format';
import { registerAmount } from './register-format';

/**
 * An amount in a register row (TK-187): the branch's money format, paise only
 * when there are some, and the currency code after it when `currency` is given.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-money-cell',
  standalone: true,
  template: `<span class="money">{{ text() }}</span>@if (currency()) {<span class="money__code">{{ currency() }}</span>}`,
  styles: [
    `
      :host { display: inline-flex; align-items: baseline; justify-content: flex-end; gap: 4px; font-variant-numeric: tabular-nums; white-space: nowrap; }
      .money__code { font-size: 10px; letter-spacing: 0.04em; color: var(--color-text-subtle); }
    `,
  ],
})
export class MoneyCellComponent {
  private readonly formats = inject(FormatSettingsService);

  readonly value = input<number | null | undefined>(null);

  /** The document's currency, shown after the figure. Empty shows none. */
  readonly currency = input<string | null | undefined>('');

  readonly text = computed(() => registerAmount(this.value(), this.formats.settings(), this.currency()));
}
