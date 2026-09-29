import { ChangeDetectionStrategy, Component, OnInit, effect, inject, input, model, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CustomFieldsComponent } from './custom-fields.component';
import { CustomFieldDefinition, CustomFieldValues, defaultValues, isEmptyValue } from './custom-fields.model';

/**
 * A document's custom fields (TK-112): loads its kind's definitions from the
 * owning service (`/api/sales/custom-fields?kind=Invoice`) and edits the
 * values two-way. On a new document with nothing typed yet it fills in each
 * field's default. Values carried from a source document are added by the
 * server when the document is created.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-document-custom-fields',
  standalone: true,
  imports: [CustomFieldsComponent],
  template: `
    @if (definitions().length > 0) {
      <section class="document-custom-fields">
        <h3>More details</h3>
        <bb-custom-fields [definitions]="definitions()" [(value)]="value" [disabled]="disabled()" />
      </section>
    }
  `,
  styles: [
    `
      .document-custom-fields { margin-top: 0.75rem; }
      h3 { font-size: 0.95rem; margin: 0 0 0.5rem; }
    `,
  ],
})
export class DocumentCustomFieldsComponent implements OnInit {
  private readonly http = inject(HttpClient);

  /** The owning service's definitions route for this kind, e.g. `/api/purchase/custom-fields?kind=Bill`. */
  readonly source = input.required<string>();
  readonly value = model<CustomFieldValues>({});
  readonly disabled = input(false);
  readonly isNew = input(false);

  protected readonly definitions = signal<CustomFieldDefinition[]>([]);

  constructor() {
    effect(() => {
      const defs = this.definitions();
      if (this.isNew() && defs.length > 0 && Object.values(this.value()).every(isEmptyValue)) {
        const defaults = defaultValues(defs);
        if (Object.keys(defaults).length > 0) {
          this.value.set(defaults);
        }
      }
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    try {
      this.definitions.set(await firstValueFrom(this.http.get<CustomFieldDefinition[]>(this.source())));
    } catch {
      // No custom fields shown when they cannot be read; the server still checks them on save.
      this.definitions.set([]);
    }
  }
}
