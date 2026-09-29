import { CommonModule } from '@angular/common';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  MessageBoxComponent,
  NumberInputComponent,
  SelectComponent,
  TextareaComponent,
} from '@bill-book/ui-components';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DepartmentCalibration,
  PerformanceApiService,
  PerformanceReview,
  ReviewCycle,
} from '@bill-book/performance-core';

@Component({
  selector: 'bb-performance-calibration',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MessageBoxComponent,
    NumberInputComponent,
    SelectComponent,
    TextareaComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
  ],
  templateUrl: './calibration.page.html',
  styleUrl: '../performance-page.scss',
})
export class CalibrationPage implements OnInit {
  protected readonly reviewsColumns: ColumnDef[] = [
    { field: 'employeeName', header: 'Employee' },
    { field: 'reviewStatus', header: 'Status' },
    { field: 'finalScore', header: 'Final Score' },
    { field: 'finalRatingLabel', header: 'Final Rating' },
    { field: 'recommendedIncreasePercent', header: 'Recommended Increment' },
    { field: 'performanceReviewId', header: 'Actions', sortable: false, filterable: false },
  ];

  protected readonly selectedCycleOptions = computed(() =>
    this.cycles().map((c) => ({ value: c.reviewCycleId, label: `${c.name}` })),
  );

  private readonly api = inject(PerformanceApiService);

  readonly cycles = signal<ReviewCycle[]>([]);
  readonly selectedCycleId = signal<number>(0);
  readonly calibrations = signal<DepartmentCalibration[]>([]);
  readonly reviews = signal<PerformanceReview[]>([]);
  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  // Adjustment
  readonly showAdjustModal = signal(false);
  readonly adjustForm = signal({
    performanceReviewId: 0,
    toRatingLevelId: 3,
    reason: '',
  });

  ngOnInit(): void {
    void this.loadCycles();
  }

  async loadCycles(): Promise<void> {
    try {
      const cycs = await this.api.getCycles();
      this.cycles.set(cycs);
      if (cycs.length > 0) {
        this.selectedCycleId.set(cycs[0].reviewCycleId);
        await this.loadCalibration();
      }
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load cycles');
    }
  }

  async loadCalibration(): Promise<void> {
    const cId = this.selectedCycleId();
    if (!cId) return;

    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const [dist, revs] = await Promise.all([
        this.api.getCalibrationDistribution(cId),
        this.api.getReviews(cId),
      ]);
      this.calibrations.set(dist);
      this.reviews.set(revs);
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load calibration data');
    } finally {
      this.loading.set(false);
    }
  }

  openAdjust(reviewId: number): void {
    this.adjustForm.set({
      performanceReviewId: reviewId,
      toRatingLevelId: 3,
      reason: '',
    });
    this.showAdjustModal.set(true);
  }

  async submitAdjust(): Promise<void> {
    try {
      await this.api.adjustCalibration(this.adjustForm());
      this.showAdjustModal.set(false);
      await this.loadCalibration();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to apply calibration adjustment');
    }
  }
}
