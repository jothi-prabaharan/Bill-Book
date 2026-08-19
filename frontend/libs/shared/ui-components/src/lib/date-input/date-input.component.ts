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
  selector: 'bb-date-input',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => DateInputComponent),
      multi: true,
    },
  ],
  templateUrl: './date-input.component.html',
  styleUrl: './date-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DateInputComponent implements ControlValueAccessor {
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly placeholder = input<string>('');
  readonly min = input<string | null>(null);
  readonly max = input<string | null>(null);
  readonly disabled = input<boolean>(false);
  readonly readonly = input<boolean>(false);
  readonly required = input<boolean, boolean | string>(false, { transform: (v: any) => v === "" || v === "true" || v === true });
  readonly ariaLabel = input<string>('Date');

  readonly valueChange = output<string | null>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly focus = output<FocusEvent>();

  protected readonly innerValue = signal<string>('');
  private readonly cvaDisabled = signal<boolean>(false);
  protected readonly effectiveDisabled = computed(() => this.disabled() || this.cvaDisabled());

  private onChange: (val: string | null) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string | Date | null | undefined): void {
    if (value === null || value === undefined || value === '') {
      this.innerValue.set('');
      return;
    }

    if (value instanceof Date) {
      if (isNaN(value.getTime())) {
        this.innerValue.set('');
        return;
      }
      const yyyy = value.getFullYear();
      const mm = String(value.getMonth() + 1).padStart(2, '0');
      const dd = String(value.getDate()).padStart(2, '0');
      this.innerValue.set(`${yyyy}-${mm}-${dd}`);
      return;
    }

    if (typeof value === 'string') {
      const match = value.match(/^(\d{4}-\d{2}-\d{2})/);
      if (match) {
        this.innerValue.set(match[1]);
      } else {
        this.innerValue.set(value);
      }
      return;
    }

    this.innerValue.set(String(value));
  }

  registerOnChange(fn: (val: string | null) => void): void {
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
    const raw = target.value;
    const valueToEmit = raw.trim() === '' ? null : raw;
    this.innerValue.set(raw);
    this.onChange(valueToEmit);
    this.valueChange.emit(valueToEmit);
  }

  protected onBlur(event: FocusEvent): void {
    this.onTouched();
    this.blur.emit(event);
  }

  protected onFocus(event: FocusEvent): void {
    this.focus.emit(event);
  }
}
