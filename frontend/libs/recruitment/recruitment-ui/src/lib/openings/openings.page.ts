import { CommonModule } from '@angular/common';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  SelectComponent,
  TextInputComponent,
  TextareaComponent,
} from '@bill-book/ui-components';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CreateJobOpening,
  JobOpeningListItem,
  JobRequisitionListItem,
  OpeningStatus,
  RecruitmentApiService,
} from '@bill-book/recruitment-core';

@Component({
  selector: 'rec-openings-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MessageBoxComponent,
    SelectComponent,
    TextInputComponent,
    TextareaComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
  ],
  templateUrl: './openings.page.html',
  styleUrl: '../recruitment-page.scss',
})
export class OpeningsPage implements OnInit {
  protected readonly openingsColumns: ColumnDef[] = [
    { field: 'title', header: 'Title' },
    { field: 'requisitionCode', header: 'Requisition' },
    { field: 'openingStatus', header: 'Status' },
    { field: 'applicationsCount', header: 'Applications' },
    { field: 'publishedDate', header: 'Published Date' },
    { field: 'col5', header: 'Actions', sortable: false, filterable: false },
  ];

  protected readonly jobRequisitionOptions = computed(() =>
    this.requisitions().map((req) => ({ value: req.jobRequisitionId, label: `${req.requisitionCode} (${req.employmentType} - ${req.openings} openings)` })),
  );

  protected readonly openingStatusOptions = [
    { value: 'Draft', label: 'Draft' },
    { value: 'Open', label: 'Open' },
    { value: 'OnHold', label: 'On Hold' },
  ];

  private readonly api = inject(RecruitmentApiService);

  readonly openings = signal<JobOpeningListItem[]>([]);
  readonly requisitions = signal<JobRequisitionListItem[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = signal<CreateJobOpening>({
    jobRequisitionId: 0,
    title: '',
    description: '',
    openingStatus: 'Open',
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const [ops, reqs] = await Promise.all([
        this.api.openings(),
        this.api.requisitions(),
      ]);
      this.openings.set(ops);
      this.requisitions.set(reqs);
      if (reqs.length > 0 && this.form().jobRequisitionId === 0) {
        this.form.update(f => ({ ...f, jobRequisitionId: reqs[0].jobRequisitionId }));
      }
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load openings');
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    const firstReqId = this.requisitions().length > 0 ? this.requisitions()[0].jobRequisitionId : 0;
    this.form.set({
      jobRequisitionId: firstReqId,
      title: '',
      description: '',
      openingStatus: 'Open',
    });
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
  }

  async save(): Promise<void> {
    const data = this.form();
    if (!data.title.trim()) {
      this.errorMessage.set('Title is required.');
      return;
    }
    if (!data.description.trim()) {
      this.errorMessage.set('Description is required.');
      return;
    }

    this.loading.set(true);
    try {
      await this.api.createOpening(data);
      this.showForm.set(false);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save opening');
    } finally {
      this.loading.set(false);
    }
  }

  async changeStatus(id: number, status: OpeningStatus): Promise<void> {
    const op = this.openings().find(o => o.jobOpeningId === id);
    if (!op) return;

    try {
      await this.api.updateOpening(id, {
        jobRequisitionId: op.jobRequisitionId,
        title: op.title,
        description: op.description,
        openingStatus: status,
      });
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Status update failed');
    }
  }
}
