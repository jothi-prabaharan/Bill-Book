import { ChangeDetectionStrategy, Component, computed, forwardRef, input, output, signal } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { BbControlBase } from '../control-base';
import { FormFieldComponent } from '../form-field/form-field.component';

/**
 * Several lines of plain text — notes, terms, an address, a void reason.
 *
 * Plain text, deliberately. Anything that needs emphasis or a list is
 * `bb-rich-text-input`; anything that only needs to be read back is a
 * `bb-textarea`, and the vast majority of long fields in this product are.
 *
 * The character counter appears only when there is a `maxlength` and only once
 * the field is most of the way to it — a counter that reads "0 / 300" from the
 * start is noise, and the one moment it matters is when somebody is about to
 * lose the end of what they wrote.
 */
@Component({
  selector: 'bb-textarea',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TextareaComponent),
      multi: true,
    },
  ],
  templateUrl: './textarea.component.html',
  styleUrl: './textarea.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TextareaComponent extends BbControlBase<string> {
  readonly placeholder = input<string>('');

  readonly rows = input<number, number | string>(3, {
    transform: (value) => Number(value) || 3,
  });

  readonly minlength = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  readonly maxlength = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  /** Vertical is the default: horizontal resizing breaks a form's grid. */
  readonly resize = input<'vertical' | 'none' | 'both'>('vertical');

  /** Off where the count would distract more than it helps. */
  readonly showCounter = input<boolean, boolean | string>(true, {
    transform: (value) => value !== 'false' && value !== false,
  });

  readonly valueChange = output<string>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();

  protected readonly innerValue = signal<string>('');

  protected readonly used = computed(() => this.innerValue().length);

  /** Shown from four fifths of the limit, which is where it starts to matter. */
  protected readonly counterVisible = computed(() => {
    const limit = this.maxlength();
    return this.showCounter() && limit !== null && this.used() >= limit * 0.8;
  });

  protected readonly counterText = computed(() => `${this.used()} / ${this.maxlength()}`);

  writeValue(value: unknown): void {
    this.innerValue.set(value === null || value === undefined ? '' : String(value));
  }

  protected emitValue(value: string): void {
    this.valueChange.emit(value);
  }

  protected onInput(event: Event): void {
    const value = (event.target as HTMLTextAreaElement).value;
    this.innerValue.set(value);
    this.publish(value);
  }

  protected onBlur(event: FocusEvent): void {
    this.onTouched();
    this.blur.emit(event);
  }

  protected idPrefix(): string {
    return 'bb-textarea';
  }
}
