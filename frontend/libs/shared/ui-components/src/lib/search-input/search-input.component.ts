import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  input,
  OnDestroy,
  output,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

@Component({
  selector: 'bb-search-input',
  standalone: true,
  imports: [CommonModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SearchInputComponent),
      multi: true,
    },
  ],
  templateUrl: './search-input.component.html',
  styleUrl: './search-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SearchInputComponent implements ControlValueAccessor, OnDestroy {
  readonly id = input<string>('');
  readonly name = input<string>('');
  readonly placeholder = input<string>('Search...');
  readonly ariaLabel = input<string>('Search');
  readonly disabled = input<boolean>(false);
  readonly debounceMs = input<number>(300);

  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly search = output<string>();
  readonly clear = output<void>();
  readonly valueChange = output<string>();

  protected readonly innerValue = signal<string>('');
  private readonly cvaDisabled = signal<boolean>(false);
  protected readonly effectiveDisabled = computed(() => this.disabled() || this.cvaDisabled());

  private debounceTimer: ReturnType<typeof setTimeout> | null = null;
  private onChange: (val: string) => void = () => {};
  private onTouched: () => void = () => {};

  ngOnDestroy(): void {
    if (this.debounceTimer !== null) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
  }

  writeValue(value: string | null | undefined): void {
    const val = value === null || value === undefined ? '' : String(value);
    this.innerValue.set(val);
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
    const value = target.value;
    this.innerValue.set(value);
    this.onChange(value);
    this.valueChange.emit(value);

    if (this.debounceTimer !== null) {
      clearTimeout(this.debounceTimer);
    }

    const ms = this.debounceMs();
    if (ms > 0) {
      this.debounceTimer = setTimeout(() => {
        this.search.emit(this.innerValue());
      }, ms);
    } else {
      this.search.emit(value);
    }
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      if (this.debounceTimer !== null) {
        clearTimeout(this.debounceTimer);
        this.debounceTimer = null;
      }
      this.search.emit(this.innerValue());
    } else if (event.key === 'Escape' && this.innerValue().length > 0) {
      event.preventDefault();
      this.onClear();
    }
  }

  protected onClear(): void {
    if (this.effectiveDisabled()) {
      return;
    }
    if (this.debounceTimer !== null) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
    this.innerValue.set('');
    this.onChange('');
    this.valueChange.emit('');
    this.clear.emit();
  }

  protected onBlur(): void {
    this.onTouched();
  }
}
