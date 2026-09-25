using System.ComponentModel.DataAnnotations;
using Recruitment.Entity.Enums;

namespace Recruitment.Entity.Models;

// ---- Requisition Models --------------------------------------------------

public class CreateJobRequisitionRequest
{
    public long DepartmentId { get; set; }

    public long DesignationId { get; set; }

    public long GradeId { get; set; }

    public long WorkLocationId { get; set; }

    [Range(1, 1000, ErrorMessage = "Openings must be between 1 and 1000.")]
    public int Openings { get; set; } = 1;

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    [Range(0, 100000000, ErrorMessage = "Min CTC must be positive.")]
    public decimal MinCtc { get; set; }

    [Range(0, 100000000, ErrorMessage = "Max CTC must be positive.")]
    public decimal MaxCtc { get; set; }

    [Required(ErrorMessage = "Justification is required.")]
    [MaxLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters.")]
    public string Justification { get; set; } = null!;

    public bool IsReplacement { get; set; }

    public long? ReplacesEmployeeId { get; set; }
}

public class UpdateJobRequisitionRequest : CreateJobRequisitionRequest
{
}

public class JobRequisitionListItem
{
    public long JobRequisitionId { get; set; }

    public string RequisitionCode { get; set; } = null!;

    public long DepartmentId { get; set; }

    public long DesignationId { get; set; }

    public long GradeId { get; set; }

    public long WorkLocationId { get; set; }

    public int Openings { get; set; }

    public string EmploymentType { get; set; } = null!;

    public decimal MinCtc { get; set; }

    public decimal MaxCtc { get; set; }

    public string Justification { get; set; } = null!;

    public bool IsReplacement { get; set; }

    public string ApprovalStatus { get; set; } = null!;

    public string? CurrentStepLabel { get; set; }

    public int ActiveOpeningsCount { get; set; }
}

// ---- Opening Models ------------------------------------------------------

public class CreateJobOpeningRequest
{
    public long JobRequisitionId { get; set; }

    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(4000, ErrorMessage = "Description cannot exceed 4000 characters.")]
    public string Description { get; set; } = null!;

    public OpeningStatus OpeningStatus { get; set; } = OpeningStatus.Draft;

    public DateOnly? PublishedDate { get; set; }

    public DateOnly? ClosingDate { get; set; }
}

public class UpdateJobOpeningRequest
{
    [Required(ErrorMessage = "Title is required.")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters.")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(4000, ErrorMessage = "Description cannot exceed 4000 characters.")]
    public string Description { get; set; } = null!;

    public OpeningStatus OpeningStatus { get; set; }

    public DateOnly? PublishedDate { get; set; }

    public DateOnly? ClosingDate { get; set; }
}

public class JobOpeningListItem
{
    public long JobOpeningId { get; set; }

    public long JobRequisitionId { get; set; }

    public string RequisitionCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string OpeningStatus { get; set; } = null!;

    public DateOnly? PublishedDate { get; set; }

    public DateOnly? ClosingDate { get; set; }

    public int ApplicationsCount { get; set; }
}

// ---- Candidate Models ----------------------------------------------------

public class CreateCandidateRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100, ErrorMessage = "First name cannot exceed 100 characters.")]
    public string FirstName { get; set; } = null!;

    [MaxLength(100, ErrorMessage = "Last name cannot exceed 100 characters.")]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [MaxLength(255, ErrorMessage = "Email cannot exceed 255 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Phone is required.")]
    [MaxLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
    public string Phone { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "Current employer cannot exceed 200 characters.")]
    public string? CurrentEmployer { get; set; }

    public decimal? CurrentCtc { get; set; }

    public decimal? ExpectedCtc { get; set; }

    [Range(0, 365, ErrorMessage = "Notice period must be between 0 and 365 days.")]
    public int? NoticePeriodDays { get; set; }

    public CandidateSource CandidateSource { get; set; } = CandidateSource.Portal;

    public long? ReferredByEmployeeId { get; set; }

    [MaxLength(500, ErrorMessage = "Resume attachment key cannot exceed 500 characters.")]
    public string? ResumeAttachmentKey { get; set; }
}

public class UpdateCandidateRequest : CreateCandidateRequest
{
}

public class CandidateListItem
{
    public long CandidateId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? CurrentEmployer { get; set; }

    public decimal? CurrentCtc { get; set; }

    public decimal? ExpectedCtc { get; set; }

    public int? NoticePeriodDays { get; set; }

    public string CandidateSource { get; set; } = null!;

    public string? ResumeAttachmentKey { get; set; }

    public int ApplicationsCount { get; set; }
}

// ---- Application & Pipeline Models ---------------------------------------

public class CreateApplicationRequest
{
    public long JobOpeningId { get; set; }

    public long CandidateId { get; set; }
}

public class UpdateApplicationStageRequest
{
    public ApplicationStage Stage { get; set; }

    [MaxLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters.")]
    public string? RejectionReason { get; set; }
}

public class ApplicationListItem
{
    public long ApplicationId { get; set; }

    public long JobOpeningId { get; set; }

    public string OpeningTitle { get; set; } = null!;

    public long CandidateId { get; set; }

    public string CandidateName { get; set; } = null!;

    public string CandidateEmail { get; set; } = null!;

    public string CandidatePhone { get; set; } = null!;

    public string Stage { get; set; } = null!;

    public string? RejectionReason { get; set; }

    public int InterviewRoundsCount { get; set; }

    public bool HasOffer { get; set; }

    public string? OfferStatus { get; set; }
}

// ---- Interview Round Models ----------------------------------------------

public class ScheduleInterviewRoundRequest
{
    public long ApplicationId { get; set; }

    public int RoundNo { get; set; }

    public RoundKind RoundKind { get; set; } = RoundKind.Technical;

    public DateTimeOffset ScheduledAt { get; set; }

    public long InterviewerEmployeeId { get; set; }
}

public class UpdateInterviewFeedbackRequest
{
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int? Rating { get; set; }

    [MaxLength(2000, ErrorMessage = "Feedback cannot exceed 2000 characters.")]
    public string? Feedback { get; set; }

    public InterviewOutcome Outcome { get; set; }
}

public class InterviewRoundView
{
    public long InterviewRoundId { get; set; }

    public long ApplicationId { get; set; }

    public int RoundNo { get; set; }

    public string RoundKind { get; set; } = null!;

    public DateTimeOffset ScheduledAt { get; set; }

    public long InterviewerEmployeeId { get; set; }

    public int? Rating { get; set; }

    public string? Feedback { get; set; }

    public string Outcome { get; set; } = null!;
}

// ---- Offer Models --------------------------------------------------------

public class CreateOfferRequest
{
    public long ApplicationId { get; set; }

    [Range(1, 100000000, ErrorMessage = "Offered CTC must be greater than zero.")]
    public decimal OfferedCtc { get; set; }

    public long SalaryStructureId { get; set; }

    public DateOnly JoiningDate { get; set; }
}

public class OfferView
{
    public long OfferId { get; set; }

    public long ApplicationId { get; set; }

    public decimal OfferedCtc { get; set; }

    public long SalaryStructureId { get; set; }

    public DateOnly JoiningDate { get; set; }

    public string OfferStatus { get; set; } = null!;

    public string ApprovalStatus { get; set; } = null!;

    public string? CurrentStepLabel { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public long? CreatedEmployeeId { get; set; }

    public string? CandidateName { get; set; }

    public string? OpeningTitle { get; set; }
}

public class AcceptOfferResult
{
    public long OfferId { get; set; }

    public long EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public bool AlreadyExisted { get; set; }

    public string Message { get; set; } = null!;
}
