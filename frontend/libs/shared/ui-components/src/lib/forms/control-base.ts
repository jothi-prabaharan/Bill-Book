import { Directive, computed, input, signal } from '@angular/core';
import { ControlValueAccessor } from '@angular/forms';
import { nextFieldId } from './field-id';

/**
 * Everything every common input does the same way.
 *
 * Labelling, hint and error ids, the disabled/readonly pair and the
 * `ControlValueAccessor` plumbing are identical in nineteen controls. Written
 * once here they cannot drift; written per control they would, and the way that
 * shows up is one field on one screen that a screen reader reads as unlabelled.
 *
 * **`@Directive()` with no selector is what makes the inputs inherit.** Signal
 * inputs are collected by the compiler from the decorated class, so an
 * undecorated base would compile and then silently accept none of them.
 *
 * Subclasses supply `writeValue` and whatever their own value type needs. The
 * three callbacks are held here because `registerOnChange` is the same eight
 * lines everywhere.
 */
@Directive()
export abstract class BbControlBase<TValue> implements ControlValueAccessor {
  /** Supply one to write your own `for`/`id` pair; otherwise one is generated. */
  readonly id = input<string>('');

  readonly name = input<string>('');

  /** Empty renders no label element — a control in a grid cell wants none. */
  readonly label = input<string>('');

  /** Shown under the field unless there is an error, which replaces it. */
  readonly hint = input<string>('');

  /**
   * The refusal to show under the field.
   *
   * **A string the page owns, not a validator result.** The page knows whether
   * the control has been touched, what the server said and which of several
   * broken rules is worth naming; a control guessing at that would show
   * "Required" on an untouched form.
   */
  readonly error = input<string>('');

  readonly required = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  readonly disabled = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /**
   * Editable-looking but not editable, and **still submitted**. Different from
   * disabled, which removes the value from the form. Reach for readonly when
   * the figure matters and cannot be changed here — a posted document — and for
   * disabled when it does not apply at all.
   */
  readonly readonly = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /** Only needed when there is no visible label — otherwise the label is the name. */
  readonly ariaLabel = input<string>('');

  readonly autocomplete = input<string>('');

  /** Every control gets an id whether or not the caller wrote one. */
  private readonly generatedId: string = nextFieldId(this.idPrefix());

  readonly controlId = computed(() => this.id() || this.generatedId);

  protected readonly hintId = computed(() => `${this.controlId()}-hint`);
  protected readonly errorId = computed(() => `${this.controlId()}-error`);

  protected readonly invalid = computed(() => this.error() !== '');

  /**
   * The ids of everything describing this control, in reading order.
   *
   * Null rather than an empty string when there is nothing: `aria-describedby=""`
   * is a pointer to no element, which some screen readers report as a broken
   * reference rather than as an absent one.
   */
  protected readonly describedBy = computed<string | null>(() => {
    if (this.error()) {
      return this.errorId();
    }
    return this.hint() ? this.hintId() : null;
  });

  /** What a screen reader announces for a field with no visible label. */
  protected readonly effectiveAriaLabel = computed<string | null>(
    () => this.ariaLabel() || (this.label() ? '' : this.fallbackAriaLabel()) || null,
  );

  private readonly cvaDisabled = signal(false);

  /** Disabled by the attribute, or by `FormControl.disable()`. Either counts. */
  protected readonly effectiveDisabled = computed(() => this.disabled() || this.cvaDisabled());

  protected onChange: (value: TValue) => void = () => undefined;
  protected onTouched: () => void = () => undefined;

  abstract writeValue(value: unknown): void;

  registerOnChange(fn: (value: TValue) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.cvaDisabled.set(isDisabled);
  }

  /** Publishes a value to the form and to any `(valueChange)` the page bound. */
  protected publish(value: TValue): void {
    this.onChange(value);
    this.emitValue(value);
  }

  /** Subclasses own their `valueChange` output, so they do the emitting. */
  protected abstract emitValue(value: TValue): void;

  /** Prefixes the generated id so a DOM dump is readable. */
  protected abstract idPrefix(): string;

  /** Used only when there is no label and the caller gave no `ariaLabel`. */
  protected fallbackAriaLabel(): string {
    return '';
  }
}
