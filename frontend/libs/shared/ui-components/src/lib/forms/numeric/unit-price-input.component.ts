import { ChangeDetectionStrategy, Component, forwardRef, input } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbNumericControlBase } from './numeric-control.base';

/**
 * A price per unit — a sales rate, a purchase rate, a price-list entry.
 *
 * **Not `bb-money-input`, and the difference is real.** `UnitPrice` is
 * `decimal(28,6)` in the schema where every other money column is
 * `decimal(28,2)`, because a rate per gram or per thousand pieces genuinely
 * needs the places an invoice total does not. Using the money control here
 * would round a rate of ₹0.004375 to ₹0.00 and produce a line total of nothing.
 *
 * Two places are shown by default because that is what a rate usually is, and
 * the display floor never truncates: a rate that carries six places shows six.
 * The branch's own `unitPriceDecimals` from `GET /api/formats` is what a page
 * should pass as `decimals` when it has them.
 */
@Component({
  selector: 'bb-unit-price-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => UnitPriceInputComponent),
      multi: true,
    },
  ],
  templateUrl: './numeric-control.html',
  styleUrl: './numeric-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnitPriceInputComponent extends BbNumericControlBase {
  readonly currencySymbol = input<string>('');

  /** The unit the price is per — `/kg`, `/pc`. Display only. */
  readonly perUnit = input<string>('');

  protected override defaultDecimals(): number {
    return 2;
  }

  /** A negative rate is not a discount; it is a mistake the server refuses. */
  protected override defaultMin(): number | null {
    return 0;
  }

  protected override defaultPrefix(): string {
    return this.currencySymbol();
  }

  protected override defaultSuffix(): string {
    return this.perUnit() ? `/${this.perUnit()}` : '';
  }

  protected override fallbackAriaLabel(): string {
    return 'Unit price';
  }

  protected idPrefix(): string {
    return 'bb-unit-price';
  }
}
