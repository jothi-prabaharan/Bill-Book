import { CommonModule } from '@angular/common';
import {
  ColumnDef,
  DataGridCellTemplateDirective,
  DataGridComponent,
  BranchDateInputComponent,
  MessageBoxComponent,
  PercentageInputComponent,
  SelectComponent,
  TextInputComponent,
} from '@bill-book/ui-components';
import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CycleKind,
  CycleStatus,
  PerformanceApiService,
  RatingScale,
  ReviewCycle,
} from '@bill-book/performance-core';

@Component({
  selector: 'bb-performance-cycles',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MessageBoxComponent,
    BranchDateInputComponent,
    PercentageInputComponent,
    SelectComponent,
    TextInputComponent,
    DataGridComponent,
    DataGridCellTemplateDirective,
  ],
  templateUrl: './cycles.page.html',
  styleUrl: '../performance-page.scss',
})
export class CyclesPage implements OnInit {
  protected readonly cyclesColumns: ColumnDef[] = [
    { field: 'name', header: 'Cycle Name' },
    { field: 'periodFrom', header: 'Period' },
    { field: 'cycleKind', header: 'Kind' },
    { field: 'ratingScaleName', header: 'Rating Scale' },
    { field: 'cycleStatus', header: 'Status' },
    { field: 'goalWeightPercent', header: 'Weights (Goal/Comp)' },
    { field: 'col6', header: 'Actions', sortable: false, filterable: false },
  ];

  protected readonly ratingScaleOptions = computed(() =>
    this.ratingScales().map((s) => ({ value: s.ratingScaleId, label: `${s.name}` })),
  );

  private readonly api = inject(PerformanceApiService);

  readonly cycles = signal<ReviewCycle[]>([]);
  readonly ratingScales = signal<RatingScale[]>([]);
  readonly loading = signal(false);
  readonly showForm = signal(false);
  readonly showEnroll = signal(false);
  readonly selectedCycleId = signal<number | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly form = signal({
    name: '',
    periodFrom: new Date().toISOString().slice(0, 10),
    periodTo: new Date(Date.now() + 365 * 86400000).toISOString().slice(0, 10),
    cycleKind: 'Annual' as CycleKind,
    ratingScaleId: 0,
    goalWeightPercent: 50,
    competencyWeightPercent: 50,
    goalSettingDueDate: new Date().toISOString().slice(0, 10),
    selfEvaluationDueDate: new Date().toISOString().slice(0, 10),
    reviewDueDate: new Date().toISOString().slice(0, 10),
    isSelfEvaluationRequired: true,
    isPeerFeedbackEnabled: false,
    isCalibrationEnabled: true,
  });

  readonly enrollForm = signal({
    joinedBeforeDate: new Date().toISOString().slice(0, 10),
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const [cycList, scales] = await Promise.all([
        this.api.getCycles(),
        this.api.getRatingScales(),
      ]);
      this.cycles.set(cycList);
      this.ratingScales.set(scales);
      if (scales.length > 0 && this.form().ratingScaleId === 0) {
        this.form.update(f => ({ ...f, ratingScaleId: scales[0].ratingScaleId }));
      }
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to load cycles');
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    this.showForm.set(true);
  }

  cancelCreate(): void {
    this.showForm.set(false);
  }

  async saveCycle(): Promise<void> {
    try {
      await this.api.createCycle(this.form());
      this.showForm.set(false);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to save cycle');
    }
  }

  async setStatus(id: number, status: CycleStatus): Promise<void> {
    try {
      await this.api.updateCycleStatus(id, status);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to update cycle status');
    }
  }

  openEnroll(id: number): void {
    this.selectedCycleId.set(id);
    this.showEnroll.set(true);
  }

  async enroll(): Promise<void> {
    const id = this.selectedCycleId();
    if (!id) return;
    try {
      await this.api.enrollEmployees(id, { joinedBeforeDate: this.enrollForm().joinedBeforeDate });
      this.showEnroll.set(false);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to enroll employees');
    }
  }

  async closeCycle(id: number): Promise<void> {
    if (!confirm('Closing the review cycle will finalize all appraisals and raise salary revisions in Payroll. Proceed?')) {
      return;
    }
    try {
      await this.api.closeCycle(id);
      await this.load();
    } catch (err: unknown) {
      this.errorMessage.set(err instanceof Error ? err.message : 'Failed to close cycle');
    }
  }
}

