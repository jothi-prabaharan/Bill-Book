import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from '@angular/core';
import { PeriodPreset, PeriodRange, rangeForPreset } from './period-range';

/**
 * The register filter bar: period presets on the left, the total of whatever is
 * currently on screen on the right.
 *
 * The from/to inputs are disabled unless the person has chosen Custom — a
 * preset and a hand-typed date disagreeing about the same range is a bug the
 * person then has to diagnose.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-period-filter-bar',
  standalone: true,
  imports: [],
  templateUrl: './period-filter-bar.component.html',
  styleUrl: './period-filter-bar.component.scss',
})
export class PeriodFilterBarComponent {
  /** Sum of the rows the register is showing right now. */
  readonly total = input<number>(0);

  /** Currency code for the kicker — the branch's own, not a conversion. */
  readonly currency = input<string>('INR');

  /** Which preset to start on. */
  readonly initialPreset = input<PeriodPreset>('this-month');

  readonly rangeChange = output<PeriodRange>();

  readonly presets: readonly { key: PeriodPreset; label: string }[] = [
    { key: 'this-month', label: 'This month' },
    { key: 'last-month', label: 'Last month' },
    { key: 'this-financial-year', label: 'This financial year' },
    { key: 'custom', label: 'Custom' },
  ];

  private readonly range = signal<PeriodRange>(rangeForPreset('this-month'));

  readonly preset = computed(() => this.range().preset);
  readonly from = computed(() => this.range().from);
  readonly to = computed(() => this.range().to);
  readonly isCustom = computed(() => this.preset() === 'custom');

  readonly totalLabel = computed(() => `Total in ${this.currency()}`);

  readonly formattedTotal = computed(() =>
    new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(this.total()),
  );

  constructor() {
    // The starting preset is an input, so it is not known until the first read.
    queueMicrotask(() => this.pick(this.initialPreset()));
  }

  pick(preset: PeriodPreset): void {
    this.commit(rangeForPreset(preset));
  }

  setFrom(value: string): void {
    this.commit({ ...this.range(), from: value });
  }

  setTo(value: string): void {
    this.commit({ ...this.range(), to: value });
  }

  private commit(range: PeriodRange): void {
    this.range.set(range);
    this.rangeChange.emit(range);
  }
}
