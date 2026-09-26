import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SessionContextService } from '@bill-book/auth';
import { readApiFailure } from '@bill-book/api-client';
import { FormatSettingsService } from '@bill-book/currency-format';
import {
  BbSelectOption,
  CheckboxComponent,
  DateInputComponent,
  NumberInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { ProjectListItem } from './project-options';

interface ProjectTask {
  projectTaskId: number | null;
  taskName: string;
  hourlyRate: number | null;
  budgetHours: number | null;
  isBillable: boolean;
  isActive: boolean;
}

interface ProjectMilestone {
  projectMilestoneId: number | null;
  name: string;
  amount: number | null;
  dueDate: string | null;
  invoiceId?: number | null;
}

interface ProjectView extends ProjectListItem {
  description: string | null;
  rateBasis: string | null;
  hourlyRate: number | null;
  fixedFee: number | null;
  tasks: ProjectTask[];
  members: { userId: string; hourlyRate: number | null; costRate: number | null }[];
  milestones: ProjectMilestone[];
}

interface ProjectForm {
  projectName: string;
  description: string;
  contactId: number | null;
  billingMethod: string;
  rateBasis: string;
  hourlyRate: number | null;
  fixedFee: number | null;
  budgetAmount: number | null;
  startDate: string | null;
  endDate: string | null;
  status: string;
}

interface LedgerRow {
  ledgerId: number;
  ledgerDate: string;
  accountCode: string;
  accountName: string;
  documentNo: string | null;
  transactionTypeCode: string;
  transactionId: number;
  description: string | null;
  debit: number;
  credit: number;
}

interface ContactOption {
  contactId: number;
  displayName: string;
}

/**
 * Accounting › Projects (TK-104). The project master — a job with its tasks
 * and milestones — and the ledger rows tagged with it, which is what the job
 * earned and cost. A project is never deleted: complete or cancel it, and it
 * takes no more postings.
 */
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-projects-page',
  standalone: true,
  imports: [FormsModule, TextInputComponent, SelectComponent, NumberInputComponent, DateInputComponent, CheckboxComponent],
  templateUrl: './projects.page.html',
  styleUrl: './projects.page.scss',
})
export class ProjectsPage implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly session = inject(SessionContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly formats = inject(FormatSettingsService);

  protected readonly billingOptions: BbSelectOption<string>[] = [
    { value: 'TimeAndMaterials', label: 'Time and materials' },
    { value: 'FixedFee', label: 'Fixed fee' },
    { value: 'NonBillable', label: 'Non-billable (internal)' },
  ];

  protected readonly rateOptions: BbSelectOption<string>[] = [
    { value: 'ProjectRate', label: 'One rate for the project' },
    { value: 'TaskRate', label: 'A rate per task' },
    { value: 'UserRate', label: 'A rate per person' },
  ];

  protected readonly statusOptions: BbSelectOption<string>[] = [
    { value: 'Active', label: 'Active' },
    { value: 'OnHold', label: 'On hold' },
    { value: 'Completed', label: 'Completed' },
    { value: 'Cancelled', label: 'Cancelled' },
  ];

  protected readonly canEdit = computed(() => this.session.has('projects.edit') || this.session.has('projects.create'));
  protected readonly rows = signal<ProjectListItem[]>([]);
  protected readonly contactOptions = signal<BbSelectOption<number>[]>([]);
  protected readonly editing = signal(false);
  protected readonly tasks = signal<ProjectTask[]>([]);
  protected readonly milestones = signal<ProjectMilestone[]>([]);
  protected readonly ledger = signal<LedgerRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly messageIsError = signal(false);
  protected readonly ledgerNet = computed(() => this.ledger().reduce((sum, r) => sum + r.debit - r.credit, 0));

  protected editingId: number | null = null;
  protected projectCode = '';
  protected statusFilter = '';
  protected form: ProjectForm = this.blankForm();
  private members: ProjectView['members'] = [];

  ngOnInit(): void {
    const routed = Number(this.route.snapshot.paramMap.get('projectId'));
    void this.load(Number.isFinite(routed) && routed > 0 ? routed : null);
  }

  protected statusLabel(status: string): string {
    return this.statusOptions.find((o) => o.value === status)?.label ?? status;
  }

  protected billingLabel(method: string): string {
    return this.billingOptions.find((o) => o.value === method)?.label ?? method;
  }

  protected async load(openId: number | null = null): Promise<void> {
    this.busy.set(true);
    try {
      const query = this.statusFilter ? `?status=${this.statusFilter}` : '';
      this.rows.set(await firstValueFrom(this.http.get<ProjectListItem[]>(`/api/projects${query}`)));

      if (this.contactOptions().length === 0) {
        const contacts = await firstValueFrom(this.http.get<ContactOption[]>('/api/contacts')).catch(() => [] as ContactOption[]);
        this.contactOptions.set(contacts.map((c) => ({ value: c.contactId, label: c.displayName })));
      }

      if (openId !== null) {
        await this.open(openId);
      }
    } catch (error) {
      this.show(readApiFailure(error).text, true);
    } finally {
      this.busy.set(false);
    }
  }

  protected startNew(): void {
    this.editingId = null;
    this.projectCode = '';
    this.form = this.blankForm();
    this.tasks.set([]);
    this.milestones.set([]);
    this.members = [];
    this.ledger.set([]);
    this.editing.set(true);
    this.message.set(null);
  }

  protected async open(projectId: number): Promise<void> {
    const project = await firstValueFrom(this.http.get<ProjectView>(`/api/projects/${projectId}`));
    this.editingId = project.projectId;
    this.projectCode = project.projectCode;
    this.form = {
      projectName: project.projectName,
      description: project.description ?? '',
      contactId: project.contactId,
      billingMethod: project.billingMethod,
      rateBasis: project.rateBasis ?? 'ProjectRate',
      hourlyRate: project.hourlyRate,
      fixedFee: project.fixedFee,
      budgetAmount: project.budgetAmount,
      startDate: project.startDate,
      endDate: project.endDate,
      status: project.status,
    };
    this.tasks.set(project.tasks.map((t) => ({ ...t })));
    this.milestones.set(project.milestones.map((m) => ({ ...m })));
    this.members = project.members;
    this.ledger.set(await firstValueFrom(this.http.get<LedgerRow[]>(`/api/projects/${projectId}/ledger`)).catch(() => []));
    this.editing.set(true);
    this.message.set(null);
  }

  protected close(): void {
    this.editing.set(false);
    this.editingId = null;
    void this.router.navigate(['/accounting/projects'], { replaceUrl: true });
  }

  protected addTask(): void {
    this.tasks.update((tasks) => [
      ...tasks,
      { projectTaskId: null, taskName: '', hourlyRate: null, budgetHours: null, isBillable: true, isActive: true },
    ]);
  }

  protected removeTask(index: number): void {
    this.tasks.update((tasks) => tasks.filter((_, i) => i !== index));
  }

  protected addMilestone(): void {
    this.milestones.update((milestones) => [...milestones, { projectMilestoneId: null, name: '', amount: null, dueDate: null }]);
  }

  protected removeMilestone(index: number): void {
    this.milestones.update((milestones) => milestones.filter((_, i) => i !== index));
  }

  /** Re-publishes the arrays after ngModel has written into one of their rows in place. */
  protected touch(): void {
    this.tasks.update((tasks) => [...tasks]);
    this.milestones.update((milestones) => [...milestones]);
  }

  protected async save(): Promise<void> {
    if (!this.form.projectName.trim()) {
      this.show('Give the project a name.', true);
      return;
    }

    const body = {
      ...this.form,
      description: this.form.description || null,
      rateBasis: this.form.billingMethod === 'TimeAndMaterials' ? this.form.rateBasis : null,
      tasks: this.tasks().filter((t) => t.taskName.trim()),
      members: this.members,
      milestones: this.milestones()
        .filter((m) => m.name.trim())
        .map((m) => ({ projectMilestoneId: m.projectMilestoneId, name: m.name, amount: m.amount ?? 0, dueDate: m.dueDate })),
    };

    this.busy.set(true);
    try {
      let id = this.editingId;
      if (id === null) {
        const created = await firstValueFrom(this.http.post<{ projectId: number }>('/api/projects', body));
        id = created.projectId;
        await this.router.navigate(['/accounting/projects', id], { replaceUrl: true });
      } else {
        await firstValueFrom(this.http.put(`/api/projects/${id}`, body));
      }

      await this.load(id);
      this.show('Project saved.', false);
    } catch (error) {
      this.show(readApiFailure(error).text, true);
    } finally {
      this.busy.set(false);
    }
  }

  private blankForm(): ProjectForm {
    return {
      projectName: '',
      description: '',
      contactId: null,
      billingMethod: 'TimeAndMaterials',
      rateBasis: 'ProjectRate',
      hourlyRate: null,
      fixedFee: null,
      budgetAmount: null,
      startDate: null,
      endDate: null,
      status: 'Active',
    };
  }

  private show(text: string, isError: boolean): void {
    this.message.set(text);
    this.messageIsError.set(isError);
  }
}
