import { ChangeDetectionStrategy, Component, forwardRef } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../forms/form-field/form-field.component';
import { BbDateControlBase } from '../forms/datetime/date-control.base';

/**
 * A calendar date, with no time and no timezone.
 *
 * The value is `yyyy-MM-dd` — a `DateOnly` on the C# side — in and out. A full
 * timestamp handed in is trimmed to its date half rather than refused, so a
 * column that turns out to be a `DateTime` renders instead of going blank.
 *
 * **Known limitation, carried deliberately.** This is a native
 * `<input type="date">`, so the placeholder and the on-screen format come from
 * the *browser's* locale and cannot be overridden by any attribute: a branch
 * configured for `dd/MM/yyyy` still sees `mm/dd/yyyy` in the field. Nothing
 * downstream is wrong — the value it stores and publishes is ISO either way —
 * but the field disagrees with every date the product *displays*, which go
 * through `FormatSettingsService`. Fixing it needs a written-from-scratch date
 * component, which is a larger decision than one screen: it affects every date
 * field in the product. See `docs/inputs.md`.
 */
@Component({
  selector: 'bb-date-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => DateInputComponent),
      multi: true,
    },
  ],
  templateUrl: '../forms/datetime/date-control.html',
  styleUrl: '../forms/datetime/date-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DateInputComponent extends BbDateControlBase {
  protected readonly inputType = 'date';

  protected override fromDate(value: Date): string {
    return `${value.getFullYear()}-${this.pad(value.getMonth() + 1)}-${this.pad(value.getDate())}`;
  }

  /**
   * The date half of whatever arrived.
   *
   * Anything that is not an ISO date is passed through untouched rather than
   * blanked: a browser shows an unparseable value as an empty field anyway, and
   * silently dropping it would lose what the server actually sent.
   */
  protected override fromIso(value: string): string {
    const match = /^(\d{4}-\d{2}-\d{2})/.exec(value);
    return match ? match[1] : value;
  }

  protected override fallbackAriaLabel(): string {
    return 'Date';
  }

  protected idPrefix(): string {
    return 'bb-date';
  }
}
