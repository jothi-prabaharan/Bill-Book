import { ChangeDetectionStrategy, Component, computed, forwardRef, input, signal } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbTextControlBase, BbTextInputType } from './text-control.base';

/** Which password this is, so the browser offers the right saved one. */
export type BbPasswordPurpose = 'current' | 'new' | 'off';

/**
 * A password, with a show/hide toggle.
 *
 * **Nothing here logs, emits or stores the value anywhere but the form control
 * it is bound to.** No `console`, no analytics, no `title` attribute carrying
 * the text. The toggle flips the element's `type` and nothing else — the value
 * never leaves the input.
 *
 * `purpose` picks the `autocomplete` token, which is what tells a password
 * manager whether to offer the saved password or to offer to save a new one.
 * Getting it wrong is why a change-password form sometimes fills itself with
 * the old password.
 */
@Component({
  selector: 'bb-password-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => PasswordInputComponent),
      multi: true,
    },
  ],
  templateUrl: './password-input.component.html',
  styleUrl: './password-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PasswordInputComponent extends BbTextControlBase {
  readonly purpose = input<BbPasswordPurpose>('current');

  /** Off where showing the password would be worse than mistyping it. */
  readonly allowReveal = input<boolean>(true);

  protected readonly revealed = signal(false);

  protected readonly fieldType = computed<BbTextInputType>(() =>
    this.revealed() ? 'text' : 'password',
  );

  protected readonly toggleLabel = computed(() =>
    this.revealed() ? 'Hide password' : 'Show password',
  );

  protected toggleReveal(): void {
    if (this.effectiveDisabled()) {
      return;
    }
    this.revealed.update((shown) => !shown);
  }

  protected override defaultType(): BbTextInputType {
    return 'password';
  }

  protected override defaultAutocomplete(): string {
    switch (this.purpose()) {
      case 'new':
        return 'new-password';
      case 'off':
        return 'off';
      default:
        return 'current-password';
    }
  }

  protected override fallbackAriaLabel(): string {
    return 'Password';
  }

  protected idPrefix(): string {
    return 'bb-password';
  }
}
