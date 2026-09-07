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
import { isRichTextEmpty, richTextToPlain, sanitizeRichText } from './sanitize-html';

/** One button on the toolbar. `block` commands take an argument. */
export type BbRichTextCommand =
  | 'bold'
  | 'italic'
  | 'underline'
  | 'bulletList'
  | 'numberList'
  | 'clear';

interface ToolbarButton {
  command: BbRichTextCommand;
  label: string;
  /** The `document.execCommand` name and its argument, where it has one. */
  native: string;
}

const TOOLBAR: readonly ToolbarButton[] = [
  { command: 'bold', label: 'Bold', native: 'bold' },
  { command: 'italic', label: 'Italic', native: 'italic' },
  { command: 'underline', label: 'Underline', native: 'underline' },
  { command: 'bulletList', label: 'Bulleted list', native: 'insertUnorderedList' },
  { command: 'numberList', label: 'Numbered list', native: 'insertOrderedList' },
  { command: 'clear', label: 'Remove formatting', native: 'removeFormat' },
];

/**
 * Formatted text — a long description, a terms block, a note with emphasis.
 *
 * **No editor package was added, and none should be.** The repository had no
 * rich-text editor before this and adding one would be a new dependency in a
 * task whose brief forbids it. What is here is a `contenteditable` region with a
 * small toolbar over the browser's own editing commands: bold, italic,
 * underline, the two list kinds, and remove-formatting. That is the whole set
 * a description field in an ERP needs, and every one of them is a command every
 * browser has implemented for twenty years.
 *
 * ## Security
 *
 * **Every value is sanitised on the way in, after every paste and on the way
 * out**, by `sanitizeRichText` — an allowlist, so markup nobody anticipated is
 * dropped rather than passed.
 *
 * The read-only rendering binds `[innerHTML]` to the value **without**
 * `bypassSecurityTrustHtml`, so Angular's own sanitiser filters it a second
 * time. That word — bypass — appears nowhere in this file on purpose: the value
 * is safe because it was filtered, not because this component asserted it was,
 * and an assertion is exactly what would survive a later change to the
 * allowlist that let something through.
 *
 * A paste is intercepted rather than left to the browser, because pasting from
 * Word or a web page is exactly where a page's markup — and its styles, and its
 * scripts — would otherwise arrive whole.
 *
 * ## Accessibility
 *
 * The editable region is `role="textbox"` with `aria-multiline`, labelled by
 * the field's own label and described by its hint or error like every other
 * control. The toolbar is a `role="toolbar"` whose buttons each carry a name
 * and an `aria-pressed` state, and it is reachable by tab. `execCommand` is
 * deprecated but is the only cross-browser way to apply formatting to a
 * selection; nothing replaces it yet, and the alternative is a selection-range
 * implementation of our own, which is a far larger surface to get wrong.
 */
@Component({
  selector: 'bb-rich-text-input',
  standalone: true,
  imports: [FormFieldComponent],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => RichTextInputComponent),
      multi: true,
    },
  ],
  templateUrl: './rich-text-input.component.html',
  styleUrl: './rich-text-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RichTextInputComponent extends BbControlBase<string> {
  readonly placeholder = input<string>('');

  /** Roughly how tall the editing area starts, in lines. */
  readonly rows = input<number, number | string>(4, {
    transform: (value) => Number(value) || 4,
  });

  /**
   * Which buttons to offer. The default is all of them; a field that should
   * take emphasis but not lists passes the two it wants.
   */
  readonly toolbar = input<readonly BbRichTextCommand[]>(
    TOOLBAR.map((button) => button.command),
  );

  /** Counted against the plain text, not the markup — the tags are not content. */
  readonly maxlength = input<number | null, number | string | null>(null, {
    transform: (value) => (value != null && value !== '' ? Number(value) : null),
  });

  readonly valueChange = output<string>();

  /** The sanitised HTML, as held. */
  protected readonly html = signal<string>('');

  private readonly region = viewChild<ElementRef<HTMLElement>>('region');

  protected readonly buttons = computed(() =>
    TOOLBAR.filter((button) => this.toolbar().includes(button.command)),
  );

  protected readonly isEmpty = computed(() => isRichTextEmpty(this.html()));

  protected readonly plainLength = computed(() => richTextToPlain(this.html()).length);

  protected readonly counterVisible = computed(() => {
    const limit = this.maxlength();
    return limit !== null && this.plainLength() >= limit * 0.8;
  });

  protected readonly editable = computed(() => !this.effectiveDisabled() && !this.readonly());

  /**
   * A `<label for>` associates with a form control, and a `contenteditable`
   * `<div>` is not one — so the region carries the name itself rather than
   * relying on the label element the wrapper draws.
   */
  protected readonly regionAriaLabel = computed(
    () => this.ariaLabel() || this.label() || 'Formatted text',
  );

  constructor() {
    super();

    // The region is uncontrolled — the browser owns its DOM while somebody
    // types — so a value arriving from the form has to be written into it. Only
    // when it differs, or the caret would jump to the start on every keystroke.
    effect(() => {
      const element = this.region()?.nativeElement;
      const value = this.html();
      if (element && element.innerHTML !== value) {
        element.innerHTML = value;
      }
    });
  }

  writeValue(value: unknown): void {
    const text = value === null || value === undefined ? '' : String(value);
    this.html.set(sanitizeRichText(text));
  }

  protected emitValue(value: string): void {
    this.valueChange.emit(value);
  }

  /**
   * What the region now contains, filtered.
   *
   * The filtered result is **not** written back into the element here: doing so
   * mid-edit resets the caret to the start of the field. The element is allowed
   * to hold whatever the browser produced until blur; what leaves this component
   * is always the sanitised form.
   */
  protected onRegionInput(): void {
    const element = this.region()?.nativeElement;
    if (!element) {
      return;
    }

    const clean = sanitizeRichText(element.innerHTML);
    this.html.set(clean);
    this.publish(clean);
  }

  /**
   * Blur is where the element and the value are reconciled.
   *
   * If the browser produced markup the allowlist dropped — a pasted `<table>`,
   * a `<font>` — the element still shows it until now. Writing the sanitised
   * value back on blur is what stops the screen and the stored value disagreeing
   * without moving the caret while somebody is typing.
   */
  protected onRegionBlur(): void {
    const element = this.region()?.nativeElement;
    if (element && element.innerHTML !== this.html()) {
      element.innerHTML = this.html();
    }
    this.onTouched();
  }

  /**
   * Paste is taken over so that what arrives is text and the formatting the
   * allowlist keeps — never a web page's markup, styles and scripts whole.
   */
  protected onPaste(event: ClipboardEvent): void {
    event.preventDefault();

    const clipboard = event.clipboardData;
    if (!clipboard) {
      return;
    }

    const html = clipboard.getData('text/html');
    const text = clipboard.getData('text/plain');

    if (html) {
      this.insertHtml(sanitizeRichText(html));
    } else if (text) {
      // Inserted as text, so `<` in a pasted code snippet stays a `<`.
      this.exec('insertText', text);
    }

    this.onRegionInput();
  }

  protected onCommand(button: ToolbarButton): void {
    if (!this.editable()) {
      return;
    }
    this.region()?.nativeElement.focus();
    this.exec(button.native);
    this.onRegionInput();
  }

  /** Whether the caret currently sits inside this formatting. */
  protected isActive(button: ToolbarButton): boolean {
    if (!this.editable()) {
      return false;
    }
    try {
      return document.queryCommandState(button.native);
    } catch {
      // Not every command answers `queryCommandState`, and a browser that
      // refuses is not a reason to fail rendering the toolbar.
      return false;
    }
  }

  private insertHtml(html: string): void {
    if (html) {
      this.exec('insertHTML', html);
    }
  }

  /**
   * `document.execCommand` is deprecated and has no replacement for applying
   * formatting to a selection. Wrapped so the deprecation is stated in exactly
   * one place, and so a browser that refuses a command does not throw into the
   * template.
   */
  private exec(command: string, argument?: string): void {
    try {
      document.execCommand(command, false, argument);
    } catch {
      // Nothing to do: the formatting simply does not apply.
    }
  }

  protected override fallbackAriaLabel(): string {
    return 'Formatted text';
  }

  protected idPrefix(): string {
    return 'bb-rich-text';
  }
}
