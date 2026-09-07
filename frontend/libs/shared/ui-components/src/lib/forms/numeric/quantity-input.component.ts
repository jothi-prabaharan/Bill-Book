import { ChangeDetectionStrategy, Component, forwardRef, input } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbNumericControlBase } from './numeric-control.base';

/**
 * A count of something — invoiced, ordered, received, in stock.
 *
 * **Fractional by default, because `Quantity` is `decimal(18,6)`.** Half a
 * kilogram, 2.5 metres and 0.125 grams of gold are all ordinary lines in this
 * product, so forcing whole numbers would be wrong far more often than right.
 * `integerOnly` is there for the fields where a fraction is genuinely
 * meaningless — a pack count, a serial-tracked item — and it is the caller's
 * business rule to assert, not this component's guess.
 *
 * A quantity is never negative here. A return is its own document with its own
 * sign convention, and a negative typed into an invoice line is a mistake the
 * check constraint refuses on save.
 */
@Component({
  selector: 'bb-quantity-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => QuantityInputComponent),
      multi: true,
    },
  ],
  templateUrl: './numeric-control.html',
  styleUrl: './numeric-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class QuantityInputComponent extends BbNumericControlBase {
  /**
   * Whole numbers only. Sets the precision to zero, which also makes the step
   * one and asks for a numeric keypad rather than a decimal one.
   */
  readonly integerOnly = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /** The unit of measure, shown at the field's right edge. Display only. */
  readonly uom = input<string>('');

  /** Off where a zero-quantity line is legitimate — a nil goods receipt row. */
  readonly allowZero = input<boolean, boolean | string>(true, {
    transform: (value) => value !== 'false' && value !== false,
  });

  protected override defaultDecimals(): number {
    return this.integerOnly() ? 0 : 2;
  }

  protected override defaultMin(): number | null {
    if (this.allowZero()) {
      return 0;
    }
    // One smallest unit, in whatever scale the caller stores: a plain 1 at
    // decimal scale, one millionth at the line grid's scale of six.
    return this.minorDigits() > 0 ? 1 : 0;
  }

  protected override defaultSuffix(): string {
    return this.uom();
  }

  protected override fallbackAriaLabel(): string {
    return 'Quantity';
  }

  protected idPrefix(): string {
    return 'bb-quantity';
  }
}
