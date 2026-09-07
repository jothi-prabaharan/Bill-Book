import { ChangeDetectionStrategy, Component, computed, forwardRef, input } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbNumericControlBase } from './numeric-control.base';

/**
 * The rate a document's currency converts to the branch's base currency at.
 *
 * **Eight decimal places, because `ExchangeRate` is `decimal(18,8)`** on every
 * table that carries one. Nothing here rounds to fewer: a rate is multiplied
 * across every amount on the document, so a place dropped at the field is a
 * discrepancy on the ledger.
 *
 * **Separate from money on purpose.** It is a ratio, not an amount — it has no
 * currency, no symbol and no two-place convention, and a control that treated
 * it as money would round 0.01123456 to 0.01.
 *
 * Strictly positive. A rate of zero would make every converted figure zero and
 * the check constraint refuses it; the minimum is one unit of the last decimal
 * place, matching the `min` the invoice form has always written by hand.
 *
 * A rate is a **snapshot at the document's date and is never looked up live** —
 * that is a product rule rather than a component one, but it is why this field
 * is editable at all and why the default hint says so.
 */
@Component({
  selector: 'bb-exchange-rate-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => ExchangeRateInputComponent),
      multi: true,
    },
  ],
  templateUrl: './numeric-control.html',
  styleUrl: './numeric-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExchangeRateInputComponent extends BbNumericControlBase {
  /** The document's currency, shown as `USD →` so the direction is not guessed. */
  readonly fromCurrency = input<string>('');

  /** The branch's base currency. */
  readonly toCurrency = input<string>('');

  protected readonly pair = computed(() => {
    const from = this.fromCurrency();
    const to = this.toCurrency();
    return from && to ? `${from} → ${to}` : '';
  });

  protected override defaultDecimals(): number {
    return 8;
  }

  /**
   * The smallest rate the column can hold. Zero is refused rather than clamped,
   * so the page can say why.
   */
  protected override defaultMin(): number | null {
    return this.minorDigits() > 0 ? 1 : 1e-8;
  }

  protected override defaultPrefix(): string {
    return this.pair();
  }

  protected override fallbackAriaLabel(): string {
    return 'Exchange rate';
  }

  protected idPrefix(): string {
    return 'bb-exchange-rate';
  }
}
