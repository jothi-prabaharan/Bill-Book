import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ApplicationListItem,
  ApplicationStage,
  JobOpeningListItem,
  RecruitmentApiService,
} from '@bill-book/recruitment-core';

@Component({
  selector: 'rec-pipeline-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './pipeline.page.html',
  styleUrl: '../recruitment-page.scss',
})
export class PipelinePage implements OnInit {
  private readonly api = inject(RecruitmentApiService);

  readonly openings = signal<JobOpeningListItem[]>([]);
  readonly applications = signal<ApplicationListItem[]>([]);
  readonly selectedOpeningId = signal<number | undefined>(undefined);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly stages: ApplicationStage[] = [
    'Applied',
    'Screening',
    'Interview',
    'Offer',
    'Hired',
    'Rejected',
  ];

  async ngOnInit(): Promise<void> {
    await this.loadOpenings();
    await this.loadApplications();
  }

  async loadOpenings(): Promise<void> {
    try {
      const ops = await this.api.openings();
      this.openings.set(ops);
    } catch {
      // ignore
    }
  }

  async loadApplications(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const apps = await this.api.applications(1, 100, this.selectedOpeningId());
      this.applications.set(apps);
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load pipeline applications');
    } finally {
      this.loading.set(false);
    }
  }

  onOpeningChange(opIdStr: string): void {
    const opId = opIdStr ? Number(opIdStr) : undefined;
    this.selectedOpeningId.set(opId);
    void this.loadApplications();
  }

  appsInStage(stage: string): ApplicationListItem[] {
    return this.applications().filter(a => a.stage === stage);
  }

  async advanceStage(app: ApplicationListItem, nextStage: ApplicationStage): Promise<void> {
    this.loading.set(true);
    try {
      await this.api.updateApplicationStage(app.applicationId, { stage: nextStage });
      await this.loadApplications();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Stage update failed');
    } finally {
      this.loading.set(false);
    }
  }

  async rejectApplication(app: ApplicationListItem): Promise<void> {
    const reason = prompt('Rejection reason (optional):');
    this.loading.set(true);
    try {
      await this.api.updateApplicationStage(app.applicationId, {
        stage: 'Rejected',
        rejectionReason: reason || undefined,
      });
      await this.loadApplications();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Rejection failed');
    } finally {
      this.loading.set(false);
    }
  }
}
