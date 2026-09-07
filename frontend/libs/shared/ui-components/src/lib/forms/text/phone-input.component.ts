import { ChangeDetectionStrategy, Component, forwardRef, input } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbTextControlBase, BbTextInputType } from './text-control.base';

/** Which of the two phone rules a field is under. */
export type BbPhoneKind = 'mobile' | 'landline';

/**
 * A landline: an STD code of two digits or more, then a number of three or more.
 *
 * The same expression as `Shared.Kernel.Validation.LandlineAttribute`, character
 * for character. Two implementations of one rule is how a screen accepts what
 * the API then refuses.
 */
export const BB_LANDLINE_PATTERN = '\\+?\\d{2,}[\\s\\-]?\\d{3,}';

/** `MobileAttribute.MaxLength`. Length is the only rule a mobile number has. */
export const BB_MOBILE_MAX_LENGTH = 20;

/**
 * A telephone number, under whichever of the two rules applies.
 *
 * **Not Indian-only, and deliberately so.** The product's rule is that a local
 * number is stored without a prefix and a foreign one **with a leading `+`** —
 * the `+` is the discriminator, and SMS prepends the branch's country phone code
 * when it is absent. So a mobile number carries **no pattern at all**: lengths
 * vary too much by country for one to be anything but a source of false
 * rejections, exactly as `MobileAttribute` says. A landline does carry one,
 * because "STD code then number" holds wherever the number is from.
 *
 * `kind` is the strategy: `mobile` by default, `landline` where the field is one.
 */
@Component({
  selector: 'bb-phone-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => PhoneInputComponent),
      multi: true,
    },
  ],
  templateUrl: './text-control.html',
  styleUrl: './text-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PhoneInputComponent extends BbTextControlBase {
  readonly kind = input<BbPhoneKind>('mobile');

  /**
   * The branch's dialling code, shown as a prefix so the person keying can see
   * what a number without a `+` will be sent as. **Display only** — it is never
   * added to the stored value, because storing it would make every local number
   * look foreign.
   */
  readonly countryCode = input<string>('');

  protected override defaultPrefix(): string {
    return this.countryCode();
  }

  protected override defaultType(): BbTextInputType {
    return 'tel';
  }

  protected override defaultInputmode(): string {
    return 'tel';
  }

  protected override defaultAutocomplete(): string {
    return 'tel';
  }

  protected override defaultPattern(): string {
    return this.kind() === 'landline' ? BB_LANDLINE_PATTERN : '';
  }

  protected override fallbackAriaLabel(): string {
    return this.kind() === 'landline' ? 'Landline number' : 'Mobile number';
  }

  protected idPrefix(): string {
    return 'bb-phone';
  }
}
