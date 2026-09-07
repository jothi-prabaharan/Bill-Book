import { Directive, input, output, signal } from '@angular/core';
import { BbControlBase } from '../control-base';

/**
 * What `bb-date-input` and `bb-datetime-input` share.
 *
 * **Nothing here ever constructs a `Date` from a string.**
 * `new Date('2026-09-07')` is parsed as midnight **UTC**, so west of Greenwich
 * it renders as the sixth — an invoice date that moves by a day depending on
 * who opens the screen, which surfaces months later in a filed GST return. The
 * whole value handling is therefore string surgery: an ISO date in, the same
 * ISO date out, and no timezone anywhere in between.
 *
 * A `Date` object handed in by a caller *is* accepted, because some pages hold
 * one — and it is read with `getFullYear`/`getMonth`/`getDate`, the local
 * accessors, so the day the user's calendar shows is the day that is stored.
 * Reading `toISOString()` instead would shift it, which is the same bug from
 * the other side.
 */
@Directive()
export abstract class BbDateControlBase extends BbControlBase<string | null> {
  /** Earliest allowed, in the same ISO form the control publishes. */
  readonly min = input<string | null>(null);

  readonly max = input<string | null>(null);

  readonly placeholder = input<string>('');

  readonly valueChange = output<string | null>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly focus = output<FocusEvent>();

  protected readonly innerValue = signal<string>('');

  /** `step` on the native element. Only `datetime-local` has a use for one. */
  protected get stepAttr(): string | null {
    return null;
  }

  writeValue(value: unknown): void {
    if (value === null || value === undefined || value === '') {
      this.innerValue.set('');
      return;
    }

    if (value instanceof Date) {
      this.innerValue.set(Number.isNaN(value.getTime()) ? '' : this.fromDate(value));
      return;
    }

    if (typeof value === 'string') {
      this.innerValue.set(this.fromIso(value));
      return;
    }

    this.innerValue.set('');
  }

  protected emitValue(value: string | null): void {
    this.valueChange.emit(value);
  }

  /**
   * The element's value, published verbatim.
   *
   * A native date input's value is already `yyyy-MM-dd` (and a datetime-local's
   * `yyyy-MM-ddTHH:mm`) in every browser, whatever the locale it *displays*.
   * That is the whole reason the native control is used: the display is the
   * browser's business and the value is unambiguous.
   */
  protected onInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    this.innerValue.set(raw);
    this.publish(raw.trim() === '' ? null : raw);
  }

  protected onBlur(event: FocusEvent): void {
    this.onTouched();
    this.blur.emit(event);
  }

  protected onFocus(event: FocusEvent): void {
    this.focus.emit(event);
  }

  /** A `Date` read through its local accessors, never through `toISOString`. */
  protected abstract fromDate(value: Date): string;

  /** An incoming string trimmed to the part this control edits. */
  protected abstract fromIso(value: string): string;

  protected pad(value: number): string {
    return String(value).padStart(2, '0');
  }
}
