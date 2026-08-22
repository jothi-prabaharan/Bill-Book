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
  selector: 'bb-currency-input',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CurrencyInputComponent),
      multi: true,
    },
  ],
  templateUrl: './currency-input.component.html',
  styleUrl: './currency-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CurrencyInputComponent implements ControlValueAccessor {
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly symbol = input<string>('₹');
  readonly currencyCode = input<string>('INR');
  readonly showSymbol = input<boolean>(false);
  readonly decimals = input<number>(2);
  readonly min = input<number | null>(null);
  readonly max = input<number | null>(null);
  readonly step = input<number | string>(0.01);
  readonly placeholder = input<string>('0.00');
  readonly disabled = input<boolean>(false);
  readonly readonly = input<boolean>(false);
  readonly required = input<boolean, boolean | string>(false, { transform: (v: any) => v === "" || v === "true" || v === true });
  readonly allowNegative = input<boolean>(false);
  readonly inPaise = input<boolean>(false);
  readonly align = input<'left' | 'right'>('right');
  readonly ariaLabel = input<string>('Amount');

  readonly valueChange = output<number | null>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly focus = output<FocusEvent>();

  protected readonly displayValue = signal<string>('');
  protected readonly isFocused = signal<boolean>(false);
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
    const decimalAmount = this.inPaise() ? num / 100 : num;
    this.displayValue.set(decimalAmount.toFixed(this.decimals()));
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
    let text = target.value;

    // Filter characters if needed
    if (!this.allowNegative()) {
      text = text.replace(/-/g, '');
    }

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

    let finalValue: number;
    if (this.inPaise()) {
      finalValue = Math.round(parsed * 100);
    } else {
      finalValue = parsed;
    }

    this.rawNumericValue = finalValue;
    this.onChange(finalValue);
    this.valueChange.emit(finalValue);
  }

  protected onFocus(event: FocusEvent): void {
    this.isFocused.set(true);
    this.focus.emit(event);
  }

  protected onBlur(event: FocusEvent): void {
    this.isFocused.set(false);
    if (this.rawNumericValue !== null) {
      const decimalAmount = this.inPaise()
        ? this.rawNumericValue / 100
        : this.rawNumericValue;
      this.displayValue.set(decimalAmount.toFixed(this.decimals()));
    } else {
      this.displayValue.set('');
    }
    this.onTouched();
    this.blur.emit(event);
  }
}
