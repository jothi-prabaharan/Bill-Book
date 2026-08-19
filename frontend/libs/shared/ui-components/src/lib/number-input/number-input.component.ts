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
  selector: 'bb-number-input',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => NumberInputComponent),
      multi: true,
    },
  ],
  templateUrl: './number-input.component.html',
  styleUrl: './number-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NumberInputComponent implements ControlValueAccessor {
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly min = input<number | null, number | string | null>(null, { transform: (v) => v != null ? Number(v) : null });
  readonly max = input<number | null, number | string | null>(null, { transform: (v) => v != null ? Number(v) : null });
  readonly step = input<number | string>(1);
  readonly decimals = input<number | null>(null);
  readonly placeholder = input<string>('');
  readonly prefix = input<string | null>(null);
  readonly suffix = input<string | null>(null);
  readonly disabled = input<boolean>(false);
  readonly readonly = input<boolean>(false);
  readonly required = input<boolean, boolean | string>(false, { transform: (v: any) => v === "" || v === "true" || v === true });
  readonly align = input<'left' | 'right' | 'center'>('left');
  readonly inputmode = input<'decimal' | 'numeric'>('decimal');
  readonly ariaLabel = input<string>('Number');

  readonly valueChange = output<number | null>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly focus = output<FocusEvent>();

  protected readonly displayValue = signal<string>('');
  private readonly cvaDisabled = signal<boolean>(false);
  protected readonly effectiveDisabled = computed(() => this.disabled() || this.cvaDisabled());

  private rawNumericValue: number | null = null;
  private onChange: (val: number | null) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: number | string | null | undefined): void {
    if (value === null || value === undefined || value === '') {
      this.rawNumericValue = null;
      this.displayValue.set('');
      return;
    }

    const num = Number(value);
    if (isNaN(num)) {
      this.rawNumericValue = null;
      this.displayValue.set('');
      return;
    }

    this.rawNumericValue = num;
    const dec = this.decimals();
    if (dec !== null && dec !== undefined) {
      this.displayValue.set(num.toFixed(dec));
    } else {
      this.displayValue.set(String(num));
    }
  }

  registerOnChange(fn: (val: number | null) => void): void {
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
    const text = target.value;
    this.displayValue.set(text);

    const trimmed = text.trim();
    if (trimmed === '' || trimmed === '-') {
      this.rawNumericValue = null;
      this.onChange(null);
      this.valueChange.emit(null);
      return;
    }

    const parsed = parseFloat(trimmed);
    if (isNaN(parsed)) {
      this.rawNumericValue = null;
      this.onChange(null);
      this.valueChange.emit(null);
      return;
    }

    this.rawNumericValue = parsed;
    this.onChange(parsed);
    this.valueChange.emit(parsed);
  }

  protected onFocus(event: FocusEvent): void {
    this.focus.emit(event);
  }

  protected onBlur(event: FocusEvent): void {
    if (this.rawNumericValue !== null) {
      const dec = this.decimals();
      if (dec !== null && dec !== undefined) {
        this.displayValue.set(this.rawNumericValue.toFixed(dec));
      }
    } else {
      this.displayValue.set('');
    }
    this.onTouched();
    this.blur.emit(event);
  }
}
