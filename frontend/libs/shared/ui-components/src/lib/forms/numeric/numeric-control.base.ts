import { Directive, computed, input, output, signal } from '@angular/core';
import { BbControlBase } from '../control-base';
import { groupDecimalText, scaledToText, stepFor, textToScaled } from '../decimal';

/**
 * The one numeric implementation. Money, Unit Price, Quantity, Percentage and
 * Exchange Rate are all this class with different defaults.
 *
 * **Five semantic components, one primitive — not one generic number box.**
 * They share the parsing, the scaling and the accessibility wiring because
 * those are genuinely identical; they do not share precision, range, suffix,
 * step or meaning, and those are the parts a screen gets wrong. A single
 * `bb-number-input` used for all five is how a quantity ends up capped at 100
 * because somebody copied a percentage field.
 *
 * ## The two ways this product holds a figure
 *
 * `minorDigits` says how the **stored** value is scaled, and it is the only
 * thing a caller has to get right:
 *
 * - `0` — the control's value is the decimal itself. Accounting works this way:
 *   a journal line's debit is `1250.5` and the API takes `1250.50`.
 * - `n > 0` — the control's value is an integer count of 10⁻ⁿ. The sales and
 *   purchase line grid works this way: a unit price is **paise** (`minorDigits`
 *   2, so ₹1250.50 is `125050`) and a quantity is **millionths** (`minorDigits`
 *   6, so 1 unit is `1000000`), because a document total that ties to a ledger
 *   cannot be summed in binary floating point.
 *
 * Conversion between the typed text and either of those is string surgery in
 * `decimal.ts` — nothing is multiplied or divided — so `1.15` at scale 2 is
 * `115` and never `114.99999999999999`.
 *
 * ## Editing against display
 *
 * The field holds plain digits while it has focus and re-formats to `decimals`
 * places on blur. Grouping separators are **not** inserted while editing: a
 * caret that jumps when a comma appears is worse than an unseparated number,
 * and the formatted form with its currency symbol is what the read-only
 * displays and `formatMoney` are for.
 */
@Directive()
export abstract class BbNumericControlBase extends BbControlBase<number | null> {
  /**
   * How the stored value is scaled. See the class comment — `0` is a decimal,
   * `2` is paise, `6` is the line grid's quantity.
   */
  readonly minorDigits = input<number, number | string>(0, {
    transform: (value) => Math.max(0, Math.trunc(Number(value) || 0)),
  });

  /** Decimal places shown. Defaults to whatever the semantic subclass says. */
  readonly decimals = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  /**
   * The lowest value allowed, **in the units the control stores** — so a paise
   * money field's `min` of 0 is zero rupees, and a `min` of `100` is one rupee.
   * Everything a caller passes here is in stored units, the same as the value
   * itself; only the native attribute is converted to what the field displays.
   */
  readonly min = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  readonly max = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  /** Overrides the step derived from the scale and precision. */
  readonly step = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  readonly placeholder = input<string>('');

  /** Shown inside the field's left edge — a currency symbol, usually. */
  readonly prefix = input<string | null>(null);

  /** Shown inside the right edge — `%`, a unit of measure. */
  readonly suffix = input<string | null>(null);

  /** Empty takes the semantic default — right for every figure but a counter. */
  readonly align = input<'left' | 'right' | 'center' | ''>('');

  /**
   * Group the figure with separators when the field is not being edited.
   *
   * Off for most controls and on for money — see `bb-money-input`. When it is
   * on the element becomes `type="text"`, because `type="number"` cannot hold
   * `12,34,567.89`: a browser treats a grouped value as invalid and blanks it.
   */
  readonly masked = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /**
   * The grouping mask, as `mst.Currency.Format` holds it — `##,##,##0.00` for
   * the rupee, `###,###,##0.00` for thousands. Empty means the control asks its
   * own source; `bb-money-input` reads the branch's currency.
   */
  readonly mask = input<string>('');

  /** Overrides the keyboard the derived one would ask for. */
  readonly inputmode = input<'decimal' | 'numeric' | ''>('');

  readonly valueChange = output<number | null>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly focus = output<FocusEvent>();

  /** What the `<input>` shows. Free text while typing, formatted on blur. */
  protected readonly displayText = signal<string>('');

  /** The last value published. Kept so blur can re-format without re-parsing. */
  private storedValue: number | null = null;

  protected readonly resolvedDecimals = computed(
    () => this.decimals() ?? this.defaultDecimals(),
  );

  protected readonly resolvedMin = computed(() => this.min() ?? this.defaultMin());

  protected readonly resolvedMax = computed(() => this.max() ?? this.defaultMax());

  protected readonly resolvedStep = computed(
    () => this.step() ?? stepFor(this.resolvedDecimals()),
  );

  /**
   * `min` and `max` as the native attributes want them: in displayed units.
   *
   * The browser compares `step`, `min` and `max` against the element's own
   * value, which is the decimal on screen. Passing a paise bound straight
   * through would tell the browser a ₹0.01 minimum is ₹1.
   */
  protected readonly minAttr = computed(() => this.boundAttr(this.resolvedMin()));

  protected readonly maxAttr = computed(() => this.boundAttr(this.resolvedMax()));

  /**
   * `aria-valuemin`/`aria-valuemax` are not set: `<input type="number">` is
   * already a `spinbutton` to assistive technology and takes its range from the
   * native attributes above. Restating them would be a second source for one
   * answer.
   */
  private boundAttr(bound: number | null): string | null {
    return bound === null ? null : scaledToText(bound, this.minorDigits(), 0);
  }

  protected readonly resolvedPrefix = computed(() => this.prefix() ?? this.defaultPrefix());

  protected readonly resolvedSuffix = computed(() => this.suffix() ?? this.defaultSuffix());

  protected readonly resolvedAlign = computed(() => this.align() || this.defaultAlign());

  protected readonly isMasked = computed(() => this.masked() || this.defaultMasked());

  protected readonly resolvedMask = computed(() => this.mask() || this.defaultMask());

  /**
   * `text` while masked, because grouping is invalid in a `number` input.
   *
   * The cost is the spinner arrows and the browser's own range checking, which
   * is why this is not the default: only money is worth the trade, and `min`,
   * `max` and `step` stop being emitted with it.
   */
  protected readonly fieldType = computed(() => (this.isMasked() ? 'text' : 'number'));

  /** True while the field has focus, when it shows plain digits. */
  protected readonly isFocused = signal(false);

  /**
   * `decimal` rather than `numeric` whenever fractions are allowed, because
   * `numeric` gives a phone keypad with no decimal point on it.
   */
  protected readonly inputMode = computed(
    () => this.inputmode() || this.defaultInputmode(),
  );

  /** The value as the caller last set or the user last typed. Test seam. */
  protected get value(): number | null {
    return this.storedValue;
  }

  writeValue(value: unknown): void {
    if (value === null || value === undefined || value === '') {
      this.storedValue = null;
      this.displayText.set('');
      return;
    }

    const numeric = typeof value === 'number' ? value : Number(value);
    if (!Number.isFinite(numeric)) {
      this.storedValue = null;
      this.displayText.set('');
      return;
    }

    this.storedValue = numeric;
    // Grouped unless the field is being edited: a value arriving from the form
    // while somebody types would otherwise punctuate itself under the caret.
    this.displayText.set(this.isFocused() ? this.plainText() : this.settledText());
  }

  protected emitValue(value: number | null): void {
    this.valueChange.emit(value);
  }

  /**
   * Every keystroke: parse, publish, and leave the text exactly as typed.
   *
   * **The text is not rewritten here.** Re-formatting mid-edit moves the caret,
   * so `1.5` becomes `1.50` with the caret behind the trailing zero and the
   * next digit lands in the wrong place. Formatting happens on blur.
   *
   * Nothing is clamped here either — a value below `min` is published so the
   * page's validator can refuse it and say so, which is more useful than the
   * field silently rewriting what somebody typed.
   */
  protected onInput(event: Event): void {
    const text = (event.target as HTMLInputElement).value;
    this.displayText.set(text);

    const parsed = textToScaled(text, this.minorDigits());
    this.storedValue = parsed;
    this.publish(parsed);
  }

  /**
   * Focus strips the grouping back to plain digits.
   *
   * Editing a grouped figure means every keystroke moves a separator and the
   * caret with it. Showing the number plainly while it is being edited and
   * punctuating it on the way out is the trade this control makes, and it is
   * why there is no caret arithmetic here at all.
   */
  protected onFocus(event: FocusEvent): void {
    this.isFocused.set(true);

    if (this.isMasked()) {
      this.displayText.set(this.plainText());
    }

    this.focus.emit(event);
  }

  /**
   * On blur the field settles: the text is redrawn from the stored value at the
   * field's precision, so `1.5` reads `1.50` and `007` reads `7`.
   */
  protected onBlur(event: FocusEvent): void {
    this.isFocused.set(false);
    this.displayText.set(this.settledText());
    this.onTouched();
    this.blur.emit(event);
  }

  /** The value as plain digits — what the field shows while it is being edited. */
  private plainText(): string {
    return this.storedValue === null
      ? ''
      : scaledToText(this.storedValue, this.minorDigits(), this.resolvedDecimals());
  }

  /**
   * The value as the field shows it at rest: grouped when masked, plain
   * otherwise. Both come from the same exact decimal string, so the two forms
   * can never disagree about the figure itself.
   */
  private settledText(): string {
    const plain = this.plainText();

    if (plain === '' || !this.isMasked()) {
      return plain;
    }

    return groupDecimalText(plain, this.resolvedMask());
  }

  /** Decimal places when the caller names none. */
  protected abstract defaultDecimals(): number;

  /** Null means unbounded below. */
  protected defaultMin(): number | null {
    return null;
  }

  protected defaultMax(): number | null {
    return null;
  }

  protected defaultPrefix(): string {
    return '';
  }

  protected defaultSuffix(): string {
    return '';
  }

  protected defaultAlign(): 'left' | 'right' | 'center' {
    return 'right';
  }

  protected defaultInputmode(): 'decimal' | 'numeric' {
    return this.resolvedDecimals() > 0 || this.minorDigits() > 0 ? 'decimal' : 'numeric';
  }

  /** Whether this control groups its figure at rest. Money does; nothing else. */
  protected defaultMasked(): boolean {
    return false;
  }

  /** The grouping mask when the caller names none. */
  protected defaultMask(): string {
    return '';
  }
}
