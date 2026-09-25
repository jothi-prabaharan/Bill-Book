import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  ApplicationListItem,
  InterviewOutcome,
  InterviewRoundView,
  RecruitmentApiService,
  RoundKind,
  ScheduleInterviewRound,
  UpdateInterviewFeedback,
} from '@bill-book/recruitment-core';

@Component({
  selector: 'rec-interviews-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './interviews.page.html',
  styleUrl: '../recruitment-page.scss',
})
export class InterviewsPage implements OnInit {
  private readonly api = inject(RecruitmentApiService);

  readonly applications = signal<ApplicationListItem[]>([]);
  readonly interviews = signal<InterviewRoundView[]>([]);
  readonly selectedAppId = signal<number | null>(null);
  readonly loading = signal(false);
  readonly showScheduleModal = signal(false);
  readonly showFeedbackModal = signal(false);
  readonly selectedRoundId = signal<number | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly scheduleForm = signal<ScheduleInterviewRound>({
    applicationId: 0,
    roundNo: 1,
    roundKind: 'Technical',
    scheduledAt: new Date(Date.now() + 86400000).toISOString().slice(0, 16),
    interviewerEmployeeId: 1,
  });

  readonly feedbackForm = signal<UpdateInterviewFeedback>({
    rating: 4,
    feedback: '',
    outcome: 'Pass',
  });

  async ngOnInit(): Promise<void> {
    await this.loadApplications();
  }

  async loadApplications(): Promise<void> {
    try {
      const apps = await this.api.applications(1, 100);
      this.applications.set(apps);
      if (apps.length > 0 && !this.selectedAppId()) {
        await this.selectApplication(apps[0].applicationId);
      }
    } catch {
      // ignore
    }
  }

  async selectApplication(appId: number): Promise<void> {
    this.selectedAppId.set(appId);
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const rounds = await this.api.interviews(appId);
      this.interviews.set(rounds);
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load interviews');
    } finally {
      this.loading.set(false);
    }
  }

  openSchedule(): void {
    const appId = this.selectedAppId();
    if (!appId) return;

    const nextRoundNo = this.interviews().length + 1;
    this.scheduleForm.set({
      applicationId: appId,
      roundNo: nextRoundNo,
      roundKind: 'Technical',
      scheduledAt: new Date(Date.now() + 86400000).toISOString().slice(0, 16),
      interviewerEmployeeId: 1,
    });
    this.showScheduleModal.set(true);
  }

  async schedule(): Promise<void> {
    this.loading.set(true);
    try {
      await this.api.scheduleInterview(this.scheduleForm());
      this.showScheduleModal.set(false);
      if (this.selectedAppId()) {
        await this.selectApplication(this.selectedAppId()!);
      }
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Scheduling failed');
    } finally {
      this.loading.set(false);
    }
  }

  openFeedback(round: InterviewRoundView): void {
    this.selectedRoundId.set(round.interviewRoundId);
    this.feedbackForm.set({
      rating: round.rating || 4,
      feedback: round.feedback || '',
      outcome: (round.outcome as InterviewOutcome) || 'Pass',
    });
    this.showFeedbackModal.set(true);
  }

  async saveFeedback(): Promise<void> {
    const rid = this.selectedRoundId();
    if (!rid) return;

    this.loading.set(true);
    try {
      await this.api.submitInterviewFeedback(rid, this.feedbackForm());
      this.showFeedbackModal.set(false);
      if (this.selectedAppId()) {
        await this.selectApplication(this.selectedAppId()!);
      }
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save feedback');
    } finally {
      this.loading.set(false);
    }
  }
}
