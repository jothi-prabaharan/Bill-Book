export type CycleKind = 'Annual' | 'HalfYearly' | 'Quarterly' | 'Probation';
export type CycleStatus = 'Draft' | 'GoalSetting' | 'SelfEvaluation' | 'InReview' | 'Calibration' | 'Released' | 'Closed';
export type ReviewStatus = 'NotStarted' | 'SelfEvaluationDraft' | 'SelfEvaluationSubmitted' | 'InApproval' | 'SentBack' | 'Calibration' | 'Released' | 'Acknowledged' | 'Closed';
export type GoalStatus = 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected';
export type LevelDecision = 'Approved' | 'SentBack' | 'Rejected';

export interface RatingLevel {
  ratingLevelId: number;
  ratingScaleId: number;
  score: number;
  label: string;
  description: string;
}

export interface RatingScale {
  ratingScaleId: number;
  name: string;
  description?: string;
  isActive: boolean;
  levels: RatingLevel[];
}

export interface Competency {
  competencyId: number;
  competencyGroupId: number;
  groupName: string;
  name: string;
  description?: string;
}

export interface CompetencyGroup {
  competencyGroupId: number;
  name: string;
  description?: string;
  competencies: Competency[];
}

export interface ReviewCycle {
  reviewCycleId: number;
  name: string;
  periodFrom: string;
  periodTo: string;
  cycleKind: CycleKind;
  ratingScaleId: number;
  ratingScaleName: string;
  goalWeightPercent: number;
  competencyWeightPercent: number;
  goalSettingDueDate: string;
  selfEvaluationDueDate: string;
  reviewDueDate: string;
  isSelfEvaluationRequired: boolean;
  isPeerFeedbackEnabled: boolean;
  isCalibrationEnabled: boolean;
  cycleStatus: CycleStatus;
  totalEligible: number;
  inProgressCount: number;
  completedCount: number;
}

export interface Goal {
  goalId: number;
  reviewCycleId: number;
  employeeId: number;
  title: string;
  description?: string;
  weightage: number;
  measure?: string;
  target?: string;
  goalStatus: GoalStatus;
}

export interface PerformanceReview {
  performanceReviewId: number;
  reviewCycleId: number;
  cycleName: string;
  employeeId: number;
  employeeName: string;
  employeeCode: string;
  departmentId?: number;
  departmentName?: string;
  reviewStatus: ReviewStatus;
  currentStepSequence: number;
  currentStepLabel?: string;
  currentAssigneeEmployeeId?: number;
  selfSubmittedAt?: string;
  finalGoalScore?: number;
  finalCompetencyScore?: number;
  finalScore?: number;
  finalRatingLevelId?: number;
  finalRatingLabel?: string;
  recommendedIncreasePercent?: number;
  isPromotionRecommended: boolean;
  recommendedDesignationId?: number;
  releasedAt?: string;
  acknowledgedAt?: string;
  employeeAcknowledgementComment?: string;
}

export interface SelfEvaluation {
  selfEvaluationId: number;
  performanceReviewId: number;
  overallSelfRatingLevelId?: number;
  achievements: string;
  challenges?: string;
  strengths?: string;
  areasToImprove?: string;
  trainingNeeds?: string;
  careerAspirations?: string;
  isSubmitted: boolean;
  submittedAt?: string;
  goals: GoalSelfAssessment[];
  competencies: CompetencySelfAssessment[];
}

export interface GoalSelfAssessment {
  goalSelfAssessmentId: number;
  goalId: number;
  goalTitle: string;
  weightage: number;
  selfRatingLevelId?: number;
  selfRatingLabel?: string;
  achievementPercent?: number;
  comments?: string;
  evidences: SelfEvidence[];
}

export interface SelfEvidence {
  selfEvidenceId: number;
  attachmentKey: string;
  title: string;
}

export interface CompetencySelfAssessment {
  competencySelfAssessmentId: number;
  competencyId: number;
  competencyName: string;
  groupName: string;
  selfRatingLevelId?: number;
  selfRatingLabel?: string;
  comments?: string;
}

export interface LevelReview {
  levelReviewId: number;
  performanceReviewId: number;
  sequence: number;
  label: string;
  reviewerEmployeeId: number;
  reviewerName?: string;
  ratingLevelId?: number;
  ratingLabel?: string;
  increasePercent?: number;
  isPromotionRecommended?: boolean;
  recommendedDesignationId?: number;
  comments: string;
  decision: LevelDecision;
  actedAt: string;
  goalRatings: LevelGoalRating[];
  competencyRatings: LevelCompetencyRating[];
}

export interface LevelGoalRating {
  goalId: number;
  goalTitle: string;
  ratingLevelId?: number;
  score?: number;
  comments?: string;
}

export interface LevelCompetencyRating {
  competencyId: number;
  competencyName: string;
  ratingLevelId?: number;
  score?: number;
  comments?: string;
}

export interface DepartmentCalibration {
  departmentId: number;
  departmentName: string;
  totalReviews: number;
  buckets: CalibrationRatingBucket[];
}

export interface CalibrationRatingBucket {
  ratingLevelId: number;
  label: string;
  count: number;
  percentage: number;
}

export interface SaveSelfEvaluationRequest {
  overallSelfRatingLevelId?: number;
  achievements: string;
  challenges?: string;
  strengths?: string;
  areasToImprove?: string;
  trainingNeeds?: string;
  careerAspirations?: string;
  isSubmit: boolean;
  goals: SaveGoalAssessmentItem[];
  competencies: SaveCompetencyAssessmentItem[];
}

export interface SaveGoalAssessmentItem {
  goalId: number;
  selfRatingLevelId?: number;
  achievementPercent?: number;
  comments?: string;
}

export interface SaveCompetencyAssessmentItem {
  competencyId: number;
  selfRatingLevelId?: number;
  comments?: string;
}

