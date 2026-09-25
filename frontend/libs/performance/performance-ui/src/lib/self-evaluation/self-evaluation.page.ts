import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  PerformanceApiService,
  PerformanceReview,
  RatingScale,
  SelfEvaluation,
} from '@bill-book/performance-core';

@Component({
  selector: 'bb-performance-self-evaluation',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './self-evaluation.page.html',
  styleUrl: '../performance-page.scss',
})
export class SelfEvaluationPage implements OnInit {
  private readonly api = inject(PerformanceApiService);

  readonly myReviews = signal<PerformanceReview[]>([]);
  readonly selectedReviewId = signal<number | null>(null);
  readonly currentReview = signal<PerformanceReview | null>(null);
  readonly selfEval = signal<SelfEvaluation | null>(null);
  readonly ratingScales = signal<RatingScale[]>([]);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);

  // Form
  readonly form = signal({
    achievements: '',
    challenges: '',
    strengths: '',
    areasToImprove: '',
    trainingNeeds: '',
    careerAspirations: '',
    overallSelfRatingLevelId: undefined as number | undefined,
  });

  readonly ackComment = signal('');

  ngOnInit(): void {
    void this.loadReviews();
  }

  async loadReviews(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const [revs, scales] = await Promise.all([
        this.api.getMyReviews(),
        this.api.getRatingScales(),
      ]);
      this.myReviews.set(revs);
      this.ratingScales.set(scales);
      if (revs.length > 0) {
        await this.selectReview(revs[0].performanceReviewId);
      }
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load self-service appraisals');
    } finally {
      this.loading.set(false);
    }
  }

  async selectReview(id: number): Promise<void> {
    this.selectedReviewId.set(id);
    const r = this.myReviews().find(x => x.performanceReviewId === id) || null;
    this.currentReview.set(r);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const se = await this.api.getMySelfEvaluation(id);
      this.selfEval.set(se);
      this.form.set({
        achievements: se.achievements || '',
        challenges: se.challenges || '',
        strengths: se.strengths || '',
        areasToImprove: se.areasToImprove || '',
        trainingNeeds: se.trainingNeeds || '',
        careerAspirations: se.careerAspirations || '',
        overallSelfRatingLevelId: se.overallSelfRatingLevelId,
      });
    } catch {
      this.selfEval.set(null);
      this.form.set({
        achievements: '',
        challenges: '',
        strengths: '',
        areasToImprove: '',
        trainingNeeds: '',
        careerAspirations: '',
        overallSelfRatingLevelId: undefined,
      });
    }
  }

  isLocked(): boolean {
    const se = this.selfEval();
    return !!(se && se.isSubmitted && this.currentReview()?.reviewStatus !== 'SentBack');
  }

  async save(isSubmit: boolean): Promise<void> {
    const revId = this.selectedReviewId();
    if (!revId) return;

    if (isSubmit && !this.form().achievements.trim()) {
      this.errorMessage.set('Achievements description is required before submitting.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    try {
      const se = this.selfEval();
      const body = {
        ...this.form(),
        isSubmit,
        goals: se?.goals.map(g => ({
          goalId: g.goalId,
          selfRatingLevelId: g.selfRatingLevelId,
          achievementPercent: g.achievementPercent,
          comments: g.comments,
        })) || [],
        competencies: se?.competencies.map(c => ({
          competencyId: c.competencyId,
          selfRatingLevelId: c.selfRatingLevelId,
          comments: c.comments,
        })) || [],
      };

      const updated = await this.api.saveMySelfEvaluation(revId, body);
      this.selfEval.set(updated);
      this.successMessage.set(isSubmit ? 'Self-evaluation successfully submitted and frozen for review.' : 'Draft saved successfully.');
      await this.loadReviews();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save self-evaluation');
    } finally {
      this.loading.set(false);
    }
  }

  async acknowledge(): Promise<void> {
    const revId = this.selectedReviewId();
    if (!revId) return;

    try {
      await this.api.acknowledgeReview(revId, this.ackComment());
      this.successMessage.set('Appraisal successfully acknowledged.');
      await this.loadReviews();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to acknowledge appraisal');
    }
  }
}
