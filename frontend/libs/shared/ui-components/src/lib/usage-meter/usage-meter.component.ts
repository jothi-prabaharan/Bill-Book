import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { nextFieldId } from '../forms/field-id';
import { meterFigures, meterMax, meterTone } from './usage-meter.model';

/**
 * How much of a plan's limit has been used: a label, "3 of 5", and a bar that
 * turns to a warning at four fifths and to a danger when the limit is reached
 * (TK-211).
 *
 * A native `<progress>` under the label, so a screen reader announces it as a
 * progress bar with the label as its name and the figures as its value. A count
 * with no limit shows the figures alone, without a bar to fill.
 */
@Component({
  selector: 'bb-usage-meter',
  standalone: true,
  templateUrl: './usage-meter.component.html',
  styleUrl: './usage-meter.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UsageMeterComponent {
  /** What is counted: "Users", "Sales this month". */
  readonly label = input.required<string>();

  readonly used = input.required<number>();

  /** Null is no limit. */
  readonly limit = input.required<number | null>();

  /** A line under the bar: what the count includes, or why it is full. */
  readonly note = input<string>('');

  protected readonly labelId = nextFieldId('bb-usage-meter');

  protected readonly tone = computed(() => meterTone(this.used(), this.limit()));
  protected readonly figures = computed(() => meterFigures(this.used(), this.limit()));
  protected readonly max = computed(() => meterMax(this.limit()));
}
