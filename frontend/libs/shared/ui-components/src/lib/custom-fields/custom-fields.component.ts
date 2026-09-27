import { ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { CustomFieldDefinition, CustomFieldValues, offeredOptions, visibleFields } from './custom-fields.model';

/**
 * A record's custom fields as a form section (TK-110): one input per field,
 * chosen by its type, bound two-way to the record's values. Owning pages put
 * it on their form; the server validates what it sends.
 *
 * A deactivated field still shows while the record holds a value for it, read
 * only, so nothing stored disappears.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-custom-fields',
  standalone: true,
  templateUrl: './custom-fields.component.html',
  styleUrl: './custom-fields.component.scss',
})
export class CustomFieldsComponent {
  readonly definitions = input<readonly CustomFieldDefinition[]>([]);
  readonly value = model<CustomFieldValues>({});
  readonly disabled = input(false);

  protected readonly fields = computed(() => visibleFields(this.definitions(), this.value()));

  protected current(key: string): unknown {
    return this.value()[key];
  }

  protected text(key: string): string {
    const v = this.value()[key];
    return v === null || v === undefined ? '' : String(v);
  }

  protected checked(key: string): boolean {
    return this.value()[key] === true;
  }

  protected isChosen(key: string, option: string): boolean {
    const v = this.value()[key];
    return Array.isArray(v) && v.includes(option);
  }

  protected options(field: CustomFieldDefinition) {
    return offeredOptions(field, this.value()[field.key]);
  }

  protected setText(key: string, event: Event): void {
    this.set(key, (event.target as HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement).value);
  }

  protected setNumber(key: string, event: Event): void {
    const raw = (event.target as HTMLInputElement).value;
    this.set(key, raw === '' ? null : Number(raw));
  }

  protected setBoolean(key: string, event: Event): void {
    this.set(key, (event.target as HTMLInputElement).checked);
  }

  protected toggle(key: string, option: string, event: Event): void {
    const on = (event.target as HTMLInputElement).checked;
    const v = this.value()[key];
    const list = Array.isArray(v) ? v.map(String) : [];
    this.set(key, on ? [...list.filter((x) => x !== option), option] : list.filter((x) => x !== option));
  }

  private set(key: string, next: unknown): void {
    this.value.update((values) => ({ ...values, [key]: next === '' ? null : next }));
  }
}
