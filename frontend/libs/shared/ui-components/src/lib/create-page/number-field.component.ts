import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { DocumentChoicesService, NumberedModule } from '@bill-book/api-client';
import { numberText } from './create-page.model';

/**
 * A create page's read-only **Number** (TK-192, Shell.dc.html 731–735).
 *
 * A saved document shows its own `number`. A new one shows the number its
 * series would give next, read through the module's own choices route — only a
 * preview, since the number is allocated when the document is saved and someone
 * saving first takes it — or "Assigned on save" when the branch has no series
 * for it or the preview cannot be read.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-number-field',
  standalone: true,
  templateUrl: './number-field.component.html',
  styleUrl: './number-field.component.scss',
})
export class NumberFieldComponent {
  private readonly choices = inject(DocumentChoicesService);

  readonly label = input<string>('Number');
  readonly fieldId = input<string>('create-number');

  /** The document's own number, once it has one. */
  readonly number = input<string | null>(null);

  /** The module and series to preview from, for a new document. */
  readonly module = input<NumberedModule | null>(null);
  readonly series = input<string | null>(null);

  /** The document date; the series may restart with the financial year. */
  readonly date = input<string | null>(null);

  protected readonly preview = signal<string | null>(null);
  protected readonly loading = signal(false);

  protected readonly shown = computed(() => numberText(this.number(), this.loading(), this.preview()));

  /** The master's note, under a previewed number only. */
  protected readonly isPreview = computed(() => !this.number() && !!this.preview());

  /** Answers that arrive out of order — a date changed twice — are dropped. */
  private sequence = 0;

  constructor() {
    effect(() => {
      const own = this.number();
      const module = this.module();
      const series = this.series();
      const date = this.date();
      untracked(() => {
        if (own || !module || !series) {
          this.sequence++;
          this.preview.set(null);
          this.loading.set(false);
          return;
        }
        void this.load(module, series, date);
      });
    });
  }

  private async load(module: NumberedModule, series: string, date: string | null): Promise<void> {
    const mine = ++this.sequence;
    this.loading.set(true);
    try {
      const next = await this.choices.nextNumber(module, series, date);
      if (mine === this.sequence) this.preview.set(next.number);
    } catch {
      if (mine === this.sequence) this.preview.set(null);
    } finally {
      if (mine === this.sequence) this.loading.set(false);
    }
  }
}
