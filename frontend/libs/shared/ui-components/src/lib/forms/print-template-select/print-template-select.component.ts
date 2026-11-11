import { ChangeDetectionStrategy, Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule, NgControl } from '@angular/forms';
import { DocumentChoicesService, PrintModule, PrintTemplateChoice } from '@bill-book/api-client';
import { BbControlBase } from '../control-base';
import { SelectComponent } from '../select/select.component';
import { NO_TEMPLATE_LABEL, pickDefaultTemplate, printTemplateOptions } from './print-template-field';

/**
 * The Print template field on a transaction's create and edit screen (TK-265).
 *
 * **It lists what the document's own module offers** (`GET /api/print-templates/choices/{module}/{type}`):
 * the branch's layouts for that type, the protected samples among them, the branch default first. That route
 * is guarded by the module's view permission, so a clerk who may raise an invoice sees the list without being
 * allowed into Settings. The forms used to read it from the Settings route instead, which refused the Sales
 * and Accountant roles, swallowed the refusal and saved the document with no template at all; so it prints
 * with the branch default, which is the seeded sample until somebody presses Set default on their own.
 *
 * **A new document starts on the branch default** (`isNew`), so the template is chosen and saved rather than
 * left to a fallback. That is a starting value, not an edit: the form is left pristine, so opening New invoice
 * and leaving again does not ask whether to discard changes. An edit screen is never changed behind the
 * person's back; a document that names no template shows "Branch default".
 *
 * **A template the document names that is no longer offered stays visible**, disabled and labelled as gone,
 * and a list that cannot load says so under the field. Neither is a blank that looks like "none".
 *
 * A normal form control: `formControlName`, `ngModel` or `[value]` and `(valueChange)`. The value is the
 * template's id, or null for the branch default.
 */
@Component({
  selector: 'bb-print-template-select',
  standalone: true,
  imports: [FormsModule, SelectComponent],
  templateUrl: './print-template-select.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PrintTemplateSelectComponent extends BbControlBase<number | null> {
  private readonly choicesApi = inject(DocumentChoicesService);

  /**
   * Taken from the injector rather than provided as `NG_VALUE_ACCESSOR`, because taking the branch default
   * must not mark the form dirty and only the control can say it is pristine again.
   */
  private readonly ngControl = inject(NgControl, { optional: true, self: true });

  /** The module whose route answers: sales, purchase, banking (receipts and payments) or accounting (journals). */
  readonly module = input.required<PrintModule>();

  /** The three-letter transaction type: INV, QTE, BIL, RCM, JRN. */
  readonly documentType = input.required<string>();

  /** A document being raised, which starts on the branch default; an edit is left as it is. */
  readonly isNew = input<boolean, boolean | string>(false, {
    transform: (value) => value === '' || value === 'true' || value === true,
  });

  readonly valueChange = output<number | null>();

  protected readonly placeholder = NO_TEMPLATE_LABEL;

  protected readonly selected = signal<number | null>(null);
  protected readonly choices = signal<readonly PrintTemplateChoice[]>([]);
  protected readonly loading = signal(true);
  protected readonly loaded = signal(false);
  protected readonly loadError = signal('');

  protected readonly options = computed(() => printTemplateOptions(this.choices(), this.selected(), this.loaded()));
  protected readonly shownError = computed(() => this.error() || this.loadError());
  protected readonly shownHint = computed(() => this.hint() || 'The layout this document prints with.');

  constructor() {
    super();

    if (this.ngControl) {
      this.ngControl.valueAccessor = this;
    }

    // Loads again if the module or the type ever changes; the screens fix both, so in practice once.
    effect(() => {
      const module = this.module();
      const type = this.documentType();
      untracked(() => void this.load(module, type));
    });
  }

  writeValue(value: unknown): void {
    const id = value === null || value === undefined || value === '' ? null : Number(value);
    const next = id !== null && Number.isFinite(id) ? id : null;
    this.selected.set(next);

    // A form reset to a new document comes back to the default, after the reset has finished.
    if (next === null) {
      queueMicrotask(() => this.takeDefaultIfNew());
    }
  }

  protected emitValue(value: number | null): void {
    this.valueChange.emit(value);
  }

  protected idPrefix(): string {
    return 'bb-print-template';
  }

  protected choose(value: number | null): void {
    this.selected.set(value);
    this.publish(value);
    this.onTouched();
  }

  private async load(module: PrintModule, type: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set('');

    try {
      this.choices.set(await this.choicesApi.printTemplates(module, type));
      this.loaded.set(true);
    } catch {
      this.choices.set([]);
      this.loaded.set(false);
      this.loadError.set('The print templates could not be loaded. This document will print with the branch default.');
    } finally {
      this.loading.set(false);
    }

    this.takeDefaultIfNew();
  }

  /** A new document with nothing chosen starts on the branch's default, without making the form dirty. */
  private takeDefaultIfNew(): void {
    if (!this.isNew() || this.selected() !== null) {
      return;
    }

    const id = pickDefaultTemplate(this.choices());
    if (id === null) {
      return;
    }

    this.selected.set(id);
    this.publish(id);
    this.ngControl?.control?.markAsPristine();
  }
}
