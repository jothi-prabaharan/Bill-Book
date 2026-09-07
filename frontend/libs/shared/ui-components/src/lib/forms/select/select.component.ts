import { ChangeDetectionStrategy, Component, computed, forwardRef, input, output, signal } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { BbControlBase } from '../control-base';
import { BbSelectOption } from '../field.model';
import { FormFieldComponent } from '../form-field/form-field.component';

/**
 * One value chosen from a list.
 *
 * **A native `<select>`, and no third party.** A rebuilt listbox has to
 * reimplement type-ahead, arrow and Home/End navigation, the closed-state
 * announcement, touch behaviour and — on a phone — the operating system's own
 * picker, which is better than anything a web page draws. None of that is worth
 * trading for a custom chevron.
 *
 * Where a list is too long to scroll or needs searching, the answer is
 * `bb-search-input` beside a `bb-lookup-dialog`, which is what the sales and
 * purchase screens already do. This control is for lists somebody can read.
 *
 * **Values are not stringified.** The element's value is always a string, so
 * options are addressed by their index and the original value — a number, a
 * string — is published back untouched. Round-tripping through `String()` is
 * how a numeric id reaches the API quoted and a `Guid` comparison starts
 * failing.
 */
@Component({
  selector: 'bb-select',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SelectComponent),
      multi: true,
    },
  ],
  templateUrl: './select.component.html',
  styleUrl: './select.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SelectComponent<TValue extends string | number = string | number>
  extends BbControlBase<TValue | null>
{
  readonly options = input<readonly BbSelectOption<TValue>[]>([]);

  /** The empty first row. Empty string renders no placeholder row at all. */
  readonly placeholder = input<string>('Choose…');

  /**
   * Whether the placeholder row stays selectable once something is chosen.
   * On for an optional field — clearing it is the only way back to "none".
   */
  readonly clearable = input<boolean, boolean | string>(true, {
    transform: (value) => value !== 'false' && value !== false,
  });

  /** Swaps the options for a single "Loading…" row and disables the control. */
  readonly loading = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  /** Shown in place of the placeholder when there is nothing to choose from. */
  readonly emptyText = input<string>('Nothing to choose from');

  readonly valueChange = output<TValue | null>();

  protected readonly selected = signal<TValue | null>(null);

  protected readonly isEmpty = computed(() => !this.loading() && this.options().length === 0);

  /** Groups in the order they first appear, so the caller controls the order. */
  protected readonly groups = computed(() => {
    const names: string[] = [];
    for (const option of this.options()) {
      const group = option.group ?? '';
      if (group !== '' && !names.includes(group)) {
        names.push(group);
      }
    }
    return names;
  });

  protected readonly ungrouped = computed(() =>
    this.options().filter((option) => !option.group),
  );

  protected optionsIn(group: string): readonly BbSelectOption<TValue>[] {
    return this.options().filter((option) => option.group === group);
  }

  /**
   * The index of the selected option as a string, which is what the element's
   * `value` holds. `''` means the placeholder row.
   */
  protected readonly selectedIndex = computed(() => {
    const value = this.selected();
    if (value === null) {
      return '';
    }
    const at = this.options().findIndex((option) => option.value === value);
    return at < 0 ? '' : String(at);
  });

  writeValue(value: unknown): void {
    this.selected.set((value ?? null) as TValue | null);
  }

  protected emitValue(value: TValue | null): void {
    this.valueChange.emit(value);
  }

  protected onSelect(event: Event): void {
    const raw = (event.target as HTMLSelectElement).value;

    if (raw === '') {
      this.selected.set(null);
      this.publish(null);
      this.onTouched();
      return;
    }

    const option = this.options()[Number(raw)];
    const next = option === undefined ? null : option.value;
    this.selected.set(next);
    this.publish(next);
    this.onTouched();
  }

  protected isSelected(option: BbSelectOption<TValue>): boolean {
    return this.selected() === option.value;
  }

  protected onBlur(): void {
    this.onTouched();
  }

  protected indexOf(option: BbSelectOption<TValue>): string {
    return String(this.options().indexOf(option));
  }

  protected override fallbackAriaLabel(): string {
    return '';
  }

  protected idPrefix(): string {
    return 'bb-select';
  }
}
