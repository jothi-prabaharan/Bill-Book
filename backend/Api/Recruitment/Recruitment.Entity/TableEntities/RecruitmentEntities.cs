using System.ComponentModel.DataAnnotations;
using Recruitment.Entity.Enums;
using Shared.Kernel.Tenancy;

namespace Recruitment.Entity.TableEntities;

public class JobRequisition : OrgScopedEntity
{
    public long JobRequisitionId { get; set; }

    [Required(ErrorMessage = "Requisition code is required.")]
    [MaxLength(30, ErrorMessage = "Requisition code cannot exceed 30 characters.")]
    public string RequisitionCode { get; set; } = null!;

    public long DepartmentId { get; set; }

    public long DesignationId { get; set; }

    public long GradeId { get; set; }

    public long WorkLocationId { get; set; }

    [Range(1, 1000, ErrorMessage = "Openings count must be at least 1.")]
    public int Openings { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;

    public decimal MinCtc { get; set; }

    public decimal MaxCtc { get; set; }

    [Required(ErrorMessage = "Justification is required.")]
    [MaxLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters.")]
    public string Justification { get; set; } = null!;

    public bool IsReplacement { get; set; }

    public long? ReplacesEmployeeId { get; set; }

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    [MaxLength(100, ErrorMessage = "Current step label cannot exceed 100 characters.")]
    public string? CurrentStepLabel { get; set; }

    public long? CurrentApproverEmployeeId { get; set; }

    public ICollection<JobOpening> OpeningsList { get; set; } = [];
}

public class JobOpening : OrgScopedEntity
{
    public long JobOpeningId { get; set; }

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

    public JobRequisition Requisition { get; set; } = null!;

    public ICollection<Application> Applications { get; set; } = [];
}

public class Candidate : OrgScopedEntity
{
    public long CandidateId { get; set; }

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

    public ICollection<Application> Applications { get; set; } = [];
}

public class Application : OrgScopedEntity
{
    public long ApplicationId { get; set; }

    public long JobOpeningId { get; set; }

    public long CandidateId { get; set; }

    public ApplicationStage Stage { get; set; } = ApplicationStage.Applied;

    [MaxLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters.")]
    public string? RejectionReason { get; set; }

    public JobOpening JobOpening { get; set; } = null!;

    public Candidate Candidate { get; set; } = null!;

    public ICollection<InterviewRound> InterviewRounds { get; set; } = [];

    public ICollection<Offer> Offers { get; set; } = [];
}

public class InterviewRound : OrgScopedEntity
{
    public long InterviewRoundId { get; set; }

    public long ApplicationId { get; set; }

    public int RoundNo { get; set; }

    public RoundKind RoundKind { get; set; } = RoundKind.Technical;

    public DateTimeOffset ScheduledAt { get; set; }

    public long InterviewerEmployeeId { get; set; }

    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int? Rating { get; set; }

    [MaxLength(2000, ErrorMessage = "Feedback cannot exceed 2000 characters.")]
    public string? Feedback { get; set; }

    public InterviewOutcome Outcome { get; set; } = InterviewOutcome.Pending;

    public Application Application { get; set; } = null!;
}

public class Offer : OrgScopedEntity
{
    public long OfferId { get; set; }

    public long ApplicationId { get; set; }

    public decimal OfferedCtc { get; set; }

    public long SalaryStructureId { get; set; }

    public DateOnly JoiningDate { get; set; }

    public OfferStatus OfferStatus { get; set; } = OfferStatus.Draft;

    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;

    [MaxLength(100, ErrorMessage = "Current step label cannot exceed 100 characters.")]
    public string? CurrentStepLabel { get; set; }

    public long? CurrentApproverEmployeeId { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public long? CreatedEmployeeId { get; set; }

    public Application Application { get; set; } = null!;
}
