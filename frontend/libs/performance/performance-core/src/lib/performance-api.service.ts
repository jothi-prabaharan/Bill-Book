import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CompetencyGroup,
  Competency,
  CycleStatus,
  DepartmentCalibration,
  Goal,
  LevelDecision,
  LevelReview,
  PerformanceReview,
  RatingScale,
  ReviewCycle,
  ReviewStatus,
  SaveSelfEvaluationRequest,
  SelfEvaluation,
} from './performance.models';

@Injectable({ providedIn: 'root' })
export class PerformanceApiService {
  private readonly http = inject(HttpClient);

  // ---- Rating Scales ----
  getRatingScales(): Promise<RatingScale[]> {
    return firstValueFrom(this.http.get<RatingScale[]>('/api/prf/rating-scales'));
  }

  saveRatingScale(body: Partial<RatingScale>): Promise<RatingScale> {
    return firstValueFrom(this.http.post<RatingScale>('/api/prf/rating-scales', body));
  }

  // ---- Competencies ----
  getCompetencyGroups(): Promise<CompetencyGroup[]> {
    return firstValueFrom(this.http.get<CompetencyGroup[]>('/api/prf/competencies/groups'));
  }

  createCompetencyGroup(body: { name: string; description?: string }): Promise<CompetencyGroup> {
    return firstValueFrom(this.http.post<CompetencyGroup>('/api/prf/competencies/groups', body));
  }

  createCompetency(body: { competencyGroupId: number; name: string; description?: string }): Promise<Competency> {
    return firstValueFrom(this.http.post<Competency>('/api/prf/competencies', body));
  }

  // ---- Cycles ----
  getCycles(): Promise<ReviewCycle[]> {
    return firstValueFrom(this.http.get<ReviewCycle[]>('/api/prf/cycles'));
  }

  getCycle(id: number): Promise<ReviewCycle> {
    return firstValueFrom(this.http.get<ReviewCycle>(`/api/prf/cycles/${id}`));
  }

  createCycle(body: Partial<ReviewCycle>): Promise<ReviewCycle> {
    return firstValueFrom(this.http.post<ReviewCycle>('/api/prf/cycles', body));
  }

  updateCycleStatus(id: number, status: CycleStatus): Promise<void> {
    return firstValueFrom(this.http.patch<void>(`/api/prf/cycles/${id}/status`, JSON.stringify(status), {
      headers: { 'Content-Type': 'application/json' },
    }));
  }

  enrollEmployees(id: number, body: { joinedBeforeDate?: string; departmentIds?: number[]; specificEmployeeIds?: number[] }): Promise<{ enrolled: number }> {
    return firstValueFrom(this.http.post<{ enrolled: number }>(`/api/prf/cycles/${id}/enroll`, body));
  }

  closeCycle(id: number): Promise<{ closed: number }> {
    return firstValueFrom(this.http.post<{ closed: number }>(`/api/prf/cycles/${id}/close`, {}));
  }

  // ---- Goals ----
  getGoals(cycleId: number, employeeId: number): Promise<Goal[]> {
    return firstValueFrom(this.http.get<Goal[]>(`/api/prf/goals?cycleId=${cycleId}&employeeId=${employeeId}`));
  }

  saveGoal(body: Partial<Goal>): Promise<Goal> {
    return firstValueFrom(this.http.post<Goal>('/api/prf/goals', body));
  }

  // ---- Reviews & Approvals ----
  getReviews(cycleId?: number, employeeId?: number, status?: ReviewStatus): Promise<PerformanceReview[]> {
    const params = new URLSearchParams();
    if (cycleId) params.append('cycleId', cycleId.toString());
    if (employeeId) params.append('employeeId', employeeId.toString());
    if (status) params.append('status', status);
    const qs = params.toString() ? `?${params.toString()}` : '';
    return firstValueFrom(this.http.get<PerformanceReview[]>(`/api/prf/reviews${qs}`));
  }

  getSelfEvaluation(reviewId: number): Promise<SelfEvaluation> {
    return firstValueFrom(this.http.get<SelfEvaluation>(`/api/prf/reviews/${reviewId}/self-evaluation`));
  }

  getLevelReviews(reviewId: number): Promise<LevelReview[]> {
    return firstValueFrom(this.http.get<LevelReview[]>(`/api/prf/reviews/${reviewId}/levels`));
  }

  actLevelReview(reviewId: number, body: {
    decision: LevelDecision;
    comments: string;
    ratingLevelId?: number;
    increasePercent?: number;
    isPromotionRecommended?: boolean;
    recommendedDesignationId?: number;
    goalRatings?: Record<string, unknown>[];
    competencyRatings?: Record<string, unknown>[];
  }): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/prf/reviews/${reviewId}/act`, body));
  }

  reopenReview(reviewId: number): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/prf/reviews/${reviewId}/reopen`, {}));
  }

  releaseReviews(cycleId: number, departmentId?: number): Promise<{ releasedCount: number }> {
    const qs = departmentId ? `&departmentId=${departmentId}` : '';
    return firstValueFrom(this.http.post<{ releasedCount: number }>(`/api/prf/reviews/release?cycleId=${cycleId}${qs}`, {}));
  }

  // ---- Calibration ----
  getCalibrationDistribution(cycleId: number): Promise<DepartmentCalibration[]> {
    return firstValueFrom(this.http.get<DepartmentCalibration[]>(`/api/prf/calibration/distribution/${cycleId}`));
  }

  adjustCalibration(body: { performanceReviewId: number; toRatingLevelId: number; reason: string }): Promise<void> {
    return firstValueFrom(this.http.post<void>('/api/prf/calibration/adjust', body));
  }

  // ---- Employee Self Service ----
  getMyReviews(): Promise<PerformanceReview[]> {
    return firstValueFrom(this.http.get<PerformanceReview[]>('/api/me/appraisals'));
  }

  getMySelfEvaluation(reviewId: number): Promise<SelfEvaluation> {
    return firstValueFrom(this.http.get<SelfEvaluation>(`/api/me/appraisals/${reviewId}/self-evaluation`));
  }

  saveMySelfEvaluation(reviewId: number, body: SaveSelfEvaluationRequest): Promise<SelfEvaluation> {
    return firstValueFrom(this.http.post<SelfEvaluation>(`/api/me/appraisals/${reviewId}/self-evaluation`, body));
  }

  acknowledgeReview(reviewId: number, comment?: string): Promise<void> {
    return firstValueFrom(this.http.post<void>(`/api/me/appraisals/${reviewId}/acknowledge`, { comment }));
  }
}
