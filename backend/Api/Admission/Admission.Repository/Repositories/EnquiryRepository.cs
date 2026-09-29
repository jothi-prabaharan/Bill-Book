using Admission.Entity.Enums;
using Admission.Entity.Models;
using Admission.Entity.TableEntities;
using Admission.Repository;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.School;
using Shared.Kernel.Validation;

namespace Admission.Repository.Repositories;

/// <summary>Enquiries (S2, TK-62): a parent asking about a seat, followed up, then converted or lost.</summary>
public sealed class EnquiryRepository
{
    private readonly AdmissionDbContext _db;
    private readonly IStudentClient _sis;
    private readonly ILogger<EnquiryRepository> _log;

    public EnquiryRepository(AdmissionDbContext db, IStudentClient sis, ILogger<EnquiryRepository> log)
    {
        _db = db;
        _sis = sis;
        _log = log;
    }

    public async Task<List<EnquiryView>> ListAsync(EnquiryStatus? status, CancellationToken ct) =>
        await _db.Enquiries.AsNoTracking()
            .Where(e => status == null || e.EnquiryStatus == status)
            .OrderBy(e => e.FollowUpDate ?? e.EnquiryDate).ThenByDescending(e => e.EnquiryId)
            .Take(1000)
            .Select(e => new EnquiryView
            {
                EnquiryId = e.EnquiryId, EnquiryDate = e.EnquiryDate, ChildName = e.ChildName, DateOfBirth = e.DateOfBirth,
                SeekingClassId = e.SeekingClassId, AcademicYearId = e.AcademicYearId, ParentName = e.ParentName,
                Phone = e.Phone, Email = e.Email, EnquirySource = e.EnquirySource, EnquiryStatus = e.EnquiryStatus,
                FollowUpDate = e.FollowUpDate,
            })
            .ToListAsync(ct);

    public async Task<AdmissionResultRepository> SaveAsync(long? id, SaveEnquiryRequest request, CancellationToken ct)
    {
        if (request.EnquiryStatus == EnquiryStatus.FollowUp && request.FollowUpDate is null)
        {
            return AdmissionResultRepository.Fail(AdmissionOutcome.Invalid, "An enquiry to follow up needs a follow-up date.");
        }

        if (request.EnquiryStatus == EnquiryStatus.Converted)
        {
            return AdmissionResultRepository.Fail(AdmissionOutcome.StageRule, "An enquiry is converted by making its application.");
        }

        AdmissionResultRepository? academic = await CheckAcademicAsync(_sis, _log, request.AcademicYearId, request.SeekingClassId, ct);
        if (academic is not null)
        {
            return academic;
        }

        Enquiry? enquiry = id is long existing
            ? await _db.Enquiries.FirstOrDefaultAsync(e => e.EnquiryId == existing, ct)
            : new Enquiry();
        if (enquiry is null)
        {
            return AdmissionResultRepository.Fail(AdmissionOutcome.NotFound);
        }

        if (enquiry.EnquiryStatus == EnquiryStatus.Converted)
        {
            return AdmissionResultRepository.Fail(AdmissionOutcome.StageRule, "A converted enquiry is kept as it was; change its application instead.");
        }

        enquiry.EnquiryDate = request.EnquiryDate;
        enquiry.ChildName = request.ChildName.Trim();
        enquiry.DateOfBirth = request.DateOfBirth;
        enquiry.SeekingClassId = request.SeekingClassId;
        enquiry.AcademicYearId = request.AcademicYearId;
        enquiry.ParentName = request.ParentName.Trim();
        enquiry.Phone = PhoneNumbers.NormalizeOptional(request.Phone) ?? request.Phone.Trim();
        enquiry.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        enquiry.EnquirySource = request.EnquirySource;
        enquiry.EnquiryStatus = request.EnquiryStatus;
        enquiry.FollowUpDate = request.FollowUpDate;

        if (id is null)
        {
            _db.Enquiries.Add(enquiry);
        }

        await _db.SaveChangesAsync(ct);
        return AdmissionResultRepository.Ok(enquiry.EnquiryId);
    }

    /// <summary>Whether the year and class are Student's, in this branch, with the year open. Null when fine.</summary>
    internal static async Task<AdmissionResultRepository?> CheckAcademicAsync(IStudentClient sis, ILogger log, long yearId, long classId, CancellationToken ct)
    {
        AcademicCheckResponse check;
        try
        {
            check = await sis.CheckAsync(yearId, classId, null, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogError(ex, "The school year and class could not be checked.");
            return AdmissionResultRepository.Fail(AdmissionOutcome.Unavailable);
        }

        if (!check.YearExists || !check.ClassExists)
        {
            return AdmissionResultRepository.Fail(AdmissionOutcome.Invalid, "Choose a school year and a class from this branch.");
        }

        return check.YearIsClosed ? AdmissionResultRepository.Fail(AdmissionOutcome.Invalid, "That school year is closed.") : null;
    }
}
