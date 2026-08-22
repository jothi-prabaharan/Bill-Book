import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  input,
  output,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'bb-text-input',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TextInputComponent),
      multi: true,
    },
  ],
  templateUrl: './text-input.component.html',
  styleUrl: './text-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TextInputComponent implements ControlValueAccessor {
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly type = input<'text' | 'email' | 'password' | 'tel' | 'url'>('text');
  readonly placeholder = input<string>('');
  readonly maxlength = input<number | null, number | string | null>(null, { transform: (v) => v != null ? Number(v) : null });
  readonly uppercase = input<boolean>(false);
  readonly disabled = input<boolean>(false);
  readonly readonly = input<boolean>(false);
  readonly required = input<boolean, boolean | string>(false, { transform: (v: any) => v === "" || v === "true" || v === true });
  readonly autocomplete = input<string>('off');
  readonly ariaLabel = input<string>('');

  readonly valueChange = output<string>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly focus = output<FocusEvent>();
  readonly enter = output<string>();

  protected readonly innerValue = signal<string>('');
  private readonly cvaDisabled = signal<boolean>(false);
  protected readonly effectiveDisabled = computed(() => this.disabled() || this.cvaDisabled());

  private onChange: (val: string) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string | null | undefined): void {
    if (value === null || value === undefined) {
      this.innerValue.set('');
      return;
    }
    const str = String(value);
    this.innerValue.set(this.uppercase() ? str.toUpperCase() : str);
  }

  registerOnChange(fn: (val: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.cvaDisabled.set(isDisabled);
  }

  protected onInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    let val = target.value;
    if (this.uppercase()) {
      val = val.toUpperCase();
      target.value = val;
    }
    this.innerValue.set(val);
    this.onChange(val);
    this.valueChange.emit(val);
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      this.enter.emit(this.innerValue());
    }
  }

  protected onBlur(event: FocusEvent): void {
    this.onTouched();
    this.blur.emit(event);
  }

  protected onFocus(event: FocusEvent): void {
    this.focus.emit(event);
  }
}
