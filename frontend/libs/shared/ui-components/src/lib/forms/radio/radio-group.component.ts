import { ChangeDetectionStrategy, Component, forwardRef, input, output, signal } from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { BbControlBase } from '../control-base';
import { BbRadioOption } from '../field.model';
import { nextFieldId } from '../field-id';

/**
 * One choice from a small, fixed set.
 *
 * **Native `<input type="radio">` inside a `<fieldset>` with a `<legend>`.**
 * That pairing is what gives the group arrow-key navigation, roving focus, a
 * single tab stop and — from the legend — a name a screen reader reads before
 * each option. Rebuilding any of it over `<div role="radiogroup">` means
 * rebuilding all of it, and the arrow keys are the part that gets forgotten.
 *
 * There is no `bb-radio`: a lone radio button is not a control, it is half of
 * one, and offering it separately invites a group with no legend and no shared
 * `name`. The options are data instead, which is also what lets a page build
 * them from an API response.
 */
@Component({
  selector: 'bb-radio-group',
  standalone: true,
  // No `bb-form-field` here, and deliberately: a radio group's name has to be
  // a `<legend>` inside its `<fieldset>` for a screen reader to read it ahead of
  // each option, and the wrapper's host is `display: contents`, which would put
  // the legend outside the fieldset in the DOM. It draws the same three
  // elements — legend, hint, error — with the same classes instead.
  imports: [],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => RadioGroupComponent),
      multi: true,
    },
  ],
  templateUrl: './radio-group.component.html',
  styleUrl: './radio-group.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RadioGroupComponent<TValue extends string | number = string | number>
  extends BbControlBase<TValue | null>
{
  readonly options = input<readonly BbRadioOption<TValue>[]>([]);

  /** Side by side rather than stacked. Wraps to a column under ~360px. */
  readonly orientation = input<'vertical' | 'horizontal'>('vertical');

  readonly valueChange = output<TValue | null>();

  protected readonly selected = signal<TValue | null>(null);

  /**
   * The shared `name`, which is what makes the buttons one group.
   *
   * Generated when the caller gives none: two groups sharing a name behave as
   * one, so this cannot be allowed to default to a constant.
   */
  private readonly generatedName = nextFieldId('bb-radio-group');

  protected readonly groupName = (): string => this.name() || this.generatedName;

  protected optionId(index: number): string {
    return `${this.controlId()}-option-${index}`;
  }

  protected optionDescriptionId(index: number): string {
    return `${this.optionId(index)}-description`;
  }

  writeValue(value: unknown): void {
    this.selected.set((value ?? null) as TValue | null);
  }

  protected emitValue(value: TValue | null): void {
    this.valueChange.emit(value);
  }

  protected onSelect(option: BbRadioOption<TValue>): void {
    if (this.effectiveDisabled() || this.readonly() || option.disabled) {
      return;
    }
    this.selected.set(option.value);
    this.publish(option.value);
    this.onTouched();
  }

  protected isSelected(option: BbRadioOption<TValue>): boolean {
    return this.selected() === option.value;
  }

  protected idPrefix(): string {
    return 'bb-radio';
  }
}
