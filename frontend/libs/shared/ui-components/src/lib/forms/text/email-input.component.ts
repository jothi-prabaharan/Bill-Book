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

  /**
   * The email mask: what an address cannot contain, removed as it is typed.
   *
   * - **Whitespace goes.** A space is never part of an address, and the one
   *   that gets typed is almost always a stray from pasting or a phone
   *   keyboard's auto-space after the domain.
   * - **Lower case.** Addresses are compared case-insensitively everywhere that
   *   matters, and storing the case somebody happened to use makes two rows
   *   look different when they are the same person.
   * - **One `@`.** The first one is the separator; any after it are a paste
   *   that went wrong, and they are dropped rather than left to fail
   *   validation later.
   *
   * **This is convenience, not validation.** The pattern above still checks the
   * shape, `bbEmail` checks it again in the form, and the API checks it a third
   * time — that last one is the only one that counts.
   */
  protected override mask(text: string): string {
    const cleaned = text.toLowerCase().replace(/\s+/g, '');
    const at = cleaned.indexOf('@');

    if (at < 0) {
      return cleaned;
    }

    return cleaned.slice(0, at + 1) + cleaned.slice(at + 1).replace(/@/g, '');
  }

  protected override fallbackAriaLabel(): string {
    return 'Email address';
  }

  protected idPrefix(): string {
    return 'bb-email';
  }
}
