import { CommonModule } from '@angular/common';
import {
  CheckboxComponent,
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  NumberInputComponent,
  PercentageInputComponent,
  SelectComponent,
  TextareaComponent,
} from '@bill-book/ui-components';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  LevelDecision,
  LevelReview,
  PerformanceApiService,
  PerformanceReview,
  RatingScale,
  ReviewCycle,
  SelfEvaluation,
} from '@bill-book/performance-core';

@Component({
  selector: 'bb-performance-reviews',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MessageBoxComponent,
    CheckboxComponent,
    NumberInputComponent,
    PercentageInputComponent,
    SelectComponent,
    TextareaComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
  ],
  templateUrl: './reviews.page.html',
  styleUrl: '../performance-page.scss',
})
export class ReviewsPage implements OnInit {
  protected readonly reviewsColumns: ColumnDef[] = [
    { field: 'employeeName', header: 'Employee' },
    { field: 'cycleName', header: 'Cycle' },
    { field: 'reviewStatus', header: 'Status' },
    { field: 'currentStepLabel', header: 'Current Step' },
    { field: 'finalScore', header: 'Final Score' },
    { field: 'recommendedIncreasePercent', header: 'Recommended Increment' },
    { field: 'isPromotionRecommended', header: 'Promotion' },
    { field: 'col7', header: 'Actions', sortable: false, filterable: false },
  ];

  protected readonly selectedCycleOptions = computed(() =>
    this.cycles().map((c) => ({ value: c.reviewCycleId, label: `${c.name}` })),
  );

  protected readonly decisionOptions = [
    { value: 'Approved', label: 'Approve & Advance' },
    { value: 'SentBack', label: 'Send Back (Returns to level before / employee)' },
    { value: 'Rejected', label: 'Reject' },
  ];

  private readonly api = inject(PerformanceApiService);

  readonly reviews = signal<PerformanceReview[]>([]);
  readonly cycles = signal<ReviewCycle[]>([]);
  readonly ratingScales = signal<RatingScale[]>([]);
  readonly selectedCycleId = signal<number | undefined>(undefined);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  // Detail drawer / sheet
  readonly activeReview = signal<PerformanceReview | null>(null);
  readonly activeSelfEval = signal<SelfEvaluation | null>(null);
  readonly activeLevels = signal<LevelReview[]>([]);
  readonly showActionModal = signal(false);

  readonly actionForm = signal({
    decision: 'Approved' as LevelDecision,
    comments: '',
    ratingLevelId: undefined as number | undefined,
    increasePercent: undefined as number | undefined,
    isPromotionRecommended: false,
  });

  ngOnInit(): void {
    void this.init();
  }

  async init(): Promise<void> {
    try {
      const [cycs, scales] = await Promise.all([
        this.api.getCycles(),
        this.api.getRatingScales(),
      ]);
      this.cycles.set(cycs);
      this.ratingScales.set(scales);
      await this.loadReviews();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to initialize reviews');
    }
  }

  async loadReviews(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const list = await this.api.getReviews(this.selectedCycleId());
      this.reviews.set(list);
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load reviews');
    } finally {
      this.loading.set(false);
    }
  }

  async openReview(r: PerformanceReview): Promise<void> {
    this.activeReview.set(r);
    try {
      const [se, levels] = await Promise.all([
        this.api.getSelfEvaluation(r.performanceReviewId),
        this.api.getLevelReviews(r.performanceReviewId),
      ]);
      this.activeSelfEval.set(se);
      this.activeLevels.set(levels);
    } catch {
      this.activeSelfEval.set(null);
      this.activeLevels.set([]);
    }
  }

  closeDetail(): void {
    this.activeReview.set(null);
    this.activeSelfEval.set(null);
    this.activeLevels.set([]);
  }

  openActModal(): void {
    this.actionForm.set({
      decision: 'Approved',
      comments: '',
      ratingLevelId: undefined,
      increasePercent: undefined,
      isPromotionRecommended: false,
    });
    this.showActionModal.set(true);
  }

  async submitLevelAction(): Promise<void> {
    const rev = this.activeReview();
    if (!rev) return;

    try {
      await this.api.actLevelReview(rev.performanceReviewId, this.actionForm());
      this.showActionModal.set(false);
      await this.openReview(rev);
      await this.loadReviews();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to submit review action');
    }
  }

  async releaseDepartment(): Promise<void> {
    const cycleId = this.selectedCycleId() || (this.cycles().length > 0 ? this.cycles()[0].reviewCycleId : 0);
    if (!cycleId) return;

    try {
      const res = await this.api.releaseReviews(cycleId);
      alert(`Released ${res.releasedCount} reviews.`);
      await this.loadReviews();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to release reviews');
    }
  }
}
