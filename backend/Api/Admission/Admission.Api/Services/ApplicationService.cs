using Admission.Entity.Enums;
using Admission.Entity.Models;
using Admission.Entity.TableEntities;
using Admission.Repository;
using Admission.Repository.SeedData;
using Microsoft.EntityFrameworkCore;
using Shared.Kernel.Contacts;
using Shared.Kernel.Numbering;
using Shared.Kernel.School;
using Shared.Kernel.Validation;

namespace Admission.Api.Services;

/// <summary>
/// Applications and admitting them (S2, TK-62).
///
/// <list type="bullet">
/// <item><b>Stages</b> move forward to Offered, and to Rejected or Withdrawn
/// from anywhere before; Admitted is reached only by admitting.</item>
/// <item><b>Admitting is idempotent end to end.</b> The guardian contact is
/// found by mobile number in Master or made; the student is made in Student keyed
/// on this application. Both are separate services, so no transaction spans
/// them: a failure after either leaves something behind that the retry finds
/// rather than duplicates. Admitting an application already admitted returns
/// its student.</item>
/// </list>
/// </summary>
public sealed class ApplicationService
{
    private readonly AdmissionDbContext _db;
    private readonly INumberGenerator _numbers;
    private readonly IStudentClient _sis;
    private readonly IContactDirectory _contacts;
    private readonly ILogger<ApplicationService> _log;

    public ApplicationService(
        AdmissionDbContext db, INumberGenerator numbers, IStudentClient sis, IContactDirectory contacts, ILogger<ApplicationService> log)
    {
        _db = db;
        _numbers = numbers;
        _sis = sis;
        _contacts = contacts;
        _log = log;
    }

    public async Task<List<ApplicationView>> ListAsync(ApplicationStage? stage, CancellationToken ct)
    {
        List<Application> rows = await _db.Applications.AsNoTracking()
            .Where(a => stage == null || a.ApplicationStage == stage)
            .OrderByDescending(a => a.ApplicationDate).ThenByDescending(a => a.ApplicationId)
            .Take(1000)
            .ToListAsync(ct);
        return [.. rows.Select(View)];
    }

    public async Task<ApplicationView?> GetAsync(long id, CancellationToken ct) =>
        await _db.Applications.AsNoTracking().Include(a => a.Documents).FirstOrDefaultAsync(a => a.ApplicationId == id, ct) is Application a
            ? View(a)
            : null;

    public async Task<AdmissionResult> SaveAsync(long? id, SaveApplicationRequest request, CancellationToken ct)
    {
        if (request.DateOfBirth >= request.ApplicationDate)
        {
            return AdmissionResult.Fail(AdmissionOutcome.Invalid, "The date of birth must be before the application date.");
        }

        if (PhoneNumbers.NormalizeOptional(request.GuardianPhone) is null)
        {
            return AdmissionResult.Fail(AdmissionOutcome.Invalid, "Give the guardian's mobile number.");
        }

        AdmissionResult? academic = await EnquiryService.CheckAcademicAsync(_sis, _log, request.AcademicYearId, request.SeekingClassId, ct);
        if (academic is not null)
        {
            return academic;
        }

        Application? application;
        if (id is long existing)
        {
            application = await _db.Applications.Include(a => a.Documents).FirstOrDefaultAsync(a => a.ApplicationId == existing, ct);
            if (application is null)
            {
                return AdmissionResult.Fail(AdmissionOutcome.NotFound);
            }

            if (IsClosed(application.ApplicationStage))
            {
                return AdmissionResult.Fail(AdmissionOutcome.StageRule, "An admitted, rejected or withdrawn application cannot be changed.");
            }

            _db.ApplicationDocuments.RemoveRange(application.Documents);
            await _db.SaveChangesAsync(ct);
            application.Documents.Clear();
        }
        else
        {
            if (request.EnquiryId is long enquiryId)
            {
                Enquiry? enquiry = await _db.Enquiries.FirstOrDefaultAsync(e => e.EnquiryId == enquiryId, ct);
                if (enquiry is null)
                {
                    return AdmissionResult.Fail(AdmissionOutcome.Invalid, "Choose an enquiry from this branch.");
                }

                if (enquiry.EnquiryStatus == EnquiryStatus.Converted)
                {
                    return AdmissionResult.Fail(AdmissionOutcome.StageRule, "That enquiry already has an application.");
                }

                enquiry.EnquiryStatus = EnquiryStatus.Converted;
            }

            NumberAllocation number = await _numbers.NextAsync(AdmissionSeed.ApplicationSeriesCode, request.ApplicationDate, ct);
            application = new Application { ApplicationNo = number.Code, EnquiryId = request.EnquiryId };
            _db.Applications.Add(application);
        }

        application.ApplicationDate = request.ApplicationDate;
        application.ChildFirstName = request.ChildFirstName.Trim();
        application.ChildLastName = string.IsNullOrWhiteSpace(request.ChildLastName) ? null : request.ChildLastName.Trim();
        application.DateOfBirth = request.DateOfBirth;
        application.ChildGender = request.ChildGender;
        application.SeekingClassId = request.SeekingClassId;
        application.AcademicYearId = request.AcademicYearId;
        application.GuardianName = request.GuardianName.Trim();
        application.GuardianPhone = PhoneNumbers.NormalizeOptional(request.GuardianPhone)!;
        application.GuardianEmail = string.IsNullOrWhiteSpace(request.GuardianEmail) ? null : request.GuardianEmail.Trim();
        application.GuardianRelationship = request.GuardianRelationship;
        application.ApplicationFee = request.ApplicationFee;

        foreach (ApplicationDocumentModel d in request.Documents)
        {
            application.Documents.Add(new ApplicationDocument
            {
                DocumentKind = d.DocumentKind,
                AttachmentKey = string.IsNullOrWhiteSpace(d.AttachmentKey) ? null : d.AttachmentKey.Trim(),
                Remarks = string.IsNullOrWhiteSpace(d.Remarks) ? null : d.Remarks.Trim(),
                IsVerified = d.IsVerified,
            });
        }

        await _db.SaveChangesAsync(ct);
        return AdmissionResult.Ok(application.ApplicationId);
    }

    public async Task<AdmissionResult> MoveAsync(long id, MoveApplicationRequest request, CancellationToken ct)
    {
        Application? application = await _db.Applications.Include(a => a.Documents).FirstOrDefaultAsync(a => a.ApplicationId == id, ct);
        if (application is null)
        {
            return AdmissionResult.Fail(AdmissionOutcome.NotFound);
        }

        if (!CanMove(application.ApplicationStage, request.ApplicationStage))
        {
            return AdmissionResult.Fail(AdmissionOutcome.StageRule, request.ApplicationStage == ApplicationStage.Admitted
                ? "An application is admitted with Admit, which makes the student."
                : "The application cannot move to that stage from where it is.");
        }

        if (request.ApplicationStage == ApplicationStage.DocumentsVerified && !DocumentsVerified(application.Documents))
        {
            return AdmissionResult.Fail(AdmissionOutcome.Invalid, "Record the documents and verify each one first.");
        }

        if (request.ApplicationStage == ApplicationStage.Assessed && request.AssessmentScore is null)
        {
            return AdmissionResult.Fail(AdmissionOutcome.Invalid, "Give the assessment score.");
        }

        application.ApplicationStage = request.ApplicationStage;
        if (request.AssessmentScore is decimal score)
        {
            application.AssessmentScore = score;
        }

        await _db.SaveChangesAsync(ct);
        return AdmissionResult.Ok(application.ApplicationId);
    }

    /// <summary>
    /// Admits an offered application: the guardian found or made in Master,
    /// the student made in Student, and the application marked admitted. Calling it
    /// again, even after a failure halfway, makes nothing twice.
    /// </summary>
    public async Task<AdmissionResult> AdmitAsync(long id, AdmitRequest request, CancellationToken ct)
    {
        Application? application = await _db.Applications.FirstOrDefaultAsync(a => a.ApplicationId == id, ct);
        if (application is null)
        {
            return AdmissionResult.Fail(AdmissionOutcome.NotFound);
        }

        if (application.ApplicationStage == ApplicationStage.Admitted && application.AdmittedStudentId is long already)
        {
            return AdmissionResult.Ok(application.ApplicationId, new AdmitResponse
            {
                StudentId = already, AdmissionNo = application.AdmissionNo ?? string.Empty, GuardianContactId = application.GuardianContactId ?? 0,
            });
        }

        if (application.ApplicationStage != ApplicationStage.Offered)
        {
            return AdmissionResult.Fail(AdmissionOutcome.StageRule, "Only an application that has been offered a seat can be admitted.");
        }

        if (request.AdmissionDate < application.ApplicationDate)
        {
            return AdmissionResult.Fail(AdmissionOutcome.Invalid, "The admission date cannot be before the application date.");
        }

        try
        {
            if (request.SectionId is long sectionId)
            {
                AcademicCheckResponse check = await _sis.CheckAsync(application.AcademicYearId, application.SeekingClassId, sectionId, ct);
                if (!check.SectionMatches)
                {
                    return AdmissionResult.Fail(AdmissionOutcome.Invalid, "Choose a section of the class sought, in the application's school year.");
                }
            }

            if (application.GuardianContactId is null)
            {
                EnsureGuardianResponse guardian = await _contacts.EnsureGuardianAsync(
                    application.GuardianName, application.GuardianPhone, application.GuardianEmail, ct);
                application.GuardianContactId = guardian.ContactId;
            }

            AdmitStudentResponse student = await _sis.AdmitAsync(new AdmitStudentRequest
            {
                SourceApplicationId = application.ApplicationId,
                FirstName = application.ChildFirstName,
                LastName = application.ChildLastName,
                DateOfBirth = application.DateOfBirth,
                Gender = application.ChildGender.ToString(),
                AdmissionDate = request.AdmissionDate,
                GuardianContactId = application.GuardianContactId.Value,
                GuardianRelationship = application.GuardianRelationship.ToString(),
                AcademicYearId = application.AcademicYearId,
                SectionId = request.SectionId,
                RollNo = request.RollNo,
            }, ct);

            application.AdmittedStudentId = student.StudentId;
            application.AdmissionNo = student.AdmissionNo;
            application.ApplicationStage = ApplicationStage.Admitted;
            await _db.SaveChangesAsync(ct);

            return AdmissionResult.Ok(application.ApplicationId, new AdmitResponse
            {
                StudentId = student.StudentId, AdmissionNo = student.AdmissionNo, GuardianContactId = application.GuardianContactId.Value,
            });
        }
        catch (StudentRefusedException refused)
        {
            return AdmissionResult.Fail(AdmissionOutcome.Invalid, refused.Message);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _log.LogError(ex, "Admitting application {ApplicationId} could not reach Student or Master.", id);
            return AdmissionResult.Fail(AdmissionOutcome.Unavailable);
        }
    }

    // ---- Rules, pure and public for tests -----------------------------------

    /// <summary>Admitted, Rejected and Withdrawn are final.</summary>
    public static bool IsClosed(ApplicationStage stage) =>
        stage is ApplicationStage.Admitted or ApplicationStage.Rejected or ApplicationStage.Withdrawn;

    /// <summary>
    /// Forward to any later stage up to Offered, so a school that does not
    /// assess can skip it; Rejected or Withdrawn from any open stage; never
    /// to Admitted, which only admitting reaches.
    /// </summary>
    public static bool CanMove(ApplicationStage from, ApplicationStage to)
    {
        if (IsClosed(from) || to == ApplicationStage.Admitted || from == to)
        {
            return false;
        }

        return to is ApplicationStage.Rejected or ApplicationStage.Withdrawn || to > from;
    }

    /// <summary>At least one document, and every one verified.</summary>
    public static bool DocumentsVerified(ICollection<ApplicationDocument> documents) =>
        documents.Count > 0 && documents.All(d => d.IsVerified);

    private static ApplicationView View(Application a) => new()
    {
        ApplicationId = a.ApplicationId,
        ApplicationNo = a.ApplicationNo,
        EnquiryId = a.EnquiryId,
        ApplicationDate = a.ApplicationDate,
        ChildFirstName = a.ChildFirstName,
        ChildLastName = a.ChildLastName,
        DateOfBirth = a.DateOfBirth,
        ChildGender = a.ChildGender,
        SeekingClassId = a.SeekingClassId,
        AcademicYearId = a.AcademicYearId,
        GuardianName = a.GuardianName,
        GuardianPhone = a.GuardianPhone,
        GuardianEmail = a.GuardianEmail,
        GuardianRelationship = a.GuardianRelationship,
        GuardianContactId = a.GuardianContactId,
        ApplicationStage = a.ApplicationStage,
        AssessmentScore = a.AssessmentScore,
        AdmittedStudentId = a.AdmittedStudentId,
        AdmissionNo = a.AdmissionNo,
        ApplicationFee = a.ApplicationFee,
        Documents = [.. a.Documents.Select(d => new ApplicationDocumentModel
        {
            ApplicationDocumentId = d.ApplicationDocumentId, DocumentKind = d.DocumentKind, AttachmentKey = d.AttachmentKey,
            Remarks = d.Remarks, IsVerified = d.IsVerified,
        })],
    };
}
