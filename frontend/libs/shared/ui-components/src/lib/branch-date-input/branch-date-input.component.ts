import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  forwardRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormatSettingsService, formatDate, parseDate } from '@bill-book/currency-format';
import { BbControlBase } from '../forms/control-base';
import { FormFieldComponent } from '../forms/form-field/form-field.component';
import {
  CalendarDay,
  addDays,
  addMonths,
  isoParts,
  monthGrid,
  outOfRange,
  todayIso,
  toIso,
  weekdayIndex,
} from './calendar';

const MONTHS_LONG = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December',
];

/**
 * A calendar date shown and typed in **the branch's own format** (TK-23).
 *
 * The proposed replacement for `bb-date-input`, built beside it and **not yet
 * swapped in**: it changes every date field in the product, so the owner tries
 * it first (TK-23, "Show the proposal to the owner before swapping it in").
 *
 * - **The value is ISO `yyyy-MM-dd`, in and out**, exactly as `bb-date-input`'s
 *   is, so swapping the selector is the whole migration for all 26 callers.
 * - **The display is `FormatSettingsService.settings().datePattern`.** Typed
 *   text is read in that pattern by `parseDate`, with any separator accepted;
 *   what cannot be read is kept on screen with a message, and the form receives
 *   null, so a required validator still refuses it.
 * - **The calendar** is a dialog with a grid: arrow keys move a day or a week,
 *   Page Up and Page Down a month, Home and End the week's ends, Enter or Space
 *   picks, Escape closes and returns focus to the button. At phone width it is
 *   a full-screen sheet.
 */
@Component({
  selector: 'bb-branch-date-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => BranchDateInputComponent),
      multi: true,
    },
  ],
  templateUrl: './branch-date-input.component.html',
  styleUrl: './branch-date-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BranchDateInputComponent extends BbControlBase<string | null> {
  private readonly formats = inject(FormatSettingsService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  /** Earliest allowed, ISO. */
  readonly min = input<string | null>(null);

  /** Latest allowed, ISO. */
  readonly max = input<string | null>(null);

  /** Overrides the pattern shown as the placeholder. */
  readonly placeholder = input<string>('');

  readonly valueChange = output<string | null>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly blur = output<FocusEvent>();
  // eslint-disable-next-line @angular-eslint/no-output-native
  readonly focus = output<FocusEvent>();

  protected readonly weekdays = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];

  /** The committed value, ISO, or null. */
  protected readonly value = signal<string | null>(null);

  /** What is being typed; null when the field shows the committed value. */
  protected readonly draft = signal<string | null>(null);

  protected readonly problem = signal<string>('');

  protected readonly open = signal(false);

  /** The day the calendar's keyboard focus is on. */
  protected readonly cursor = signal<string>(todayIso());

  protected readonly pattern = computed(() => this.formats.settings().datePattern);

  protected readonly text = computed(
    () => this.draft() ?? formatDate(this.value(), this.pattern()),
  );

  protected readonly shownPlaceholder = computed(
    () => this.placeholder() || this.pattern().toLowerCase(),
  );

  /** The page's own error wins; otherwise what the typing could not be read as. */
  protected readonly shownError = computed(() => this.error() || this.problem());

  protected readonly isInvalid = computed(() => this.shownError() !== '');

  protected readonly describedByIds = computed<string | null>(() => {
    if (this.shownError()) {
      return this.errorId();
    }
    return this.hint() ? this.hintId() : null;
  });

  protected readonly dialogId = computed(() => `${this.controlId()}-calendar`);

  protected readonly monthLabel = computed(() => {
    const { year, month } = isoParts(this.cursor());
    return `${MONTHS_LONG[month - 1]} ${year}`;
  });

  protected readonly weeks = computed<CalendarDay[][]>(() => monthGrid(this.cursor()));

  protected readonly today = todayIso();

  writeValue(value: unknown): void {
    this.draft.set(null);
    this.problem.set('');

    if (typeof value === 'string' && value !== '') {
      const match = /^(\d{4}-\d{2}-\d{2})/.exec(value);
      this.value.set(match ? match[1] : null);
    } else if (value instanceof Date && !Number.isNaN(value.getTime())) {
      this.value.set(toIso(value.getFullYear(), value.getMonth() + 1, value.getDate()));
    } else {
      this.value.set(null);
    }
  }

  protected emitValue(value: string | null): void {
    this.valueChange.emit(value);
  }

  protected idPrefix(): string {
    return 'bb-bdate';
  }

  protected override fallbackAriaLabel(): string {
    return 'Date';
  }

  protected onInput(event: Event): void {
    this.draft.set((event.target as HTMLInputElement).value);
    this.problem.set('');
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter') {
      this.commit();
    } else if (event.key === 'ArrowDown' && event.altKey) {
      event.preventDefault();
      this.openCalendar();
    }
  }

  protected onBlur(event: FocusEvent): void {
    this.commit();
    this.onTouched();
    this.blur.emit(event);
  }

  protected onFocus(event: FocusEvent): void {
    this.focus.emit(event);
  }

  /** Reads what was typed, and publishes the date or null. */
  protected commit(): void {
    const draft = this.draft();
    if (draft === null) {
      return;
    }

    if (draft.trim() === '') {
      this.accept(null);
      return;
    }

    const iso = parseDate(draft, this.pattern());
    if (iso === null) {
      this.problem.set(`Enter a date as ${this.shownPlaceholder()}.`);
      this.value.set(null);
      this.publish(null);
      return;
    }

    if (outOfRange(iso, this.min(), this.max())) {
      this.problem.set(this.rangeMessage());
      this.value.set(null);
      this.publish(null);
      return;
    }

    this.accept(iso);
  }

  protected toggleCalendar(): void {
    if (this.open()) {
      this.closeCalendar(true);
    } else {
      this.openCalendar();
    }
  }

  protected openCalendar(): void {
    if (this.effectiveDisabled() || this.readonly()) {
      return;
    }

    this.commit();
    this.cursor.set(this.value() ?? this.clamp(this.today));
    this.open.set(true);
    this.focusCursor();
  }

  protected closeCalendar(returnFocus: boolean): void {
    this.open.set(false);
    if (returnFocus) {
      setTimeout(() => this.host.nativeElement.querySelector<HTMLButtonElement>('.bb-bdate__toggle')?.focus());
    }
  }

  protected pick(day: CalendarDay): void {
    if (this.isDisabledDay(day.iso)) {
      return;
    }
    this.accept(day.iso);
    this.onTouched();
    this.closeCalendar(true);
  }

  protected moveMonth(months: number): void {
    this.cursor.set(addMonths(this.cursor(), months));
  }

  private onGridKeydown(event: KeyboardEvent): void {
    const cursor = this.cursor();
    let next: string | null = null;

    switch (event.key) {
      case 'ArrowLeft':
        next = addDays(cursor, -1);
        break;
      case 'ArrowRight':
        next = addDays(cursor, 1);
        break;
      case 'ArrowUp':
        next = addDays(cursor, -7);
        break;
      case 'ArrowDown':
        next = addDays(cursor, 7);
        break;
      case 'PageUp':
        next = addMonths(cursor, event.shiftKey ? -12 : -1);
        break;
      case 'PageDown':
        next = addMonths(cursor, event.shiftKey ? 12 : 1);
        break;
      case 'Home':
        next = addDays(cursor, -weekdayIndex(cursor));
        break;
      case 'End':
        next = addDays(cursor, 6 - weekdayIndex(cursor));
        break;
      case 'Enter':
      case ' ':
        event.preventDefault();
        this.pick({ iso: cursor, day: isoParts(cursor).day, inMonth: true });
        return;
      case 'Escape':
        event.preventDefault();
        this.closeCalendar(true);
        return;
      default:
        return;
    }

    event.preventDefault();
    this.cursor.set(next);
    this.focusCursor();
  }

  /** Keys inside the calendar: a day button moves the cursor; anywhere, Escape closes. */
  protected onDialogKeydown(event: KeyboardEvent): void {
    if ((event.target as HTMLElement | null)?.dataset?.['iso']) {
      this.onGridKeydown(event);
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeCalendar(true);
    }
  }

  protected isDisabledDay(iso: string): boolean {
    return outOfRange(iso, this.min(), this.max());
  }

  /** "Thursday 24 September 2026", which is what a screen reader announces for a day. */
  protected dayLabel(iso: string): string {
    const { year, month, day } = isoParts(iso);
    const weekday = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'][weekdayIndex(iso)];
    return `${weekday} ${day} ${MONTHS_LONG[month - 1]} ${year}`;
  }

  private accept(iso: string | null): void {
    this.draft.set(null);
    this.problem.set('');
    this.value.set(iso);
    this.publish(iso);
  }

  private clamp(iso: string): string {
    const min = this.min();
    const max = this.max();
    if (min && iso < min) return min;
    if (max && iso > max) return max;
    return iso;
  }

  private rangeMessage(): string {
    const min = this.min();
    const max = this.max();
    const pattern = this.pattern();
    if (min && max) return `Enter a date from ${formatDate(min, pattern)} to ${formatDate(max, pattern)}.`;
    if (min) return `Enter a date on or after ${formatDate(min, pattern)}.`;
    return `Enter a date on or before ${formatDate(max, pattern)}.`;
  }

  private focusCursor(): void {
    setTimeout(() =>
      this.host.nativeElement
        .querySelector<HTMLButtonElement>(`[data-iso="${this.cursor()}"]`)
        ?.focus(),
    );
  }
}
