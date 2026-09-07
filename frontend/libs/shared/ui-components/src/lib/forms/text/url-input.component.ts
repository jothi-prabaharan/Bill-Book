import { ChangeDetectionStrategy, Component, forwardRef } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbTextControlBase, BbTextInputType } from './text-control.base';

/**
 * A web address, with a scheme.
 *
 * **The scheme is required by the pattern on purpose.** A stored `acme.com`
 * rendered into an `href` is read as a *relative* path, so the link lands on
 * the application's own domain and quietly goes nowhere. Requiring `http://` or
 * `https://` at the point of entry is the only place that is cheap to fix.
 *
 * Only those two schemes are accepted. `javascript:` in a field whose value
 * later reaches an `href` is a cross-site scripting hole, and no legitimate
 * website address in this product needs any other scheme.
 */
export const BB_URL_PATTERN = 'https?://[^\\s]+\\.[^\\s]{2,}';

@Component({
  selector: 'bb-url-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => UrlInputComponent),
      multi: true,
    },
  ],
  templateUrl: './text-control.html',
  styleUrl: './text-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UrlInputComponent extends BbTextControlBase {
  protected override defaultType(): BbTextInputType {
    return 'url';
  }

  protected override defaultInputmode(): string {
    return 'url';
  }

  protected override defaultAutocomplete(): string {
    return 'url';
  }

  protected override defaultPattern(): string {
    return BB_URL_PATTERN;
  }

  protected override fallbackAriaLabel(): string {
    return 'Web address';
  }

  protected idPrefix(): string {
    return 'bb-url';
  }
}
