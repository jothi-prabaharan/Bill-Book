import { Directive, computed, input, output, signal } from '@angular/core';
import { BbControlBase } from '../control-base';

/** The `type` attributes the text primitives put on their `<input>`. */
export type BbTextInputType = 'text' | 'email' | 'password' | 'tel' | 'url' | 'search';

/**
 * The one single-line text implementation, which Text, Email, Phone and URL all are.
 *
 * They differ in four things — the `type` attribute, the `inputmode`, the
 * `autocomplete` token and the pattern they validate against — and in nothing
 * else. Four copies of the same `writeValue`, the same trim-and-uppercase and
 * the same `aria-describedby` wiring is four places for one of them to be
 * wrong, so there is one, and the subclasses are thirty lines each.
 *
 * **The value is a string, never null.** An empty field publishes `''`, which is
 * what `bb-text-input` has always done and what every page reading these
 * controls expects; whether an optional field should normalise `''` to null on
 * the way to the API is a decision that belongs to the page, not here.
 */
@Directive()
export abstract class BbTextControlBase extends BbControlBase<string> {
  /**
   * Overrides the type the subclass would use. Kept public because
   * `bb-text-input` has always accepted it.
   */
  readonly type = input<BbTextInputType | ''>('');

  readonly placeholder = input<string>('');

  readonly minlength = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  readonly maxlength = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  /**
   * A regular expression the browser checks, as an HTML `pattern`.
   *
   * **A convenience, never the boundary.** The server validates the same rule,
   * and a `pattern` that is missing or wrong makes a screen permissive, not a
   * database. Anything security-relevant is checked in the API.
   */
  readonly pattern = input<string>('');

  /** Codes and GSTINs are keyed in upper case and stored that way. */
  readonly uppercase = input<boolean>(false);

  /** Text shown inside the field's left edge — a `+91`, a `₹`, an `https://`. */
  readonly prefix = input<string | null>(null);

  readonly suffix = input<string | null>(null);

  readonly valueChange = output<string>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly focus = output<FocusEvent>();
  /** Enter in a single-line field usually means "search" or "add". */
  readonly enter = output<string>();

  protected readonly innerValue = signal<string>('');

  protected readonly resolvedType = computed<BbTextInputType>(
    () => (this.type() || this.defaultType()) as BbTextInputType,
  );

  protected readonly resolvedInputmode = computed<string | null>(
    () => this.defaultInputmode() || null,
  );

  protected readonly resolvedAutocomplete = computed(
    () => this.autocomplete() || this.defaultAutocomplete(),
  );

  protected readonly resolvedPattern = computed<string | null>(
    () => this.pattern() || this.defaultPattern() || null,
  );

  protected readonly resolvedPrefix = computed(() => this.prefix() ?? this.defaultPrefix());

  writeValue(value: unknown): void {
    if (value === null || value === undefined) {
      this.innerValue.set('');
      return;
    }
    const text = String(value);
    this.innerValue.set(this.uppercase() ? text.toUpperCase() : text);
  }

  protected emitValue(value: string): void {
    this.valueChange.emit(value);
  }

  /**
   * Every keystroke, through the field's mask.
   *
   * **The caret is put back by hand.** A mask that drops characters shortens
   * the text, and writing the shortened value into the element sends the caret
   * to the end — so typing a space in the middle of an email address would
   * throw you to the end of the field. Masking the text *before* the caret
   * gives its new position: the mask only removes and maps characters, never
   * inserts, so the length of the masked prefix is where the caret belongs.
   */
  protected onInput(event: Event): void {
    const target = event.target as HTMLInputElement;
    const typed = target.value;
    const masked = this.mask(typed);

    if (masked !== typed) {
      const caret = target.selectionStart ?? typed.length;
      const moved = this.mask(typed.slice(0, caret)).length;

      target.value = masked;
      // Only a text-ish input has a selection to set. Guarded because the DOM
      // throws on the input types that do not.
      try {
        target.setSelectionRange(moved, moved);
      } catch {
        // Nothing to restore; the value is still correct.
      }
    }

    this.innerValue.set(masked);
    this.publish(masked);
  }

  /**
   * What the field will accept, given what was typed.
   *
   * **Only ever removes or maps characters — never inserts.** The caret
   * arithmetic above depends on that, and so does the promise that the value
   * you see is the value that is stored.
   *
   * Applied to typing only, **not to `writeValue`**: a value the server sent is
   * what it is, and quietly rewriting it on load would put one thing on screen
   * and another in the form.
   */
  protected mask(text: string): string {
    return this.uppercase() ? text.toUpperCase() : text;
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

  /** The `type` attribute when the caller names none. */
  protected defaultType(): BbTextInputType {
    return 'text';
  }

  /** The on-screen keyboard to ask for. Empty leaves the attribute off. */
  protected defaultInputmode(): string {
    return '';
  }

  /**
   * `off` unless the subclass says otherwise.
   *
   * An ERP is full of fields a browser would happily fill with the wrong
   * person's address, so the default is off and the four fields where autofill
   * genuinely helps — email, phone, URL, password — turn it on by name.
   */
  protected defaultAutocomplete(): string {
    return 'off';
  }

  protected defaultPattern(): string {
    return '';
  }

  /** Affix text a subclass supplies for itself — a phone's dialling code. */
  protected defaultPrefix(): string {
    return '';
  }
}
