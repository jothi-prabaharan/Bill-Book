using System.ComponentModel.DataAnnotations;
using Performance.Entity.Enums;
using Shared.Kernel.Approvals;
using Shared.Kernel.Tenancy;

namespace Performance.Entity.TableEntities;

public class RatingScale : OrgScopedEntity
{
    public long RatingScaleId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<RatingLevel> Levels { get; set; } = [];
}

public class RatingLevel : OrgScopedEntity
{
    public long RatingLevelId { get; set; }

    public long RatingScaleId { get; set; }

    public int Score { get; set; }

    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string Label { get; set; } = null!;

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(200, ErrorMessage = "Description cannot exceed 200 characters.")]
    public string Description { get; set; } = null!;

    public RatingScale RatingScale { get; set; } = null!;
}

public class CompetencyGroup : OrgScopedEntity
{
    public long CompetencyGroupId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    public List<Competency> Competencies { get; set; } = [];
}

public class Competency : OrgScopedEntity
{
    public long CompetencyId { get; set; }

    public long CompetencyGroupId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    public string Name { get; set; } = null!;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    public CompetencyGroup Group { get; set; } = null!;
}

public class ReviewCycle : OrgScopedEntity
{
    public long ReviewCycleId { get; set; }

    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
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

    public CycleStatus CycleStatus { get; set; } = CycleStatus.Draft;

    public RatingScale RatingScale { get; set; } = null!;

    public List<CycleCompetency> Competencies { get; set; } = [];

    public List<ReviewEligibility> Eligibilities { get; set; } = [];

    public List<PerformanceReview> Reviews { get; set; } = [];
}

public class CycleCompetency : OrgScopedEntity
{
    public long CycleCompetencyId { get; set; }

    public long ReviewCycleId { get; set; }

    public long CompetencyId { get; set; }

    public long? GradeId { get; set; }

    public ReviewCycle ReviewCycle { get; set; } = null!;

    public Competency Competency { get; set; } = null!;
}

public class ReviewEligibility : OrgScopedEntity
{
    public long ReviewEligibilityId { get; set; }

    public long ReviewCycleId { get; set; }

    public long EmployeeId { get; set; }

    public long? DepartmentId { get; set; }

    public long? DesignationId { get; set; }

    public long? GradeId { get; set; }

    public DateOnly? JoinedBeforeDate { get; set; }

    public bool IsIncluded { get; set; } = true;

    public ReviewCycle ReviewCycle { get; set; } = null!;
}

public class Goal : OrgScopedEntity
{
    public long GoalId { get; set; }

    public long ReviewCycleId { get; set; }

    public long EmployeeId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    public decimal Weightage { get; set; }

    [MaxLength(200, ErrorMessage = "Measure cannot exceed 200 characters.")]
    public string? Measure { get; set; }

    [MaxLength(200, ErrorMessage = "Target cannot exceed 200 characters.")]
    public string? Target { get; set; }

    public GoalStatus GoalStatus { get; set; } = GoalStatus.Draft;
}

public class PerformanceReview : OrgScopedEntity
{
    public long PerformanceReviewId { get; set; }

    public long ReviewCycleId { get; set; }

    public long EmployeeId { get; set; }

    public long? DepartmentId { get; set; }

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.NotStarted;

    public int CurrentStepSequence { get; set; }

    public long? CurrentAssigneeEmployeeId { get; set; }

    public DateTimeOffset? SelfSubmittedAt { get; set; }

    public decimal? FinalGoalScore { get; set; }

    public decimal? FinalCompetencyScore { get; set; }

    public decimal? FinalScore { get; set; }

    public long? FinalRatingLevelId { get; set; }

    public decimal? RecommendedIncreasePercent { get; set; }

    public bool IsPromotionRecommended { get; set; }

    public long? RecommendedDesignationId { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    [MaxLength(2000, ErrorMessage = "Acknowledgement comment cannot exceed 2000 characters.")]
    public string? EmployeeAcknowledgementComment { get; set; }

    [MaxLength(100, ErrorMessage = "Approval workflow name cannot exceed 100 characters.")]
    public string? ApprovalWorkflowName { get; set; }

    public ReviewCycle ReviewCycle { get; set; } = null!;

    public SelfEvaluation? SelfEvaluation { get; set; }

    public List<LevelReview> LevelReviews { get; set; } = [];

    public List<PerformanceApprovalStep> ApprovalSteps { get; set; } = [];

    public List<PeerFeedback> PeerFeedbacks { get; set; } = [];

    public List<CalibrationAdjustment> CalibrationAdjustments { get; set; } = [];
}

public class PerformanceApprovalStep : ApprovalStepBase
{
    public long PerformanceApprovalStepId { get; set; }

    public long PerformanceReviewId { get; set; }

    public PerformanceReview PerformanceReview { get; set; } = null!;
}

public class SelfEvaluation : OrgScopedEntity
{
    public long SelfEvaluationId { get; set; }

    public long PerformanceReviewId { get; set; }

    public long? OverallSelfRatingLevelId { get; set; }

    [Required(ErrorMessage = "Achievements description is required.")]
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

    public bool IsSubmitted { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public PerformanceReview PerformanceReview { get; set; } = null!;

    public List<GoalSelfAssessment> GoalAssessments { get; set; } = [];

    public List<CompetencySelfAssessment> CompetencyAssessments { get; set; } = [];
}

public class GoalSelfAssessment : OrgScopedEntity
{
    public long GoalSelfAssessmentId { get; set; }

    public long SelfEvaluationId { get; set; }

    public long GoalId { get; set; }

    public long? SelfRatingLevelId { get; set; }

    public decimal? AchievementPercent { get; set; }

    [MaxLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    public string? Comments { get; set; }

    public SelfEvaluation SelfEvaluation { get; set; } = null!;

    public List<SelfEvidence> Evidences { get; set; } = [];
}

public class SelfEvidence : OrgScopedEntity
{
    public long SelfEvidenceId { get; set; }

    public long GoalSelfAssessmentId { get; set; }

    [Required(ErrorMessage = "Attachment key is required.")]
    [MaxLength(500, ErrorMessage = "Attachment key cannot exceed 500 characters.")]
    public string AttachmentKey { get; set; } = null!;

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    public GoalSelfAssessment GoalSelfAssessment { get; set; } = null!;
}

public class CompetencySelfAssessment : OrgScopedEntity
{
    public long CompetencySelfAssessmentId { get; set; }

    public long SelfEvaluationId { get; set; }

    public long CompetencyId { get; set; }

    public long? SelfRatingLevelId { get; set; }

    [MaxLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters.")]
    public string? Comments { get; set; }

    public SelfEvaluation SelfEvaluation { get; set; } = null!;
}

public class LevelReview : OrgScopedEntity
{
    public long LevelReviewId { get; set; }

    public long PerformanceReviewId { get; set; }

    public long? ApprovalStepId { get; set; }

    public int Sequence { get; set; }

    [Required(ErrorMessage = "Label is required.")]
    [MaxLength(50, ErrorMessage = "Label cannot exceed 50 characters.")]
    public string Label { get; set; } = null!;

    public long ReviewerEmployeeId { get; set; }

    public long? RatingLevelId { get; set; }

    public decimal? IncreasePercent { get; set; }

    public bool? IsPromotionRecommended { get; set; }

    public long? RecommendedDesignationId { get; set; }

    [Required(ErrorMessage = "Comments are required.")]
    [MaxLength(4000, ErrorMessage = "Comments cannot exceed 4000 characters.")]
    public string Comments { get; set; } = null!;

    public LevelDecision Decision { get; set; } = LevelDecision.Approved;

    public DateTimeOffset ActedAt { get; set; }

    public PerformanceReview PerformanceReview { get; set; } = null!;

    public List<LevelGoalRating> GoalRatings { get; set; } = [];

    public List<LevelCompetencyRating> CompetencyRatings { get; set; } = [];
}

public class LevelGoalRating : OrgScopedEntity
{
    public long LevelGoalRatingId { get; set; }

    public long LevelReviewId { get; set; }

    public long GoalId { get; set; }

    public long? RatingLevelId { get; set; }

    public decimal? Score { get; set; }

    [MaxLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters.")]
    public string? Comments { get; set; }

    public LevelReview LevelReview { get; set; } = null!;
}

public class LevelCompetencyRating : OrgScopedEntity
{
    public long LevelCompetencyRatingId { get; set; }

    public long LevelReviewId { get; set; }

    public long CompetencyId { get; set; }

    public long? RatingLevelId { get; set; }

    public decimal? Score { get; set; }

    [MaxLength(1000, ErrorMessage = "Comments cannot exceed 1000 characters.")]
    public string? Comments { get; set; }

    public LevelReview LevelReview { get; set; } = null!;
}

public class PeerFeedback : OrgScopedEntity
{
    public long PeerFeedbackId { get; set; }

    public long PerformanceReviewId { get; set; }

    public long ReviewerEmployeeId { get; set; }

    [Required(ErrorMessage = "Comments are required.")]
    [MaxLength(2000, ErrorMessage = "Comments cannot exceed 2000 characters.")]
    public string Comments { get; set; } = null!;

    public long? RatingLevelId { get; set; }

    public bool IsSubmitted { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    public PerformanceReview PerformanceReview { get; set; } = null!;
}

public class CalibrationAdjustment : OrgScopedEntity
{
    public long CalibrationAdjustmentId { get; set; }

    public long PerformanceReviewId { get; set; }

    public long? FromRatingLevelId { get; set; }

    public long ToRatingLevelId { get; set; }

    [Required(ErrorMessage = "Reason is required.")]
    [MaxLength(1000, ErrorMessage = "Reason cannot exceed 1000 characters.")]
    public string Reason { get; set; } = null!;

    public long AdjustedByEmployeeId { get; set; }

    public DateTimeOffset AdjustedAt { get; set; }

    public PerformanceReview PerformanceReview { get; set; } = null!;
}
