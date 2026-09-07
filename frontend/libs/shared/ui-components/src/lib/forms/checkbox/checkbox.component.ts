import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  forwardRef,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { NG_VALUE_ACCESSOR } from '@angular/forms';
import { BbControlBase } from '../control-base';
import { FormFieldComponent } from '../form-field/form-field.component';

/**
 * A boolean — active, is-customer, is-vendor, use-SSL, one permission.
 *
 * **A real `<input type="checkbox">`, not a styled `<div>`.** The native
 * element brings the space-bar toggle, the form association, the label click
 * target, the `aria-checked` state and the operating system's own high-contrast
 * rendering. Every one of those has to be rebuilt, badly, the moment it is
 * replaced — and the ones that get missed are the ones nobody using a mouse
 * ever notices.
 *
 * `indeterminate` is a **property**, not an attribute: it cannot be set from
 * markup at all, which is why it is written to the element here.
 *
 * There is no readonly checkbox in HTML — `readonly` is ignored on one. So
 * `readonly` disables the element and keeps the value in the form, which is the
 * behaviour readonly is asking for; that is the difference from `disabled`,
 * which Angular's `FormControl.disable()` also strips from the form value.
 */
@Component({
  selector: 'bb-checkbox',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => CheckboxComponent),
      multi: true,
    },
  ],
  templateUrl: './checkbox.component.html',
  styleUrl: './checkbox.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CheckboxComponent extends BbControlBase<boolean> {
  /** The text beside the box. Falls back to `label` when only that is given. */
  readonly text = input<string>('');

  /**
   * Neither on nor off — a "select all" over a partly selected list. Purely
   * visual: the value stays whatever it is until somebody clicks.
   */
  readonly indeterminate = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  readonly valueChange = output<boolean>();

  protected readonly checked = signal(false);

  private readonly box = viewChild<ElementRef<HTMLInputElement>>('box');

  /** The caption over the control, when the caller gave both a label and text. */
  protected readonly groupLabel = computed(() => (this.text() ? this.label() : ''));

  protected readonly boxText = computed(() => this.text() || this.label());

  constructor() {
    super();

    // `indeterminate` exists only as a DOM property, so it is written rather
    // than bound. An effect rather than ngOnChanges because the input is a
    // signal and the element may not exist on the first run.
    effect(() => {
      const element = this.box()?.nativeElement;
      if (element) {
        element.indeterminate = this.indeterminate();
      }
    });
  }

  writeValue(value: unknown): void {
    this.checked.set(value === true);
  }

  protected emitValue(value: boolean): void {
    this.valueChange.emit(value);
  }

  protected onChangeEvent(event: Event): void {
    const next = (event.target as HTMLInputElement).checked;
    this.checked.set(next);
    this.publish(next);
    this.onTouched();
  }

  protected override fallbackAriaLabel(): string {
    return '';
  }

  protected idPrefix(): string {
    return 'bb-checkbox';
  }
}
