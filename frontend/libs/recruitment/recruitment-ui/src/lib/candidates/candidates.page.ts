import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CandidateListItem,
  CreateCandidate,
  JobOpeningListItem,
  RecruitmentApiService,
} from '@bill-book/recruitment-core';

@Component({
  selector: 'rec-candidates-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './candidates.page.html',
  styleUrl: '../recruitment-page.scss',
})
export class CandidatesPage implements OnInit {
  private readonly api = inject(RecruitmentApiService);

  readonly candidates = signal<CandidateListItem[]>([]);
  readonly openings = signal<JobOpeningListItem[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);
  readonly showApplyModal = signal(false);
  readonly selectedCandidateId = signal<number | null>(null);
  readonly selectedOpeningId = signal<number | null>(null);
  readonly searchTerm = signal('');
  readonly errorMessage = signal<string | null>(null);

  readonly form = signal<CreateCandidate>({
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    currentEmployer: '',
    currentCtc: undefined,
    expectedCtc: undefined,
    noticePeriodDays: 30,
    candidateSource: 'Portal',
  });

  async ngOnInit(): Promise<void> {
    await this.load();
    const ops = await this.api.openings(1, 100, 'Open');
    this.openings.set(ops);
    if (ops.length > 0) this.selectedOpeningId.set(ops[0].jobOpeningId);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const data = await this.api.candidates(1, 50, this.searchTerm());
      this.candidates.set(data);
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load candidates');
    } finally {
      this.loading.set(false);
    }
  }

  onSearch(): void {
    void this.load();
  }

  openCreate(): void {
    this.form.set({
      firstName: '',
      lastName: '',
      email: '',
      phone: '',
      currentEmployer: '',
      currentCtc: undefined,
      expectedCtc: undefined,
      noticePeriodDays: 30,
      candidateSource: 'Portal',
    });
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
  }

  async save(): Promise<void> {
    const data = this.form();
    if (!data.firstName.trim()) {
      this.errorMessage.set('First name is required.');
      return;
    }
    if (!data.email.trim() || !data.phone.trim()) {
      this.errorMessage.set('Email and phone are required.');
      return;
    }

    this.loading.set(true);
    try {
      await this.api.createCandidate(data);
      this.showForm.set(false);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save candidate');
    } finally {
      this.loading.set(false);
    }
  }

  openApply(candidateId: number): void {
    this.selectedCandidateId.set(candidateId);
    this.showApplyModal.set(true);
  }

  async applyToOpening(): Promise<void> {
    const cid = this.selectedCandidateId();
    const oid = this.selectedOpeningId();
    if (!cid || !oid) return;

    this.loading.set(true);
    try {
      await this.api.createApplication({ candidateId: cid, jobOpeningId: oid });
      this.showApplyModal.set(false);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Application submission failed');
    } finally {
      this.loading.set(false);
    }
  }
}
