import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { CheckboxComponent } from '../forms/checkbox/checkbox.component';
import { SelectComponent } from '../forms/select/select.component';
import { MessageBoxComponent } from '../message-box/message-box.component';
import { UiMessage } from '../message-box/message-box.model';
import { NumberInputComponent } from '../number-input/number-input.component';
import { printHtml } from '../print-html';
import {
  LABEL_KINDS,
  LabelData,
  LabelKind,
  LabelTemplate,
  MAX_LABELS,
  PrintLabelsRequest,
  PrintLabelsResponse,
  sizeLabel,
  templatesFor,
  totalCopies,
} from './label-print.model';

/**
 * Prints labels at an exact size (TK-159): pick the kind and the size, set how
 * many of each, and the browser prints them through the label printer's own
 * driver at that size — or on A4 sticker sheets when there is no label printer.
 *
 * The page opening it supplies what goes on each label (the items, a goods
 * receipt's lines, a batch); Printing lays them out and never reads an item.
 * The size can be changed here to match the branch's own roll, and saved for
 * the next time.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-label-print-dialog',
  standalone: true,
  imports: [FormsModule, SelectComponent, CheckboxComponent, NumberInputComponent, MessageBoxComponent],
  templateUrl: './label-print.dialog.html',
  styleUrl: './label-print.dialog.scss',
})
export class LabelPrintDialog implements OnInit {
  private readonly http = inject(HttpClient);

  /** What to print, one entry per distinct label. Copies can be changed here. */
  readonly labels = input.required<LabelData[]>();

  /** The kind to start on: a price tag for items, batch and expiry for a batch. */
  readonly kind = input<LabelKind>('PriceTag');

  readonly closed = output<void>();

  protected readonly kindOptions = LABEL_KINDS.map((k) => ({ value: k.value, label: k.label }));
  protected readonly templates = signal<LabelTemplate[]>([]);
  protected readonly chosenKind = signal<LabelKind>('PriceTag');
  protected readonly chosenTemplateId = signal<number | null>(null);
  protected readonly sheet = signal(false);
  protected readonly rows = signal<LabelData[]>([]);
  protected readonly editingSize = signal(false);
  protected readonly draft = signal<LabelTemplate | null>(null);
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);

  protected readonly forKind = computed(() => templatesFor(this.templates(), this.chosenKind()));

  protected readonly templateOptions = computed(() =>
    this.forKind().map((t) => ({ value: t.labelTemplateId, label: `${t.name} (${sizeLabel(t)})` })),
  );

  protected readonly chosen = computed<LabelTemplate | null>(
    () => this.forKind().find((t) => t.labelTemplateId === this.chosenTemplateId()) ?? this.forKind()[0] ?? null,
  );

  protected readonly count = computed(() => totalCopies(this.rows()));
  protected readonly tooMany = computed(() => this.count() > MAX_LABELS);
  protected readonly maxLabels = MAX_LABELS;
  protected readonly sizeLabel = sizeLabel;

  ngOnInit(): void {
    this.chosenKind.set(this.kind());
    this.rows.set(this.labels().map((l) => ({ ...l, copies: Math.max(1, Math.floor(l.copies || 1)) })));
    void this.loadTemplates();
  }

  private async loadTemplates(): Promise<void> {
    try {
      this.templates.set(await firstValueFrom(this.http.get<LabelTemplate[]>('/api/print/labels/templates')));
    } catch (error) {
      this.fail(error, 'Could not load the label sizes; printing uses the standard size.');
    }
  }

  protected chooseKind(kind: LabelKind | null): void {
    this.chosenKind.set(kind ?? 'PriceTag');
    this.chosenTemplateId.set(null);
    this.editingSize.set(false);
  }

  protected setCopies(index: number, value: number | string | null): void {
    const copies = Math.max(1, Math.floor(Number(value) || 1));
    this.rows.update((rows) => rows.map((row, at) => (at === index ? { ...row, copies } : row)));
  }

  protected startEditSize(): void {
    const template = this.chosen();
    if (template) {
      this.draft.set({ ...template });
      this.editingSize.set(true);
    }
  }

  protected setDraft(field: 'widthMm' | 'heightMm' | 'gapMm' | 'columns' | 'columnGapMm', value: number | string | null): void {
    this.draft.update((d) => (d ? { ...d, [field]: Number(value) || 0 } : d));
  }

  /** Saves the size for the branch, so the next print starts on it. */
  protected async saveSize(): Promise<void> {
    const draft = this.draft();
    if (!draft) {
      return;
    }
    this.busy.set(true);
    try {
      await firstValueFrom(
        this.http.put(`/api/print/labels/templates/${draft.labelTemplateId}`, {
          name: draft.name,
          widthMm: draft.widthMm,
          heightMm: draft.heightMm,
          gapMm: draft.gapMm,
          columns: draft.columns,
          columnGapMm: draft.columnGapMm,
        }),
      );
      this.templates.update((list) => list.map((t) => (t.labelTemplateId === draft.labelTemplateId ? draft : t)));
      this.editingSize.set(false);
      this.messages.set([{ tone: 'success', text: `Saved: ${sizeLabel(draft)}.` }]);
    } catch (error) {
      this.fail(error, 'The size could not be saved.');
    } finally {
      this.busy.set(false);
    }
  }

  protected async print(): Promise<void> {
    if (this.tooMany() || this.rows().length === 0) {
      return;
    }
    const request: PrintLabelsRequest = {
      kind: this.chosenKind(),
      labelTemplateId: this.chosen()?.labelTemplateId ?? null,
      sheet: this.sheet(),
      labels: this.rows(),
    };

    this.busy.set(true);
    this.messages.set([]);
    try {
      const page = await firstValueFrom(this.http.post<PrintLabelsResponse>('/api/print/labels', request));
      printHtml(page.html);
      this.messages.set([{ tone: 'success', text: `${page.count} label${page.count === 1 ? '' : 's'} sent to the printer.` }]);
    } catch (error) {
      this.fail(error, 'The labels could not be printed.');
    } finally {
      this.busy.set(false);
    }
  }

  protected close(): void {
    this.closed.emit();
  }

  private fail(error: unknown, fallback: string): void {
    const message = (error as { error?: { message?: string } } | null)?.error?.message;
    this.messages.set([{ tone: 'error', text: message ?? fallback }]);
  }
}
