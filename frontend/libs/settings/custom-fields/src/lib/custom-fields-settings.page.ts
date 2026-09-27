import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { SessionContextService } from '@bill-book/auth';
import { readApiFailure } from '@bill-book/api-client';
import {
  BbSelectOption,
  CheckboxComponent,
  CustomFieldDefinition,
  CustomFieldValues,
  CustomFieldsComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  defaultValues,
} from '@bill-book/ui-components';
import {
  CustomFieldForm,
  DATA_TYPES,
  ENTITY_KINDS,
  MAX_ACTIVE_PER_KIND,
  blankForm,
  formOf,
  formProblem,
  hasOptions,
  isText,
  keyFromLabel,
  requestOf,
} from './custom-fields-settings.model';

/**
 * Settings › Custom fields (TK-110): a branch's own fields on each kind of
 * record, in one list. A field's key, kind and type are fixed once it is
 * saved; its label, options, order and flags can change. A field is never
 * deleted: deactivating it keeps every value stored under it. An option is
 * deactivated rather than removed, for the same reason.
 *
 * Reading needs `settings.view`; saving needs `settings.edit`, which the API
 * checks regardless.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-custom-fields-settings-page',
  standalone: true,
  imports: [FormsModule, TextInputComponent, SelectComponent, NumberInputComponent, CheckboxComponent, CustomFieldsComponent],
  templateUrl: './custom-fields-settings.page.html',
  styleUrl: './custom-fields-settings.page.scss',
})
export class CustomFieldsSettingsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionContextService);

  protected readonly kindOptions: BbSelectOption<string>[] = ENTITY_KINDS;
  protected readonly typeOptions: BbSelectOption<string>[] = DATA_TYPES;
  protected readonly maxActive = MAX_ACTIVE_PER_KIND;

  protected readonly canEdit = computed(() => this.session.has('settings.edit'));
  protected readonly kind = signal('Contact');
  protected readonly all = signal<CustomFieldDefinition[]>([]);
  protected readonly fields = computed(() => this.all().filter((f) => f.entityKind === this.kind()));
  protected readonly activeCount = computed(() => this.fields().filter((f) => f.isActive).length);
  protected readonly editing = signal(false);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly preview = signal<CustomFieldValues>({});

  protected editingId: number | null = null;
  protected form: CustomFieldForm = blankForm('Contact', 0);
  private keyTouched = false;

  ngOnInit(): void {
    void this.load();
  }

  protected kindLabel(value: string): string {
    return ENTITY_KINDS.find((k) => k.value === value)?.label ?? value;
  }

  protected typeLabel(value: string): string {
    return DATA_TYPES.find((t) => t.value === value)?.label ?? value;
  }

  protected hasOptions = hasOptions;
  protected isText = isText;

  protected setKind(value: string | null): void {
    this.kind.set(value ?? 'Contact');
    this.editing.set(false);
    this.preview.set(defaultValues(this.fields()));
  }

  protected startNew(): void {
    this.editingId = null;
    this.keyTouched = false;
    this.form = blankForm(this.kind(), (this.fields().at(-1)?.displayOrder ?? 0) + 10);
    this.message.set(null);
    this.editing.set(true);
  }

  protected edit(field: CustomFieldDefinition): void {
    this.editingId = field.customFieldDefinitionId;
    this.keyTouched = true;
    this.form = formOf(field);
    this.message.set(null);
    this.editing.set(true);
  }

  protected cancel(): void {
    this.editing.set(false);
  }

  protected onLabel(): void {
    if (this.editingId === null && !this.keyTouched) {
      this.form.key = keyFromLabel(this.form.label);
    }
  }

  protected onKey(): void {
    this.keyTouched = true;
  }

  protected addOption(): void {
    this.form.options = [...this.form.options, { value: '', label: '', active: true, saved: false }];
  }

  /** Only an option not yet saved can be removed; a saved one is deactivated, because records may hold it. */
  protected removeOption(index: number): void {
    this.form.options = this.form.options.filter((_, i) => i !== index);
  }

  protected async save(): Promise<void> {
    const problem = formProblem(this.form);
    if (problem) {
      this.fail(problem);
      return;
    }

    this.busy.set(true);
    try {
      const body = requestOf(this.form);
      if (this.editingId === null) {
        await firstValueFrom(this.http.post('/api/custom-fields', body));
      } else {
        await firstValueFrom(this.http.put(`/api/custom-fields/${this.editingId}`, body));
      }
      this.editing.set(false);
      this.ok('Field saved.');
      await this.load();
    } catch (err) {
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    this.busy.set(true);
    try {
      this.all.set(await firstValueFrom(this.http.get<CustomFieldDefinition[]>('/api/custom-fields')));
      this.preview.set(defaultValues(this.fields()));
    } catch (err) {
      this.fail(readApiFailure(err).text);
    } finally {
      this.busy.set(false);
    }
  }

  private ok(text: string): void {
    this.message.set(text);
    this.messageIsError.set(false);
  }

  private fail(text: string): void {
    this.message.set(text);
    this.messageIsError.set(true);
  }
}
