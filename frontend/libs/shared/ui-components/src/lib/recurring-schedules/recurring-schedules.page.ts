import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FormatSettingsService } from '@bill-book/currency-format';
import { DataGridComponent } from '../data-grid/data-grid.component';
import { DataGridCellTemplateDirective } from '../data-grid/data-grid-cell-template.directive';
import { ColumnDef } from '../data-grid/data-grid.models';
import { BranchDateInputComponent } from '../branch-date-input/branch-date-input.component';
import { todayIso } from '../branch-date-input/calendar';
import { SelectComponent } from '../forms/select/select.component';
import { CheckboxComponent } from '../forms/checkbox/checkbox.component';
import { BbSelectOption } from '../forms/field.model';
import { TextInputComponent } from '../text-input/text-input.component';
import { NumberInputComponent } from '../number-input/number-input.component';
import { MessageBoxComponent } from '../message-box/message-box.component';
import { UiMessage } from '../message-box/message-box.model';
import { onCreateRequest } from '../register/create-request';
import {
  FREQUENCY_OPTIONS,
  RECURRING_KINDS,
  RecurringDraft,
  RecurringKind,
  RecurringSchedule,
  TemplateOption,
  cadence,
  draftOf,
  draftProblem,
  emptyDraft,
  requestOf,
  runDocumentId,
  scheduleId,
  statusTag,
  templateOptions,
} from './recurring-schedules.model';

/**
 * Recurring invoices and recurring bills (TK-139), one page for both. The
 * route says which: `data: { recurringKind: 'invoice' | 'bill' }`.
 *
 * A schedule names an existing document as its template; the worker copies it
 * on each occurrence, dated that day, at that day's tax rates. The list is on
 * top and the form below it, so at ~360px the two simply stack.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-recurring-schedules',
  standalone: true,
  imports: [
    FormsModule,
    RouterModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    BranchDateInputComponent,
    SelectComponent,
    CheckboxComponent,
    TextInputComponent,
    NumberInputComponent,
    MessageBoxComponent,
  ],
  templateUrl: './recurring-schedules.page.html',
  styleUrl: './recurring-schedules.page.scss',
})
export class RecurringSchedulesPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly kind: RecurringKind =
    this.route.snapshot.data['recurringKind'] === 'bill' ? 'bill' : 'invoice';
  protected readonly config = RECURRING_KINDS[this.kind];

  protected readonly rows = signal<RecurringSchedule[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly selected = signal<RecurringSchedule | null>(null);
  protected readonly editing = signal(false);
  protected readonly templates = signal<TemplateOption[]>([]);

  /** Bound with ngModel, so a plain field. */
  protected draft: RecurringDraft = emptyDraft(todayIso());

  protected readonly frequencyOptions: BbSelectOption<string>[] = FREQUENCY_OPTIONS.map((f) => ({ value: f.value, label: f.label }));

  protected readonly templateChoices = computed<BbSelectOption<number>[]>(() =>
    this.templates().map((t) => ({
      value: t.id,
      label: `${t.documentNo}${t.contactName ? ' · ' + t.contactName : ''} · ${this.formats.formatMoney(t.totalAmount)}`,
    })),
  );

  protected readonly columns: ColumnDef[] = [
    { field: 'name', header: 'Name' },
    { field: 'templateDocumentNo', header: 'Copies', width: '9rem' },
    { field: 'frequency', header: 'Repeats', isTemplate: true, width: '9rem' },
    { field: 'nextRunDate', header: 'Next', isTemplate: true, width: '8rem' },
    { field: 'occurrencesCreated', header: 'Raised', align: 'right', width: '5rem' },
    { field: 'templateTotal', header: 'Amount', align: 'right', dataType: 'money', width: '8rem' },
    { field: 'status', header: 'Status', isTemplate: true, width: '7rem' },
  ];

  protected readonly cadence = cadence;
  protected readonly statusTag = statusTag;
  protected readonly runDocumentId = runDocumentId;

  /** The template, start and frequency lock once something has been raised. */
  protected readonly locked = computed(() => (this.selected()?.occurrencesCreated ?? 0) > 0);

  constructor() {
    // The secondary menu's + asks for a new schedule with ?action=create (TK-189).
    onCreateRequest(() => this.create());
  }

  ngOnInit(): void {
    void this.load();
    void this.loadTemplates();
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      this.rows.set(await firstValueFrom(this.http.get<RecurringSchedule[]>(this.config.base)));
    } catch (error) {
      this.fail(error, `The ${this.config.title.toLowerCase()} could not be loaded.`);
    } finally {
      this.loading.set(false);
    }
  }

  private async loadTemplates(): Promise<void> {
    try {
      const url = this.kind === 'invoice' ? '/api/sales/invoices?take=200' : '/api/purchase/bills';
      this.templates.set(templateOptions(await firstValueFrom(this.http.get<unknown>(url)), this.kind));
    } catch {
      // The form still opens; the choice list is just empty and says so.
      this.templates.set([]);
    }
  }

  protected create(): void {
    this.selected.set(null);
    this.draft = emptyDraft(todayIso());
    this.messages.set([]);
    this.editing.set(true);
  }

  protected async open(row: RecurringSchedule): Promise<void> {
    this.messages.set([]);
    try {
      const full = await firstValueFrom(this.http.get<RecurringSchedule>(`${this.config.base}/${scheduleId(row)}`));
      this.selected.set(full);
      this.draft = draftOf(full);
      this.editing.set(true);
    } catch (error) {
      this.fail(error, 'The schedule could not be opened.');
    }
  }

  protected close(): void {
    this.editing.set(false);
    this.selected.set(null);
  }

  protected async save(): Promise<void> {
    const problem = draftProblem(this.draft, this.kind);
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    this.saving.set(true);
    try {
      const body = requestOf(this.draft, this.kind);
      const current = this.selected();
      if (current) {
        await firstValueFrom(this.http.put(`${this.config.base}/${scheduleId(current)}`, body));
      } else {
        await firstValueFrom(this.http.post(this.config.base, body));
      }

      this.messages.set([{ tone: 'success', text: 'Schedule saved.' }]);
      this.close();
      await this.load();
    } catch (error) {
      this.fail(error, 'The schedule could not be saved.');
    } finally {
      this.saving.set(false);
    }
  }

  protected async act(action: 'pause' | 'resume' | 'end'): Promise<void> {
    const current = this.selected();
    if (!current) return;

    this.saving.set(true);
    try {
      await firstValueFrom(this.http.post(`${this.config.base}/${scheduleId(current)}/${action}`, {}));
      await this.load();
      await this.open(current);
    } catch (error) {
      this.fail(error, 'The schedule could not be changed.');
    } finally {
      this.saving.set(false);
    }
  }

  protected date(value: string | null): string {
    return value ? this.formats.formatDate(value) : '—';
  }

  private fail(error: unknown, fallback: string): void {
    const text =
      error instanceof HttpErrorResponse && typeof error.error?.message === 'string' ? error.error.message : fallback;
    this.messages.set([{ tone: 'error', text }]);
  }
}
