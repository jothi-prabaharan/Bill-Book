import { ChangeDetectionStrategy, Component, forwardRef } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../forms/form-field/form-field.component';
import { BbTextControlBase } from '../forms/text/text-control.base';

/**
 * A single line of text — the default of the common input set.
 *
 * Names, codes, descriptions, references. Anything with its own rules gets its
 * own component instead: an address gets `bb-email-input`, a number
 * `bb-phone-input`, a figure one of the five numeric controls. They are all
 * this class with a different four lines at the bottom.
 *
 * **Its implementation moved into `BbTextControlBase` and its markup into the
 * shared text template; its public API did not change.** The one visible
 * difference is that it now draws with the house `.input` styling rather than
 * its own near-copy of it, so a `bb-text-input` and a hand-written `<input
 * class="input">` on the same form are finally the same height.
 */
@Component({
  selector: 'bb-text-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TextInputComponent),
      multi: true,
    },
  ],
  templateUrl: '../forms/text/text-control.html',
  styleUrl: '../forms/text/text-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TextInputComponent extends BbTextControlBase {
  protected idPrefix(): string {
    return 'bb-text';
  }
}
