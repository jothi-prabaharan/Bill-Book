import { ChangeDetectionStrategy, Component, forwardRef, input } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbDateControlBase } from './date-control.base';

/**
 * A date and a time of day.
 *
 * The value is `yyyy-MM-ddTHH:mm` — what a native `datetime-local` holds and
 * what a C# `DateTime` round-trips through `[FromBody]` without a conversion.
 *
 * **Local, and with no offset on purpose.** Every timestamp this product edits
 * is a wall-clock time at the branch: when a ticket was raised, when a
 * statement line cleared. Appending a `Z` or an offset would make the value a
 * point on the global timeline, and the same row would then read differently to
 * a colleague in another state. Seconds are dropped for the same reason a date
 * has no time — nothing the user keys needs them, and an unasked-for `:00`
 * suggests a precision that is not there.
 *
 * A value arriving with seconds or a `Z` is trimmed to minutes rather than
 * refused; a value arriving as a bare date gets `T00:00`, so a column that
 * turns out to be a `DateOnly` still edits.
 */
@Component({
  selector: 'bb-datetime-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => DateTimeInputComponent),
      multi: true,
    },
  ],
  templateUrl: './date-control.html',
  styleUrl: './date-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DateTimeInputComponent extends BbDateControlBase {
  protected readonly inputType = 'datetime-local';

  /** Minutes, in seconds — 60 by default, so the picker offers no seconds. */
  readonly stepSeconds = input<number>(60);

  protected override get stepAttr(): string | null {
    return String(this.stepSeconds());
  }

  protected override fromDate(value: Date): string {
    const date = `${value.getFullYear()}-${this.pad(value.getMonth() + 1)}-${this.pad(value.getDate())}`;
    return `${date}T${this.pad(value.getHours())}:${this.pad(value.getMinutes())}`;
  }

  protected override fromIso(value: string): string {
    const full = /^(\d{4}-\d{2}-\d{2})[T ](\d{2}:\d{2})/.exec(value);
    if (full) {
      return `${full[1]}T${full[2]}`;
    }

    const dateOnly = /^(\d{4}-\d{2}-\d{2})$/.exec(value);
    return dateOnly ? `${dateOnly[1]}T00:00` : value;
  }

  protected override fallbackAriaLabel(): string {
    return 'Date and time';
  }

  protected idPrefix(): string {
    return 'bb-datetime';
  }
}
