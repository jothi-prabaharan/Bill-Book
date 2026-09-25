using Microsoft.EntityFrameworkCore;
using Recruitment.Entity.Enums;
using Recruitment.Entity.Models;
using Recruitment.Entity.TableEntities;
using Recruitment.Repository;
using Shared.Kernel.Employees;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;

namespace Recruitment.Api.Services;

public sealed class RecruitmentService
{
    private readonly RecruitmentDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly INumberGenerator _numbers;
    private readonly IEmployeeClient _hrmClient;
    private readonly IPayrollClient _payrollClient;

    public RecruitmentService(
        RecruitmentDbContext db,
        ITenantContext tenant,
        INumberGenerator numbers,
        IEmployeeClient hrmClient,
        IPayrollClient payrollClient)
    {
        _db = db;
        _tenant = tenant;
        _numbers = numbers;
        _hrmClient = hrmClient;
        _payrollClient = payrollClient;
    }

    // ---- Requisitions --------------------------------------------------------

    public async Task<List<JobRequisitionListItem>> ListRequisitionsAsync(int page, int pageSize, CancellationToken ct)
    {
        return await _db.JobRequisitions.AsNoTracking()
            .OrderByDescending(r => r.JobRequisitionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new JobRequisitionListItem
            {
                JobRequisitionId = r.JobRequisitionId,
                RequisitionCode = r.RequisitionCode,
                DepartmentId = r.DepartmentId,
                DesignationId = r.DesignationId,
                GradeId = r.GradeId,
                WorkLocationId = r.WorkLocationId,
                Openings = r.Openings,
                EmploymentType = r.EmploymentType.ToString(),
                MinCtc = r.MinCtc,
                MaxCtc = r.MaxCtc,
                Justification = r.Justification,
                IsReplacement = r.IsReplacement,
                ApprovalStatus = r.ApprovalStatus.ToString(),
                CurrentStepLabel = r.CurrentStepLabel,
                ActiveOpeningsCount = r.OpeningsList.Count(o => o.OpeningStatus == OpeningStatus.Open),
            })
            .ToListAsync(ct);
    }

    public async Task<JobRequisition?> GetRequisitionByIdAsync(long id, CancellationToken ct) =>
        await _db.JobRequisitions.AsNoTracking()
            .Include(r => r.OpeningsList)
            .FirstOrDefaultAsync(r => r.JobRequisitionId == id, ct);

    public async Task<JobRequisition> CreateRequisitionAsync(CreateJobRequisitionRequest request, CancellationToken ct)
    {
        var alloc = await _numbers.NextAsync("REQ", DateOnly.FromDateTime(DateTime.UtcNow), ct);

        var req = new JobRequisition
        {
            RequisitionCode = alloc.Code,
            DepartmentId = request.DepartmentId,
            DesignationId = request.DesignationId,
            GradeId = request.GradeId,
            WorkLocationId = request.WorkLocationId,
            Openings = request.Openings,
            EmploymentType = request.EmploymentType,
            MinCtc = request.MinCtc,
            MaxCtc = request.MaxCtc,
            Justification = request.Justification,
            IsReplacement = request.IsReplacement,
            ReplacesEmployeeId = request.ReplacesEmployeeId,
            ApprovalStatus = ApprovalStatus.Draft,
        };

        _db.JobRequisitions.Add(req);
        await _db.SaveChangesAsync(ct);
        return req;
    }

    public async Task<JobRequisition?> UpdateRequisitionAsync(long id, UpdateJobRequisitionRequest request, CancellationToken ct)
    {
        var req = await _db.JobRequisitions.FirstOrDefaultAsync(r => r.JobRequisitionId == id, ct);
        if (req is null) return null;

        if (req.ApprovalStatus != ApprovalStatus.Draft)
            throw new InvalidOperationException("Cannot update requisition that is already submitted or approved.");

        req.DepartmentId = request.DepartmentId;
        req.DesignationId = request.DesignationId;
        req.GradeId = request.GradeId;
        req.WorkLocationId = request.WorkLocationId;
        req.Openings = request.Openings;
        req.EmploymentType = request.EmploymentType;
        req.MinCtc = request.MinCtc;
        req.MaxCtc = request.MaxCtc;
        req.Justification = request.Justification;
        req.IsReplacement = request.IsReplacement;
        req.ReplacesEmployeeId = request.ReplacesEmployeeId;

        await _db.SaveChangesAsync(ct);
        return req;
    }

    public async Task<bool> SubmitRequisitionAsync(long id, CancellationToken ct)
    {
        var req = await _db.JobRequisitions.FirstOrDefaultAsync(r => r.JobRequisitionId == id, ct);
        if (req is null) return false;
        req.ApprovalStatus = ApprovalStatus.PendingApproval;
        req.CurrentStepLabel = "Manager Review";
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ApproveRequisitionAsync(long id, CancellationToken ct)
    {
        var req = await _db.JobRequisitions.FirstOrDefaultAsync(r => r.JobRequisitionId == id, ct);
        if (req is null) return false;
        req.ApprovalStatus = ApprovalStatus.Approved;
        req.CurrentStepLabel = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RejectRequisitionAsync(long id, CancellationToken ct)
    {
        var req = await _db.JobRequisitions.FirstOrDefaultAsync(r => r.JobRequisitionId == id, ct);
        if (req is null) return false;
        req.ApprovalStatus = ApprovalStatus.Rejected;
        req.CurrentStepLabel = null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Openings ------------------------------------------------------------

    public async Task<List<JobOpeningListItem>> ListOpeningsAsync(int page, int pageSize, OpeningStatus? status, CancellationToken ct)
    {
        var q = _db.JobOpenings.AsNoTracking().AsQueryable();
        if (status.HasValue) q = q.Where(o => o.OpeningStatus == status.Value);

        return await q.OrderByDescending(o => o.JobOpeningId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new JobOpeningListItem
            {
                JobOpeningId = o.JobOpeningId,
                JobRequisitionId = o.JobRequisitionId,
                RequisitionCode = o.Requisition.RequisitionCode,
                Title = o.Title,
                Description = o.Description,
                OpeningStatus = o.OpeningStatus.ToString(),
                PublishedDate = o.PublishedDate,
                ClosingDate = o.ClosingDate,
                ApplicationsCount = o.Applications.Count,
            })
            .ToListAsync(ct);
    }

    public async Task<JobOpening?> GetOpeningByIdAsync(long id, CancellationToken ct) =>
        await _db.JobOpenings.AsNoTracking()
            .Include(o => o.Requisition)
            .Include(o => o.Applications)
            .FirstOrDefaultAsync(o => o.JobOpeningId == id, ct);

    public async Task<JobOpening> CreateOpeningAsync(CreateJobOpeningRequest request, CancellationToken ct)
    {
        var opening = new JobOpening
        {
            JobRequisitionId = request.JobRequisitionId,
            Title = request.Title,
            Description = request.Description,
            OpeningStatus = request.OpeningStatus,
            PublishedDate = request.PublishedDate ?? (request.OpeningStatus == OpeningStatus.Open ? DateOnly.FromDateTime(DateTime.UtcNow) : null),
            ClosingDate = request.ClosingDate,
        };

        _db.JobOpenings.Add(opening);
        await _db.SaveChangesAsync(ct);
        return opening;
    }

    public async Task<JobOpening?> UpdateOpeningAsync(long id, UpdateJobOpeningRequest request, CancellationToken ct)
    {
        var opening = await _db.JobOpenings.FirstOrDefaultAsync(o => o.JobOpeningId == id, ct);
        if (opening is null) return null;

        opening.Title = request.Title;
        opening.Description = request.Description;
        opening.OpeningStatus = request.OpeningStatus;
        opening.PublishedDate = request.PublishedDate;
        opening.ClosingDate = request.ClosingDate;

        await _db.SaveChangesAsync(ct);
        return opening;
    }

    // ---- Candidates ----------------------------------------------------------

    public async Task<List<CandidateListItem>> ListCandidatesAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        var q = _db.Candidates.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            q = q.Where(c => c.FirstName.ToLower().Contains(term)
                          || (c.LastName != null && c.LastName.ToLower().Contains(term))
                          || c.Email.ToLower().Contains(term)
                          || c.Phone.Contains(term));
        }

        return await q.OrderByDescending(c => c.CandidateId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CandidateListItem
            {
                CandidateId = c.CandidateId,
                FullName = c.FirstName + (c.LastName == null ? "" : " " + c.LastName),
                Email = c.Email,
                Phone = c.Phone,
                CurrentEmployer = c.CurrentEmployer,
                CurrentCtc = c.CurrentCtc,
                ExpectedCtc = c.ExpectedCtc,
                NoticePeriodDays = c.NoticePeriodDays,
                CandidateSource = c.CandidateSource.ToString(),
                ResumeAttachmentKey = c.ResumeAttachmentKey,
                ApplicationsCount = c.Applications.Count,
            })
            .ToListAsync(ct);
    }

    public async Task<Candidate?> GetCandidateByIdAsync(long id, CancellationToken ct) =>
        await _db.Candidates.AsNoTracking()
            .Include(c => c.Applications)
                .ThenInclude(a => a.JobOpening)
            .FirstOrDefaultAsync(c => c.CandidateId == id, ct);

    public async Task<Candidate> CreateCandidateAsync(CreateCandidateRequest request, CancellationToken ct)
    {
        var emailLower = request.Email.Trim().ToLower();
        var exists = await _db.Candidates.AnyAsync(c => c.Email.ToLower() == emailLower, ct);
        if (exists) throw new InvalidOperationException($"Candidate with email '{request.Email}' already exists.");

        var candidate = new Candidate
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName?.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            CurrentEmployer = request.CurrentEmployer?.Trim(),
            CurrentCtc = request.CurrentCtc,
            ExpectedCtc = request.ExpectedCtc,
            NoticePeriodDays = request.NoticePeriodDays,
            CandidateSource = request.CandidateSource,
            ReferredByEmployeeId = request.ReferredByEmployeeId,
            ResumeAttachmentKey = request.ResumeAttachmentKey?.Trim(),
        };

        _db.Candidates.Add(candidate);
        await _db.SaveChangesAsync(ct);
        return candidate;
    }

    public async Task<Candidate?> UpdateCandidateAsync(long id, UpdateCandidateRequest request, CancellationToken ct)
    {
        var candidate = await _db.Candidates.FirstOrDefaultAsync(c => c.CandidateId == id, ct);
        if (candidate is null) return null;

        var emailLower = request.Email.Trim().ToLower();
        var exists = await _db.Candidates.AnyAsync(c => c.CandidateId != id && c.Email.ToLower() == emailLower, ct);
        if (exists) throw new InvalidOperationException($"Candidate with email '{request.Email}' already exists.");

        candidate.FirstName = request.FirstName.Trim();
        candidate.LastName = request.LastName?.Trim();
        candidate.Email = request.Email.Trim();
        candidate.Phone = request.Phone.Trim();
        candidate.CurrentEmployer = request.CurrentEmployer?.Trim();
        candidate.CurrentCtc = request.CurrentCtc;
        candidate.ExpectedCtc = request.ExpectedCtc;
        candidate.NoticePeriodDays = request.NoticePeriodDays;
        candidate.CandidateSource = request.CandidateSource;
        candidate.ReferredByEmployeeId = request.ReferredByEmployeeId;
        candidate.ResumeAttachmentKey = request.ResumeAttachmentKey?.Trim();

        await _db.SaveChangesAsync(ct);
        return candidate;
    }

    // ---- Applications & Pipeline ---------------------------------------------

    public async Task<List<ApplicationListItem>> ListApplicationsAsync(int page, int pageSize, long? openingId, ApplicationStage? stage, CancellationToken ct)
    {
        var q = _db.Applications.AsNoTracking().AsQueryable();
        if (openingId.HasValue) q = q.Where(a => a.JobOpeningId == openingId.Value);
        if (stage.HasValue) q = q.Where(a => a.Stage == stage.Value);

        return await q.OrderByDescending(a => a.ApplicationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new ApplicationListItem
            {
                ApplicationId = a.ApplicationId,
                JobOpeningId = a.JobOpeningId,
                OpeningTitle = a.JobOpening.Title,
                CandidateId = a.CandidateId,
                CandidateName = a.Candidate.FirstName + (a.Candidate.LastName == null ? "" : " " + a.Candidate.LastName),
                CandidateEmail = a.Candidate.Email,
                CandidatePhone = a.Candidate.Phone,
                Stage = a.Stage.ToString(),
                RejectionReason = a.RejectionReason,
                InterviewRoundsCount = a.InterviewRounds.Count,
                HasOffer = a.Offers.Any(),
                OfferStatus = a.Offers.OrderByDescending(o => o.OfferId).Select(o => o.OfferStatus.ToString()).FirstOrDefault(),
            })
            .ToListAsync(ct);
    }

    public async Task<Application?> GetApplicationByIdAsync(long id, CancellationToken ct) =>
        await _db.Applications.AsNoTracking()
            .Include(a => a.JobOpening)
            .Include(a => a.Candidate)
            .Include(a => a.InterviewRounds)
            .Include(a => a.Offers)
            .FirstOrDefaultAsync(a => a.ApplicationId == id, ct);

    public async Task<Application> CreateApplicationAsync(CreateApplicationRequest request, CancellationToken ct)
    {
        var exists = await _db.Applications.AnyAsync(a => a.JobOpeningId == request.JobOpeningId && a.CandidateId == request.CandidateId, ct);
        if (exists) throw new InvalidOperationException("Candidate has already applied to this opening.");

        var app = new Application
        {
            JobOpeningId = request.JobOpeningId,
            CandidateId = request.CandidateId,
            Stage = ApplicationStage.Applied,
        };

        _db.Applications.Add(app);
        await _db.SaveChangesAsync(ct);
        return app;
    }

    public async Task<bool> UpdateApplicationStageAsync(long id, UpdateApplicationStageRequest request, CancellationToken ct)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.ApplicationId == id, ct);
        if (app is null) return false;

        app.Stage = request.Stage;
        if (request.Stage == ApplicationStage.Rejected)
        {
            app.RejectionReason = request.RejectionReason;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ---- Interviews ----------------------------------------------------------

    public async Task<InterviewRound> ScheduleInterviewRoundAsync(ScheduleInterviewRoundRequest request, CancellationToken ct)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.ApplicationId == request.ApplicationId, ct)
            ?? throw new KeyNotFoundException($"Application {request.ApplicationId} not found.");

        var round = new InterviewRound
        {
            ApplicationId = request.ApplicationId,
            RoundNo = request.RoundNo,
            RoundKind = request.RoundKind,
            ScheduledAt = request.ScheduledAt,
            InterviewerEmployeeId = request.InterviewerEmployeeId,
            Outcome = InterviewOutcome.Pending,
        };

        _db.InterviewRounds.Add(round);
        app.Stage = ApplicationStage.Interview;
        await _db.SaveChangesAsync(ct);
        return round;
    }

    public async Task<bool> UpdateInterviewFeedbackAsync(long roundId, UpdateInterviewFeedbackRequest request, CancellationToken ct)
    {
        var round = await _db.InterviewRounds.FirstOrDefaultAsync(r => r.InterviewRoundId == roundId, ct);
        if (round is null) return false;

        round.Rating = request.Rating;
        round.Feedback = request.Feedback;
        round.Outcome = request.Outcome;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<InterviewRoundView>> ListInterviewsByApplicationAsync(long applicationId, CancellationToken ct)
    {
        return await _db.InterviewRounds.AsNoTracking()
            .Where(r => r.ApplicationId == applicationId)
            .OrderBy(r => r.RoundNo)
            .Select(r => new InterviewRoundView
            {
                InterviewRoundId = r.InterviewRoundId,
                ApplicationId = r.ApplicationId,
                RoundNo = r.RoundNo,
                RoundKind = r.RoundKind.ToString(),
                ScheduledAt = r.ScheduledAt,
                InterviewerEmployeeId = r.InterviewerEmployeeId,
                Rating = r.Rating,
                Feedback = r.Feedback,
                Outcome = r.Outcome.ToString(),
            })
            .ToListAsync(ct);
    }

    // ---- Offers & Idempotent Acceptance --------------------------------------

    public async Task<OfferView?> GetOfferByIdAsync(long id, CancellationToken ct)
    {
        return await _db.Offers.AsNoTracking()
            .Where(o => o.OfferId == id)
            .Select(o => new OfferView
            {
                OfferId = o.OfferId,
                ApplicationId = o.ApplicationId,
                OfferedCtc = o.OfferedCtc,
                SalaryStructureId = o.SalaryStructureId,
                JoiningDate = o.JoiningDate,
                OfferStatus = o.OfferStatus.ToString(),
                ApprovalStatus = o.ApprovalStatus.ToString(),
                CurrentStepLabel = o.CurrentStepLabel,
                AcceptedAt = o.AcceptedAt,
                CreatedEmployeeId = o.CreatedEmployeeId,
                CandidateName = o.Application.Candidate.FirstName + (o.Application.Candidate.LastName == null ? "" : " " + o.Application.Candidate.LastName),
                OpeningTitle = o.Application.JobOpening.Title,
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<OfferView>> ListOffersAsync(long? applicationId, CancellationToken ct)
    {
        var q = _db.Offers.AsNoTracking().AsQueryable();
        if (applicationId.HasValue) q = q.Where(o => o.ApplicationId == applicationId.Value);

        return await q.OrderByDescending(o => o.OfferId)
            .Select(o => new OfferView
            {
                OfferId = o.OfferId,
                ApplicationId = o.ApplicationId,
                OfferedCtc = o.OfferedCtc,
                SalaryStructureId = o.SalaryStructureId,
                JoiningDate = o.JoiningDate,
                OfferStatus = o.OfferStatus.ToString(),
                ApprovalStatus = o.ApprovalStatus.ToString(),
                CurrentStepLabel = o.CurrentStepLabel,
                AcceptedAt = o.AcceptedAt,
                CreatedEmployeeId = o.CreatedEmployeeId,
                CandidateName = o.Application.Candidate.FirstName + (o.Application.Candidate.LastName == null ? "" : " " + o.Application.Candidate.LastName),
                OpeningTitle = o.Application.JobOpening.Title,
            })
            .ToListAsync(ct);
    }

    public async Task<Offer> CreateOfferAsync(CreateOfferRequest request, CancellationToken ct)
    {
        var app = await _db.Applications.FirstOrDefaultAsync(a => a.ApplicationId == request.ApplicationId, ct)
            ?? throw new KeyNotFoundException($"Application {request.ApplicationId} not found.");

        var offer = new Offer
        {
            ApplicationId = request.ApplicationId,
            OfferedCtc = request.OfferedCtc,
            SalaryStructureId = request.SalaryStructureId,
            JoiningDate = request.JoiningDate,
            OfferStatus = OfferStatus.Draft,
            ApprovalStatus = ApprovalStatus.Draft,
        };

        _db.Offers.Add(offer);
        app.Stage = ApplicationStage.Offer;
        await _db.SaveChangesAsync(ct);
        return offer;
    }

    public async Task<bool> ApproveOfferAsync(long id, CancellationToken ct)
    {
        var offer = await _db.Offers.FirstOrDefaultAsync(o => o.OfferId == id, ct);
        if (offer is null) return false;
        offer.ApprovalStatus = ApprovalStatus.Approved;
        offer.OfferStatus = OfferStatus.Approved;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SendOfferAsync(long id, CancellationToken ct)
    {
        var offer = await _db.Offers.FirstOrDefaultAsync(o => o.OfferId == id, ct);
        if (offer is null) return false;
        offer.OfferStatus = OfferStatus.Sent;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeclineOfferAsync(long id, CancellationToken ct)
    {
        var offer = await _db.Offers.FirstOrDefaultAsync(o => o.OfferId == id, ct);
        if (offer is null) return false;
        offer.OfferStatus = OfferStatus.Declined;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RevokeOfferAsync(long id, CancellationToken ct)
    {
        var offer = await _db.Offers.FirstOrDefaultAsync(o => o.OfferId == id, ct);
        if (offer is null) return false;
        offer.OfferStatus = OfferStatus.Revoked;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Accepts an offer and provisions the employee in Employee and salary in Payroll.
    /// Idempotency guarantee: Accepting the same offer multiple times returns
    /// the single created employee without creating duplicates.
    /// </summary>
    public async Task<AcceptOfferResult> AcceptOfferAsync(long offerId, CancellationToken ct)
    {
        var offer = await _db.Offers
            .Include(o => o.Application)
                .ThenInclude(a => a.Candidate)
            .Include(o => o.Application)
                .ThenInclude(a => a.JobOpening)
                    .ThenInclude(j => j.Requisition)
            .FirstOrDefaultAsync(o => o.OfferId == offerId, ct)
            ?? throw new KeyNotFoundException($"Offer {offerId} not found.");

        // Idempotency check 1: if already accepted and created, return immediately
        if (offer.OfferStatus == OfferStatus.Accepted && offer.CreatedEmployeeId.HasValue)
        {
            var existingProfile = await _hrmClient.FindByIdAsync(
                _tenant.CustomerId ?? Guid.Empty,
                _tenant.OrgId ?? Guid.Empty,
                offer.CreatedEmployeeId.Value,
                ct);

            return new AcceptOfferResult
            {
                OfferId = offer.OfferId,
                EmployeeId = offer.CreatedEmployeeId.Value,
                EmployeeCode = existingProfile?.EmployeeCode ?? $"EMP-{offer.CreatedEmployeeId.Value}",
                AlreadyExisted = true,
                Message = "Offer was already accepted. Returned existing employee.",
            };
        }

        var candidate = offer.Application.Candidate;
        var req = offer.Application.JobOpening.Requisition;

        var onboardReq = new OnboardEmployeeRequest
        {
            CustomerId = _tenant.CustomerId ?? Guid.Empty,
            OrgId = _tenant.OrgId ?? Guid.Empty,
            OfferId = offer.OfferId,
            FirstName = candidate.FirstName,
            LastName = candidate.LastName,
            Email = candidate.Email,
            Phone = candidate.Phone,
            DepartmentId = req.DepartmentId,
            DesignationId = req.DesignationId,
            GradeId = req.GradeId,
            WorkLocationId = req.WorkLocationId,
            JoiningDate = offer.JoiningDate,
            EmploymentType = req.EmploymentType.ToString(),
            Remarks = $"Onboarded via Offer #{offer.OfferId} for {offer.Application.JobOpening.Title}",
        };

        var hrmResult = await _hrmClient.OnboardEmployeeAsync(onboardReq, ct)
            ?? throw new InvalidOperationException("Failed to provision employee in HRM service.");

        // Assign salary in Payroll service
        await _payrollClient.AssignSalaryAsync(
            hrmResult.EmployeeId,
            offer.SalaryStructureId,
            offer.OfferedCtc,
            offer.JoiningDate,
            ct);

        // Update offer and application records
        offer.OfferStatus = OfferStatus.Accepted;
        offer.AcceptedAt = DateTimeOffset.UtcNow;
        offer.CreatedEmployeeId = hrmResult.EmployeeId;
        offer.Application.Stage = ApplicationStage.Hired;

        await _db.SaveChangesAsync(ct);

        return new AcceptOfferResult
        {
            OfferId = offer.OfferId,
            EmployeeId = hrmResult.EmployeeId,
            EmployeeCode = hrmResult.EmployeeCode,
            AlreadyExisted = hrmResult.AlreadyExisted,
            Message = hrmResult.AlreadyExisted
                ? "Offer accepted. Employee already existed in HRM."
                : "Offer accepted. New employee onboarded successfully.",
        };
    }
}
