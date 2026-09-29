import { ChangeDetectionStrategy, Component, forwardRef } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { FormFieldComponent } from '../form-field/form-field.component';
import { BbDateControlBase } from './date-control.base';

/**
 * A calendar month — a payroll month, an attendance month, a return period.
 *
 * **The value is `yyyy-MM`**, which is what a native month input produces and
 * what every screen that asks for a month already sends. A date or a `Date`
 * written into it is cut to its month, so a field bound to the first of the
 * month shows the right one.
 *
 * Like `bb-date-input` it is the browser's own control, so the on-screen format
 * is the browser's; the value is unambiguous either way.
 */
@Component({
  selector: 'bb-month-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => MonthInputComponent),
      multi: true,
    },
  ],
  templateUrl: './date-control.html',
  styleUrl: './date-control.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonthInputComponent extends BbDateControlBase {
  protected readonly inputType = 'month';

  protected override fromDate(value: Date): string {
    return `${value.getFullYear()}-${this.pad(value.getMonth() + 1)}`;
  }

  protected override fromIso(value: string): string {
    const month = /^(\d{4}-\d{2})(?:-\d{2})?/.exec(value);
    return month ? month[1] : value;
  }

  protected override fallbackAriaLabel(): string {
    return 'Month';
  }

  protected idPrefix(): string {
    return 'bb-month';
  }
}
