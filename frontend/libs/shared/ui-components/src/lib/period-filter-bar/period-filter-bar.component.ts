import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FormatSettingsService } from '@bill-book/currency-format';
import { BranchDateInputComponent } from '../branch-date-input/branch-date-input.component';
import { registerAmount } from '../register/register-format';
import { PeriodPreset, PeriodRange, rangeForPreset } from './period-range';

/**
 * The register filter bar: period presets, From and To, the total of whatever
 * is currently on screen, and any filters of the page's own between them.
 *
 * The from/to inputs are disabled unless the person has chosen Custom — a
 * preset and a hand-typed date disagreeing about the same range is a bug the
 * person then has to diagnose. Under Custom the dates are a draft until
 * **Apply** (TK-187): the register does not refilter on every keystroke of a
 * half-typed date, and Apply is only offered when the draft differs from what
 * is shown and From is not after To.
 *
 * It draws no frame of its own: it sits at the top of the register card, above
 * the grid, with a hairline under it.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-period-filter-bar',
  standalone: true,
  imports: [FormsModule, BranchDateInputComponent],
  templateUrl: './period-filter-bar.component.html',
  styleUrl: './period-filter-bar.component.scss',
})
export class PeriodFilterBarComponent {
  private readonly formats = inject(FormatSettingsService);

  /** Sum of the rows the register is showing right now. */
  readonly total = input<number>(0);

  /** Currency code for the kicker. Empty is the branch's own, not a conversion. */
  readonly currency = input<string>('');

  /** Which preset to start on. */
  readonly initialPreset = input<PeriodPreset>('this-month');

  readonly rangeChange = output<PeriodRange>();

  readonly presets: readonly { key: PeriodPreset; label: string }[] = [
    { key: 'this-month', label: 'This month' },
    { key: 'last-month', label: 'Last month' },
    { key: 'this-financial-year', label: 'This financial year' },
    { key: 'custom', label: 'Custom' },
  ];

  /** The range the register is filtered to. */
  private readonly range = signal<PeriodRange>(rangeForPreset('this-month'));

  /** Custom dates typed but not yet applied; null when nothing is pending. */
  private readonly draft = signal<{ from: string; to: string } | null>(null);

  /** Modal popup open state for mobile view. */
  readonly modalOpen = signal(false);

  readonly preset = computed(() => this.range().preset);
  readonly from = computed(() => this.draft()?.from ?? this.range().from);
  readonly to = computed(() => this.draft()?.to ?? this.range().to);
  readonly isCustom = computed(() => this.preset() === 'custom');

  /** Whether Apply would change anything, and the dates make a range. */
  readonly canApply = computed(() => {
    const draft = this.draft();
    if (!this.isCustom() || !draft) return false;
    if (draft.from && draft.to && draft.from > draft.to) return false;
    const range = this.range();
    return draft.from !== range.from || draft.to !== range.to;
  });

  readonly canApplyMobile = computed(() => {
    const f = this.from();
    const t = this.to();
    if (f && t && f > t) return false;
    return true;
  });

  readonly activePresetLabel = computed(() => {
    const p = this.presets.find((x) => x.key === this.preset());
    return p ? p.label : 'Date filter';
  });

  readonly totalLabel = computed(
    () => `Total in ${this.currency() || this.formats.settings().currencyCode}`,
  );

  readonly formattedTotal = computed(() =>
    registerAmount(this.total(), this.formats.settings(), this.currency() || null),
  );

  constructor() {
    // The starting preset is an input, so it is not known until the first read.
    queueMicrotask(() => this.pick(this.initialPreset()));
  }

  /**
   * A preset applies at once. Custom starts from the dates on screen, so
   * switching to it changes nothing until a date is changed and applied.
   */
  pick(preset: PeriodPreset): void {
    this.draft.set(null);
    if (preset === 'custom') {
      const { from, to } = this.range();
      this.commit({ preset: 'custom', from, to });
      return;
    }
    this.commit(rangeForPreset(preset));
  }

  setFrom(value: string | null): void {
    this.draft.set({ from: value ?? '', to: this.to() });
  }

  setTo(value: string | null): void {
    this.draft.set({ from: this.from(), to: value ?? '' });
  }

  apply(): void {
    const draft = this.draft();
    if (!this.canApply() || !draft) return;
    this.draft.set(null);
    this.commit({ preset: 'custom', from: draft.from, to: draft.to });
  }

  openModal(): void {
    if (this.isCustom()) {
      this.draft.set({ from: this.from(), to: this.to() });
    } else {
      this.draft.set(null);
    }
    this.modalOpen.set(true);
  }

  closeModal(): void {
    this.draft.set(null);
    this.modalOpen.set(false);
  }

  pickAndApplyPreset(preset: PeriodPreset): void {
    this.pick(preset);
    if (preset !== 'custom') {
      this.closeModal();
    }
  }

  applyAndClose(): void {
    if (this.isCustom()) {
      const f = this.from();
      const t = this.to();
      if (!f || !t || f <= t) {
        this.commit({ preset: 'custom', from: f, to: t });
        this.draft.set(null);
      }
    }
    this.closeModal();
  }

  private commit(range: PeriodRange): void {
    this.range.set(range);
    this.rangeChange.emit(range);
  }
}

