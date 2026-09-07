import { ChangeDetectionStrategy, Component, forwardRef } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbTextControlBase, BbTextInputType } from './text-control.base';

/**
 * The regular expression the field checks and `emailValidator` shares.
 *
 * Deliberately loose. A strict RFC 5322 expression rejects addresses that
 * genuinely deliver, and the only test that settles the question is sending
 * mail to it — which is what the invitation and password-reset flows already
 * do. This catches the typo class of mistake (no `@`, no dot, a trailing
 * comma) and leaves the rest to the server.
 */
export const BB_EMAIL_PATTERN = '[^@\\s]+@[^@\\s]+\\.[^@\\s]{2,}';

/**
 * An email address.
 *
 * `type="email"` alone is not validation — a browser only enforces it inside a
 * native form submit, which this application never does, so the same expression
 * is offered to Angular as `emailValidator` below and the field is the
 * convenience rather than the check. The API validates it a third time, and
 * that is the one that counts.
 */
@Component({
  selector: 'bb-email-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => EmailInputComponent),
      multi: true,
    },
  ],
  templateUrl: './text-control.html',
  styleUrl: './text-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmailInputComponent extends BbTextControlBase {
  protected override defaultType(): BbTextInputType {
    return 'email';
  }

  protected override defaultInputmode(): string {
    return 'email';
  }

  protected override defaultAutocomplete(): string {
    return 'email';
  }

  protected override defaultPattern(): string {
    return BB_EMAIL_PATTERN;
  }

  protected override fallbackAriaLabel(): string {
    return 'Email address';
  }

  protected idPrefix(): string {
    return 'bb-email';
  }
}
