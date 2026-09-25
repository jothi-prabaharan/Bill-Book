import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CreateJobRequisition,
  JobRequisitionListItem,
  RecruitmentApiService,
} from '@bill-book/recruitment-core';

@Component({
  selector: 'rec-requisitions-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './requisitions.page.html',
  styleUrl: '../recruitment-page.scss',
})
export class RequisitionsPage implements OnInit {
  private readonly api = inject(RecruitmentApiService);

  readonly requisitions = signal<JobRequisitionListItem[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = signal<CreateJobRequisition>({
    departmentId: 1,
    designationId: 1,
    gradeId: 1,
    workLocationId: 1,
    openings: 1,
    employmentType: 'Permanent',
    minCtc: 500000,
    maxCtc: 800000,
    justification: '',
    isReplacement: false,
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const data = await this.api.requisitions();
      this.requisitions.set(data);
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load requisitions');
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    this.form.set({
      departmentId: 1,
      designationId: 1,
      gradeId: 1,
      workLocationId: 1,
      openings: 1,
      employmentType: 'Permanent',
      minCtc: 500000,
      maxCtc: 800000,
      justification: '',
      isReplacement: false,
    });
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
  }

  async save(): Promise<void> {
    const data = this.form();
    if (!data.justification.trim()) {
      this.errorMessage.set('Justification is required.');
      return;
    }

    this.loading.set(true);
    try {
      await this.api.createRequisition(data);
      this.showForm.set(false);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save requisition');
    } finally {
      this.loading.set(false);
    }
  }

  async submit(id: number): Promise<void> {
    try {
      await this.api.submitRequisition(id);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Submit failed');
    }
  }

  async approve(id: number): Promise<void> {
    try {
      await this.api.approveRequisition(id);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Approval failed');
    }
  }

  async reject(id: number): Promise<void> {
    try {
      await this.api.rejectRequisition(id);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Rejection failed');
    }
  }
}
