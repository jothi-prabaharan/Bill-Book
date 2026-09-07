import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * The label, hint and error around a control — and nothing else.
 *
 * **Presentational only.** It holds no value, implements no `ControlValueAccessor`
 * and knows nothing about what it wraps; the control passes it the ids it has
 * already put on itself. That separation is what lets one wrapper serve a text
 * box, a checkbox group and a rich-text editor without any of them leaking into
 * it.
 *
 * **The host is `display: contents`** so this element adds no box of its own.
 * Every form in the product is already laid out as `.field > label + .input +
 * .field-error` by `libs/shared/theming/_forms.scss`; rendering those same three
 * children straight into the caller's `.field` means the wrapper inherits the
 * house styling instead of restating it, and a control that gains a label does
 * not shift on the page.
 *
 * Error beats hint, rather than both showing: two lines of small print under a
 * field is where people stop reading the one that matters.
 */
@Component({
  selector: 'bb-form-field',
  standalone: true,
  imports: [],
  templateUrl: './form-field.component.html',
  styleUrl: './form-field.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FormFieldComponent {
  /** Omitted means no label element at all — a control inside a grid cell. */
  readonly label = input<string>('');

  /** The id of the control this labels. Required for the label to be clickable. */
  readonly controlId = input<string>('');

  readonly required = input<boolean>(false);

  readonly hint = input<string>('');

  /** Non-empty puts the field in its error state and hides the hint. */
  readonly error = input<string>('');

  readonly hintId = input<string>('');
  readonly errorId = input<string>('');

  /**
   * A label the control renders for itself — a checkbox's own text, a radio
   * group's legend. Turns the `<label for>` into a plain caption so the click
   * target is not stolen from the control.
   */
  readonly labelAsCaption = input<boolean>(false);

  protected readonly showHint = computed(() => this.error() === '' && this.hint() !== '');
}
