import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { FormatSettingsService } from '@bill-book/currency-format';
import { CustomerService } from '@bill-book/customer-core';
import {
  BbSelectOption,
  BranchDateInputComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  LookupDialogComponent,
  LookupRow,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
  TextareaComponent,
  UiMessage,
} from '@bill-book/ui-components';
import {
  INTERVAL_OPTIONS,
  SERVICE_PLANS_API,
  ServicePlan,
  ServicePlanDraft,
  cadence,
  dueLabel,
  dueTone,
  emptyPlan,
  planDraftOf,
  planProblem,
  planRequest,
} from './service-plans.model';

function today(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/**
 * Service plans (TK-140): each customer's recurring service, when it is next
 * due, and the two things staff do with one — record a service as done, which
 * moves the next due date on, and raise a visit, which opens a support ticket
 * (D-48). Customers are emailed ahead of each due date by the worker, worded
 * from the branch's "Service reminder" email template.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-service-plans',
  standalone: true,
  imports: [
    FormsModule,
    DataGridComponent,
    DataGridCellTemplateDirective,
    BranchDateInputComponent,
    LookupDialogComponent,
    MessageBoxComponent,
    NumberInputComponent,
    SelectComponent,
    TextInputComponent,
    TextareaComponent,
  ],
  templateUrl: './service-plans.page.html',
  styleUrl: './service-plans.page.scss',
})
export class ServicePlansPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly route = inject(ActivatedRoute);
  private readonly customers = inject(CustomerService);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly plans = signal<ServicePlan[]>([]);
  protected readonly loading = signal(false);
  protected readonly busy = signal(false);
  protected readonly messages = signal<UiMessage[]>([]);
  protected readonly selected = signal<ServicePlan | null>(null);
  protected readonly editing = signal(false);
  protected readonly contactLabel = signal('');

  protected filter: 'due' | 'Active' | 'Paused' | 'Ended' | '' = 'due';
  protected draft: ServicePlanDraft = emptyPlan(today());
  protected completedOn: string | null = today();

  protected readonly filterOptions: BbSelectOption<string>[] = [
    { value: 'due', label: 'Due in the next 30 days' },
    { value: 'Active', label: 'Active' },
    { value: 'Paused', label: 'Paused' },
    { value: 'Ended', label: 'Ended' },
    { value: '', label: 'Every plan' },
  ];
  protected readonly intervalOptions: BbSelectOption<string>[] = INTERVAL_OPTIONS.map((o) => ({ value: o.value, label: o.label }));

  protected readonly columns: ColumnDef[] = [
    { field: 'name', header: 'Plan' },
    { field: 'contactId', header: 'Customer', width: '7rem' },
    { field: 'interval', header: 'Repeats', isTemplate: true, width: '9rem' },
    { field: 'nextDueDate', header: 'Next service', isTemplate: true, width: '12rem' },
    { field: 'lastServicedOn', header: 'Last done', isTemplate: true, width: '8rem' },
    { field: 'status', header: 'Status', width: '6rem' },
  ];

  // The customer picker.
  protected readonly pickerOpen = signal(false);
  protected readonly pickerRows = signal<LookupRow[]>([]);
  protected readonly pickerLoading = signal(false);

  protected readonly cadence = cadence;
  protected readonly dueLabel = dueLabel;
  protected readonly dueTone = dueTone;

  ngOnInit(): void {
    void this.start();
  }

  private async start(): Promise<void> {
    await this.load();
    const open = Number(this.route.snapshot.queryParamMap.get('open'));
    const row = open ? this.plans().find((p) => p.servicePlanId === open) : undefined;
    if (row) {
      this.open(row);
    }
  }

  protected async load(): Promise<void> {
    this.loading.set(true);
    try {
      const query = this.filter === 'due' ? '?dueWithinDays=30' : this.filter ? `?status=${this.filter}` : '';
      this.plans.set(await firstValueFrom(this.http.get<ServicePlan[]>(`${SERVICE_PLANS_API}${query}`)));
    } catch (error) {
      this.fail(error, 'The service plans could not be loaded.');
    } finally {
      this.loading.set(false);
    }
  }

  protected create(): void {
    this.selected.set(null);
    this.draft = emptyPlan(today());
    this.contactLabel.set('');
    this.editing.set(true);
  }

  protected open(plan: ServicePlan): void {
    this.selected.set(plan);
    this.draft = planDraftOf(plan);
    this.contactLabel.set(`Customer ${plan.contactId}`);
    this.completedOn = today();
    this.editing.set(true);
  }

  protected async save(): Promise<void> {
    const problem = planProblem(this.draft);
    if (problem) {
      this.messages.set([{ tone: 'error', text: problem }]);
      return;
    }

    const current = this.selected();
    await this.run(async () => {
      const body = planRequest(this.draft);
      if (current) {
        await firstValueFrom(this.http.put(`${SERVICE_PLANS_API}/${current.servicePlanId}`, body));
      } else {
        await firstValueFrom(this.http.post(SERVICE_PLANS_API, body));
      }

      this.editing.set(false);
      this.selected.set(null);
    }, 'Plan saved.');
  }

  protected async complete(): Promise<void> {
    const current = this.selected();
    if (!current || !this.completedOn) return;
    await this.run(
      () => firstValueFrom(this.http.post(`${SERVICE_PLANS_API}/${current.servicePlanId}/complete`, { completedOn: this.completedOn })).then(() => undefined),
      'Service recorded. The next one is scheduled.',
    );
  }

  protected async raiseVisit(): Promise<void> {
    const current = this.selected();
    if (!current) return;
    await this.run(
      () => firstValueFrom(this.http.post(`${SERVICE_PLANS_API}/${current.servicePlanId}/visit`, {})).then(() => undefined),
      'A support ticket was raised for the visit.',
    );
  }

  protected async act(action: 'pause' | 'resume' | 'end'): Promise<void> {
    const current = this.selected();
    if (!current) return;
    await this.run(() => firstValueFrom(this.http.post(`${SERVICE_PLANS_API}/${current.servicePlanId}/${action}`, {})).then(() => undefined), 'Plan updated.');
  }

  protected openPicker(): void {
    this.pickerOpen.set(true);
    void this.search('');
  }

  protected async search(term: string): Promise<void> {
    this.pickerLoading.set(true);
    try {
      const contacts = await this.customers.searchContacts(term.trim());
      this.pickerRows.set(contacts.map((c) => ({ id: c.contactId, code: c.contactCode, name: c.displayName, meta: c.gstin })));
    } catch {
      this.pickerRows.set([]);
    } finally {
      this.pickerLoading.set(false);
    }
  }

  protected choose(row: LookupRow): void {
    this.draft.contactId = row.id;
    this.contactLabel.set(`${row.code} ${row.name}`.trim());
    this.pickerOpen.set(false);
  }

  protected date(value: string | null): string {
    return value ? this.formats.formatDate(value) : '—';
  }

  private async run(work: () => Promise<void>, success: string): Promise<void> {
    this.busy.set(true);
    this.messages.set([]);
    try {
      await work();
      this.messages.set([{ tone: 'success', text: success }]);
      const keep = this.selected()?.servicePlanId;
      await this.load();
      const fresh = keep ? this.plans().find((p) => p.servicePlanId === keep) : undefined;
      if (fresh) {
        this.open(fresh);
      }
    } catch (error) {
      this.fail(error, 'That could not be done.');
    } finally {
      this.busy.set(false);
    }
  }

  private fail(error: unknown, fallback: string): void {
    const text =
      error instanceof HttpErrorResponse && typeof error.error?.message === 'string' ? error.error.message : fallback;
    this.messages.set([{ tone: 'error', text }]);
  }
}
