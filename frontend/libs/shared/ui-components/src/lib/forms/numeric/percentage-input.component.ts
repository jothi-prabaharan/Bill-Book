import { ChangeDetectionStrategy, Component, forwardRef, input } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbNumericControlBase } from './numeric-control.base';

/**
 * A percentage — a line discount, a tax rate, a margin.
 *
 * **Ten per cent is `10`, never `0.1`.** That is what the schema holds
 * (`DiscountPercent` is `decimal(9,6)` and a tax `Rate` is `decimal(9,4)`, both
 * as percents), what `line-math.ts` divides by a hundred, and what
 * `GstCalculator` does on the C# side. This control neither multiplies nor
 * divides: what is typed is what is stored, and the `%` at the field's edge is
 * a label rather than a conversion. Changing that convention would restate
 * every rate in the product, so nothing here is allowed to.
 *
 * Capped at 100 by default. A discount over the whole line is not a discount,
 * and `line-math.ts` clamps it to the gross anyway — refusing it at the field
 * is how somebody finds out before saving. `max` overrides it for the rare
 * field where more is meaningful, such as a markup.
 */
@Component({
  selector: 'bb-percentage-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => PercentageInputComponent),
      multi: true,
    },
  ],
  templateUrl: './numeric-control.html',
  styleUrl: './numeric-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PercentageInputComponent extends BbNumericControlBase {
  /** Off where a negative percentage means something — a downward revision. */
  readonly allowNegative = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /** Hides the `%` where the column heading already says it. */
  readonly showSuffix = input<boolean, boolean | string>(true, {
    transform: (value) => value !== 'false' && value !== false,
  });

  protected override defaultDecimals(): number {
    return 2;
  }

  protected override defaultMin(): number | null {
    return this.allowNegative() ? null : 0;
  }

  /** A hundred per cent, in whatever scale the caller stores. */
  protected override defaultMax(): number | null {
    return 100 * 10 ** this.minorDigits();
  }

  protected override defaultSuffix(): string {
    return this.showSuffix() ? '%' : '';
  }

  protected override fallbackAriaLabel(): string {
    return 'Percentage';
  }

  protected idPrefix(): string {
    return 'bb-percentage';
  }
}
