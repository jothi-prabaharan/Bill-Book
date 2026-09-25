using System.ComponentModel.DataAnnotations;
using Performance.Entity.Enums;

namespace Performance.Entity.Models;

// ==========================================
// 1. Rating Scales
// ==========================================
public sealed class RatingScaleView
{
    public long RatingScaleId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public List<RatingLevelView> Levels { get; set; } = [];
}

public sealed class RatingLevelView
{
    public long RatingLevelId { get; set; }
    public long RatingScaleId { get; set; }
    public int Score { get; set; }
    public string Label { get; set; } = null!;
    public string Description { get; set; } = null!;
}

public sealed class SaveRatingScaleRequest
{
    [Required(ErrorMessage = "Scale name is required.")]
    [MaxLength(100, ErrorMessage = "Scale name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<SaveRatingLevelRequest> Levels { get; set; } = [];
}

public sealed class SaveRatingLevelRequest
{
    public long? RatingLevelId { get; set; }
    public int Score { get; set; }

    [Required(ErrorMessage = "Level label is required.")]
    [MaxLength(50, ErrorMessage = "Level label cannot exceed 50 characters.")]
    public string Label { get; set; } = null!;

    [Required(ErrorMessage = "Level description is required.")]
    [MaxLength(200, ErrorMessage = "Level description cannot exceed 200 characters.")]
    public string Description { get; set; } = null!;
}

// ==========================================
// 2. Competencies
// ==========================================
public sealed class CompetencyGroupView
{
    public long CompetencyGroupId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public List<CompetencyView> Competencies { get; set; } = [];
}

public sealed class CompetencyView
{
    public long CompetencyId { get; set; }
    public long CompetencyGroupId { get; set; }
    public string GroupName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}

public sealed class SaveCompetencyGroupRequest
{
    [Required(ErrorMessage = "Group name is required.")]
    [MaxLength(100, ErrorMessage = "Group name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }
}

public sealed class SaveCompetencyRequest
{
    public long CompetencyGroupId { get; set; }

    [Required(ErrorMessage = "Competency name is required.")]
    [MaxLength(100, ErrorMessage = "Competency name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }
}

// ==========================================
// 3. Review Cycles
// ==========================================
public sealed class ReviewCycleView
{
    public long ReviewCycleId { get; set; }
    public string Name { get; set; } = null!;
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public CycleKind CycleKind { get; set; }
    public long RatingScaleId { get; set; }
    public string RatingScaleName { get; set; } = null!;
    public decimal GoalWeightPercent { get; set; }
    public decimal CompetencyWeightPercent { get; set; }
    public DateOnly GoalSettingDueDate { get; set; }
    public DateOnly SelfEvaluationDueDate { get; set; }
    public DateOnly ReviewDueDate { get; set; }
    public bool IsSelfEvaluationRequired { get; set; }
    public bool IsPeerFeedbackEnabled { get; set; }
    public bool IsCalibrationEnabled { get; set; }
    public CycleStatus CycleStatus { get; set; }
    public int TotalEligible { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
}

public sealed class SaveReviewCycleRequest
{
    [Required(ErrorMessage = "Cycle name is required.")]
    [MaxLength(100, ErrorMessage = "Cycle name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public CycleKind CycleKind { get; set; } = CycleKind.Annual;
    public long RatingScaleId { get; set; }
    public decimal GoalWeightPercent { get; set; } = 50m;
    public decimal CompetencyWeightPercent { get; set; } = 50m;
    public DateOnly GoalSettingDueDate { get; set; }
    public DateOnly SelfEvaluationDueDate { get; set; }
    public DateOnly ReviewDueDate { get; set; }
    public bool IsSelfEvaluationRequired { get; set; } = true;
    public bool IsPeerFeedbackEnabled { get; set; }
    public bool IsCalibrationEnabled { get; set; }
    public List<long> CompetencyIds { get; set; } = [];
}

public sealed class EnrollEmployeesRequest
{
    public DateOnly? JoinedBeforeDate { get; set; }
    public List<long>? DepartmentIds { get; set; }
    public List<long>? SpecificEmployeeIds { get; set; }
}

// ==========================================
// 4. Goals
// ==========================================
public sealed class GoalView
{
    public long GoalId { get; set; }
    public long ReviewCycleId { get; set; }
    public long EmployeeId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Weightage { get; set; }
    public string? Measure { get; set; }
    public string? Target { get; set; }
    public GoalStatus GoalStatus { get; set; }
}

public sealed class SaveGoalRequest
{
    public long ReviewCycleId { get; set; }
    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [Range(0, 100, ErrorMessage = "Weightage must be between 0 and 100.")]
    public decimal Weightage { get; set; }

    [MaxLength(200, ErrorMessage = "Measure cannot exceed 200 characters.")]
    public string? Measure { get; set; }

    [MaxLength(200, ErrorMessage = "Target cannot exceed 200 characters.")]
    public string? Target { get; set; }
}

// ==========================================
// 5. Performance Reviews & Self Evaluation
// ==========================================
public sealed class PerformanceReviewView
{
    public long PerformanceReviewId { get; set; }
    public long ReviewCycleId { get; set; }
    public string CycleName { get; set; } = null!;
    public long EmployeeId { get; set; }
    public string EmployeeName { get; set; } = null!;
    public string EmployeeCode { get; set; } = null!;
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public ReviewStatus ReviewStatus { get; set; }
    public int CurrentStepSequence { get; set; }
    public string? CurrentStepLabel { get; set; }
    public long? CurrentAssigneeEmployeeId { get; set; }
    public DateTimeOffset? SelfSubmittedAt { get; set; }
    public decimal? FinalGoalScore { get; set; }
    public decimal? FinalCompetencyScore { get; set; }
    public decimal? FinalScore { get; set; }
    public long? FinalRatingLevelId { get; set; }
    public string? FinalRatingLabel { get; set; }
    public decimal? RecommendedIncreasePercent { get; set; }
    public bool IsPromotionRecommended { get; set; }
    public long? RecommendedDesignationId { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? EmployeeAcknowledgementComment { get; set; }
}

public sealed class SelfEvaluationView
{
    public long SelfEvaluationId { get; set; }
    public long PerformanceReviewId { get; set; }
    public long? OverallSelfRatingLevelId { get; set; }
    public string Achievements { get; set; } = null!;
    public string? Challenges { get; set; }
    public string? Strengths { get; set; }
    public string? AreasToImprove { get; set; }
    public string? TrainingNeeds { get; set; }
    public string? CareerAspirations { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public List<GoalSelfAssessmentView> Goals { get; set; } = [];
    public List<CompetencySelfAssessmentView> Competencies { get; set; } = [];
}

public sealed class GoalSelfAssessmentView
{
    public long GoalSelfAssessmentId { get; set; }
    public long GoalId { get; set; }
    public string GoalTitle { get; set; } = null!;
    public decimal Weightage { get; set; }
    public long? SelfRatingLevelId { get; set; }
    public string? SelfRatingLabel { get; set; }
    public decimal? AchievementPercent { get; set; }
    public string? Comments { get; set; }
    public List<SelfEvidenceView> Evidences { get; set; } = [];
}

public sealed class SelfEvidenceView
{
    public long SelfEvidenceId { get; set; }
    public string AttachmentKey { get; set; } = null!;
    public string Title { get; set; } = null!;
}

public sealed class CompetencySelfAssessmentView
{
    public long CompetencySelfAssessmentId { get; set; }
    public long CompetencyId { get; set; }
    public string CompetencyName { get; set; } = null!;
    public string GroupName { get; set; } = null!;
    public long? SelfRatingLevelId { get; set; }
    public string? SelfRatingLabel { get; set; }
    public string? Comments { get; set; }
}

public sealed class SaveSelfEvaluationRequest
{
    public long? OverallSelfRatingLevelId { get; set; }

    [Required(ErrorMessage = "Achievements are required.")]
    [MaxLength(4000, ErrorMessage = "Achievements cannot exceed 4000 characters.")]
    public string Achievements { get; set; } = null!;

    [MaxLength(4000, ErrorMessage = "Challenges cannot exceed 4000 characters.")]
    public string? Challenges { get; set; }

    [MaxLength(2000, ErrorMessage = "Strengths cannot exceed 2000 characters.")]
    public string? Strengths { get; set; }

    [MaxLength(2000, ErrorMessage = "Areas to improve cannot exceed 2000 characters.")]
    public string? AreasToImprove { get; set; }

    [MaxLength(2000, ErrorMessage = "Training needs cannot exceed 2000 characters.")]
    public string? TrainingNeeds { get; set; }

    [MaxLength(2000, ErrorMessage = "Career aspirations cannot exceed 2000 characters.")]
    public string? CareerAspirations { get; set; }

    public bool IsSubmit { get; set; }

    public List<SaveGoalAssessmentItem> Goals { get; set; } = [];
    public List<SaveCompetencyAssessmentItem> Competencies { get; set; } = [];
}

public sealed class SaveGoalAssessmentItem
{
    public long GoalId { get; set; }
    public long? SelfRatingLevelId { get; set; }
    public decimal? AchievementPercent { get; set; }
    public string? Comments { get; set; }
    public List<SelfEvidenceView>? Evidences { get; set; }
}

public sealed class SaveCompetencyAssessmentItem
{
    public long CompetencyId { get; set; }
    public long? SelfRatingLevelId { get; set; }
    public string? Comments { get; set; }
}

// ==========================================
// 6. Level Review & Approvals
// ==========================================
public sealed class LevelReviewView
{
    public long LevelReviewId { get; set; }
    public long PerformanceReviewId { get; set; }
    public int Sequence { get; set; }
    public string Label { get; set; } = null!;
    public long ReviewerEmployeeId { get; set; }
    public string? ReviewerName { get; set; }
    public long? RatingLevelId { get; set; }
    public string? RatingLabel { get; set; }
    public decimal? IncreasePercent { get; set; }
    public bool? IsPromotionRecommended { get; set; }
    public long? RecommendedDesignationId { get; set; }
    public string Comments { get; set; } = null!;
    public LevelDecision Decision { get; set; }
    public DateTimeOffset ActedAt { get; set; }
    public List<LevelGoalRatingView> GoalRatings { get; set; } = [];
    public List<LevelCompetencyRatingView> CompetencyRatings { get; set; } = [];
}

public sealed class LevelGoalRatingView
{
    public long GoalId { get; set; }
    public string GoalTitle { get; set; } = null!;
    public long? RatingLevelId { get; set; }
    public decimal? Score { get; set; }
    public string? Comments { get; set; }
}

public sealed class LevelCompetencyRatingView
{
    public long CompetencyId { get; set; }
    public string CompetencyName { get; set; } = null!;
    public long? RatingLevelId { get; set; }
    public decimal? Score { get; set; }
    public string? Comments { get; set; }
}

public sealed class ActLevelReviewRequest
{
    public LevelDecision Decision { get; set; } = LevelDecision.Approved;

    public long? RatingLevelId { get; set; }

    public decimal? IncreasePercent { get; set; }

    public bool? IsPromotionRecommended { get; set; }

    public long? RecommendedDesignationId { get; set; }

    [Required(ErrorMessage = "Comments are required.")]
    [MaxLength(4000, ErrorMessage = "Comments cannot exceed 4000 characters.")]
    public string Comments { get; set; } = null!;

    public List<LevelGoalRatingItem>? GoalRatings { get; set; }

    public List<LevelCompetencyRatingItem>? CompetencyRatings { get; set; }
}

public sealed class LevelGoalRatingItem
{
    public long GoalId { get; set; }
    public long? RatingLevelId { get; set; }
    public decimal? Score { get; set; }
    public string? Comments { get; set; }
}

public sealed class LevelCompetencyRatingItem
{
    public long CompetencyId { get; set; }
    public long? RatingLevelId { get; set; }
    public decimal? Score { get; set; }
    public string? Comments { get; set; }
}

// ==========================================
// 7. Peer Feedback & Calibration
// ==========================================
public sealed class PeerFeedbackView
{
    public long PeerFeedbackId { get; set; }
    public long PerformanceReviewId { get; set; }
    public long ReviewerEmployeeId { get; set; }
    public string? ReviewerName { get; set; }
    public string Comments { get; set; } = null!;
    public long? RatingLevelId { get; set; }
    public bool IsSubmitted { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}

public sealed class SavePeerFeedbackRequest
{
    [Required(ErrorMessage = "Comments are required.")]
    [MaxLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    public string Comments { get; set; } = null!;

    public long? RatingLevelId { get; set; }
}

public sealed class DepartmentCalibrationView
{
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = null!;
    public int TotalReviews { get; set; }
    public List<CalibrationRatingBucket> Buckets { get; set; } = [];
}

public sealed class CalibrationRatingBucket
{
    public long RatingLevelId { get; set; }
    public string Label { get; set; } = null!;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public sealed class CalibrationAdjustmentRequest
{
    public long PerformanceReviewId { get; set; }
    public long ToRatingLevelId { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters.")]
    public string Reason { get; set; } = null!;
}

public sealed class AcknowledgeReviewRequest
{
    [MaxLength(2000, ErrorMessage = "Acknowledgement comment cannot exceed 2000 characters.")]
    public string? Comment { get; set; }
}
