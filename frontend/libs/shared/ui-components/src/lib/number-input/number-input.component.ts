import { ChangeDetectionStrategy, Component, forwardRef } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../forms/form-field/form-field.component';
import { BbNumericControlBase } from '../forms/numeric/numeric-control.base';

/**
 * A number with no business meaning of its own.
 *
 * Reorder levels, lead times, days, counts, sequence numbers — figures that are
 * numeric and nothing more. **Anything that is money, a rate per unit, a
 * quantity of stock, a percentage or an exchange rate has its own component**,
 * because each of those has a precision, a range and a scale this one cannot
 * know. Reaching for this where one of the five belongs is how a quantity ends
 * up rounded to two places or a percentage accepts 400.
 *
 * It is the same implementation as those five — `BbNumericControlBase` — with
 * no semantic defaults layered on. Its public API is unchanged from before that
 * refactor; what it gained is `label`, `hint`, `error` and the accessibility
 * wiring every common input now shares.
 */
@Component({
  selector: 'bb-number-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => NumberInputComponent),
      multi: true,
    },
  ],
  templateUrl: '../forms/numeric/numeric-control.html',
  styleUrl: '../forms/numeric/numeric-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NumberInputComponent extends BbNumericControlBase {
  /** Unformatted unless the caller asks for places, as it has always been. */
  protected override defaultDecimals(): number {
    return 0;
  }

  /** Left, matching what this component has always drawn. */
  protected override defaultAlign(): 'left' | 'right' | 'center' {
    return 'left';
  }

  /** `decimal` regardless of precision, as before — a plain number may be one. */
  protected override defaultInputmode(): 'decimal' | 'numeric' {
    return 'decimal';
  }

  protected override fallbackAriaLabel(): string {
    return 'Number';
  }

  protected idPrefix(): string {
    return 'bb-number';
  }
}
