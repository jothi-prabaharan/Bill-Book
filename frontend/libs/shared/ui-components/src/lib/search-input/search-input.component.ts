import {
  ChangeDetectionStrategy,
  Component,
  forwardRef,
  input,
  OnDestroy,
  output,
  signal,
} from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { BbControlBase } from '../forms/control-base';
import { FormFieldComponent } from '../forms/form-field/form-field.component';

/**
 * A search box: an icon, a debounce, a clear button and Escape to clear.
 *
 * **It never calls an API.** It publishes what was typed and emits `search`
 * once typing settles; fetching, cancelling and racing belong to the page,
 * which is the only place that knows what is being searched and what to do when
 * two answers come back out of order. A component that fetched would also have
 * to know about permissions, org context and error display — none of which is a
 * text box's business.
 *
 * `loading` is a caller's flag for the same reason: only the page knows whether
 * its request is still out.
 */
@Component({
  selector: 'bb-search-input',
  standalone: true,
  imports: [FormFieldComponent],
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
export class SearchInputComponent extends BbControlBase<string> implements OnDestroy {
  readonly placeholder = input<string>('Search...');

  /**
   * How long typing must stop before `search` fires. Zero fires on every
   * keystroke, which is right for filtering a list already in memory and wrong
   * for anything that reaches the network.
   */
  readonly debounceMs = input<number>(300);

  /** Shows a busy state and tells assistive technology the results are stale. */
  readonly loading = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /** The id of the results element, so a screen reader ties the two together. */
  readonly controlsId = input<string>('');

  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly search = output<string>();
  readonly clear = output<void>();
  readonly valueChange = output<string>();

  protected readonly innerValue = signal<string>('');

  private debounceTimer: ReturnType<typeof setTimeout> | null = null;

  ngOnDestroy(): void {
    this.cancelPending();
  }

  writeValue(value: unknown): void {
    this.innerValue.set(value === null || value === undefined ? '' : String(value));
  }

  protected emitValue(value: string): void {
    this.valueChange.emit(value);
  }

  protected onInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.innerValue.set(value);
    this.publish(value);

    this.cancelPending();

    const wait = this.debounceMs();
    if (wait > 0) {
      this.debounceTimer = setTimeout(() => {
        this.debounceTimer = null;
        this.search.emit(this.innerValue());
      }, wait);
    } else {
      this.search.emit(value);
    }
  }

  protected onKeyDown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.cancelPending();
      this.search.emit(this.innerValue());
      return;
    }

    if (event.key === 'Escape' && this.innerValue().length > 0) {
      // Stops the key reaching a dialog that would otherwise close behind the
      // search box the user was only trying to empty.
      event.preventDefault();
      event.stopPropagation();
      this.onClear();
    }
  }

  protected onClear(): void {
    if (this.effectiveDisabled() || this.readonly()) {
      return;
    }
    this.cancelPending();
    this.innerValue.set('');
    this.publish('');
    this.clear.emit();
  }

  protected onBlur(): void {
    this.onTouched();
  }

  private cancelPending(): void {
    if (this.debounceTimer !== null) {
      clearTimeout(this.debounceTimer);
      this.debounceTimer = null;
    }
  }

  protected override fallbackAriaLabel(): string {
    return 'Search';
  }

  protected idPrefix(): string {
    return 'bb-search';
  }
}
