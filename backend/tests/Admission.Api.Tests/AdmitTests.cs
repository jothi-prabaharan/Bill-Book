using Admission.Api.Services;
using Admission.Entity.Enums;
using Admission.Entity.Models;
using Admission.Entity.TableEntities;
using Admission.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Kernel.Contacts;
using Shared.Kernel.Numbering;
using Shared.Kernel.School;
using Xunit;

namespace Admission.Api.Tests;

/// <summary>Student, as admitting sees it: idempotent on the application, like the real one (TK-61).</summary>
internal sealed class FakeStudent : IStudentClient
{
    private readonly Dictionary<long, AdmitStudentResponse> _byApplication = [];

    public int StudentsCreated { get; private set; }

    public int FailNextAdmits { get; set; }

    public bool SectionMatches { get; set; } = true;

    public Task<AcademicCheckResponse> CheckAsync(long? academicYearId, long? schoolClassId, long? sectionId, CancellationToken ct) =>
        Task.FromResult(new AcademicCheckResponse
        {
            YearExists = academicYearId is > 0, ClassExists = schoolClassId is > 0, SectionExists = sectionId is > 0, SectionMatches = SectionMatches,
        });

    public Task<SectionRollResponse> RollAsync(long sectionId, CancellationToken ct) =>
        Task.FromResult(new SectionRollResponse());

    public Task<IReadOnlyList<EnrolmentInfo>> EnrolmentsAsync(EnrolmentQueryRequest query, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<AdmitStudentResponse> AdmitAsync(AdmitStudentRequest request, CancellationToken ct)
    {
        if (FailNextAdmits > 0)
        {
            FailNextAdmits--;
            throw new HttpRequestException("Student is down.");
        }

        if (!_byApplication.TryGetValue(request.SourceApplicationId, out AdmitStudentResponse? student))
        {
            StudentsCreated++;
            student = new AdmitStudentResponse { StudentId = 1000 + StudentsCreated, AdmissionNo = $"ADM-{StudentsCreated:00000}" };
            _byApplication[request.SourceApplicationId] = student;
        }

        return Task.FromResult(student);
    }
}

/// <summary>Master's guardians, found by mobile number like the real endpoint.</summary>
internal sealed class FakeGuardians : IContactDirectory
{
    private readonly Dictionary<string, long> _byMobile = [];

    public int GuardiansCreated { get; private set; }

    public Task<IReadOnlyDictionary<long, ContactSummary>> FindAsync(IEnumerable<long> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<long, ContactSummary>>(new Dictionary<long, ContactSummary>());

    public Task<EnsureGuardianResponse> EnsureGuardianAsync(string displayName, string mobileNumber, string? email, CancellationToken ct)
    {
        if (_byMobile.TryGetValue(mobileNumber, out long id))
        {
            return Task.FromResult(new EnsureGuardianResponse { ContactId = id, Created = false });
        }

        GuardiansCreated++;
        _byMobile[mobileNumber] = 500 + GuardiansCreated;
        return Task.FromResult(new EnsureGuardianResponse { ContactId = 500 + GuardiansCreated, Created = true });
    }
}

internal sealed class AprilYear : IFinancialYearProvider
{
    public Task<int> GetStartMonthAsync(CancellationToken ct = default) => Task.FromResult(4);
}

/// <summary>
/// Applications and admitting (S2, TK-62), against a real database with Student
/// and Master faked. The card's Done-when: admitting twice creates one student.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class AdmitTests
{
    private readonly PostgresFixture _postgres;

    public AdmitTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed record Branch(AdmissionDbContext Db, FakeStudent Student, FakeGuardians Guardians);

    private async Task<Branch> NewBranchAsync()
    {
        Guid orgId = Guid.NewGuid();
        AdmissionDbContext db = _postgres.CreateContext(Guid.NewGuid(), orgId);
        await new AdmissionSeeder(db).SeedForOrganizationAsync(orgId, default);
        return new Branch(db, new FakeStudent(), new FakeGuardians());
    }

    private static ApplicationService Service(Branch b) => new(
        b.Db,
        new NumberGenerator(b.Db, Options.Create(new NumberingOptions()), new AprilYear()),
        b.Student,
        b.Guardians,
        NullLogger<ApplicationService>.Instance);

    private static SaveApplicationRequest Meera(string mobile = "9840012345") => new()
    {
        ApplicationDate = new DateOnly(2026, 4, 10),
        ChildFirstName = "Meera",
        ChildLastName = "Raman",
        DateOfBirth = new DateOnly(2020, 7, 1),
        ChildGender = ChildGender.Female,
        SeekingClassId = 1,
        AcademicYearId = 1,
        GuardianName = "Lakshmi Raman",
        GuardianPhone = mobile,
        GuardianRelationship = ParentRelationship.Mother,
        Documents = [new() { DocumentKind = DocumentKind.BirthCertificate, IsVerified = true }],
    };

    private static async Task<long> OfferedAsync(ApplicationService service, SaveApplicationRequest request)
    {
        long id = (await service.SaveAsync(null, request, default)).Id!.Value;
        Assert.Equal(AdmissionOutcome.Ok, (await service.MoveAsync(id, new MoveApplicationRequest { ApplicationStage = ApplicationStage.Offered }, default)).Outcome);
        return id;
    }

    [SkippableFact]
    public async Task Admitting_twice_creates_one_student_and_one_guardian()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using AdmissionDbContext _ = b.Db;

        long id = await OfferedAsync(Service(b), Meera());
        var admit = new AdmitRequest { AdmissionDate = new DateOnly(2026, 6, 1) };

        AdmissionResult first = await Service(b).AdmitAsync(id, admit, default);
        AdmissionResult second = await Service(b).AdmitAsync(id, admit, default);

        Assert.Equal(AdmissionOutcome.Ok, first.Outcome);
        Assert.Equal(AdmissionOutcome.Ok, second.Outcome);
        Assert.Equal(((AdmitResponse)first.Body!).StudentId, ((AdmitResponse)second.Body!).StudentId);
        Assert.Equal(1, b.Student.StudentsCreated);
        Assert.Equal(1, b.Guardians.GuardiansCreated);

        Application row = await b.Db.Applications.AsNoTracking().SingleAsync();
        Assert.Equal(ApplicationStage.Admitted, row.ApplicationStage);
        Assert.Equal("ADM-00001", row.AdmissionNo);
    }

    [SkippableFact]
    public async Task An_admit_that_fails_halfway_makes_nothing_twice_when_retried()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using AdmissionDbContext _ = b.Db;

        long id = await OfferedAsync(Service(b), Meera());
        b.Student.FailNextAdmits = 1;

        // The guardian is made in Master, then Student is down.
        Assert.Equal(AdmissionOutcome.Unavailable, (await Service(b).AdmitAsync(id, new AdmitRequest { AdmissionDate = new DateOnly(2026, 6, 1) }, default)).Outcome);

        // The request's transaction would have rolled the application back;
        // here the row is simply re-read, as the retry would find it.
        b.Db.ChangeTracker.Clear();
        await b.Db.Applications.Where(a => a.ApplicationId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.GuardianContactId, (long?)null));

        Assert.Equal(AdmissionOutcome.Ok, (await Service(b).AdmitAsync(id, new AdmitRequest { AdmissionDate = new DateOnly(2026, 6, 1) }, default)).Outcome);
        Assert.Equal(1, b.Student.StudentsCreated);
        Assert.Equal(1, b.Guardians.GuardiansCreated);
    }

    [SkippableFact]
    public async Task Two_children_of_one_parent_share_one_guardian()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using AdmissionDbContext _ = b.Db;

        long meera = await OfferedAsync(Service(b), Meera());
        SaveApplicationRequest brother = Meera();
        brother.ChildFirstName = "Arun";
        long arun = await OfferedAsync(Service(b), brother);

        await Service(b).AdmitAsync(meera, new AdmitRequest { AdmissionDate = new DateOnly(2026, 6, 1) }, default);
        await Service(b).AdmitAsync(arun, new AdmitRequest { AdmissionDate = new DateOnly(2026, 6, 1) }, default);

        Assert.Equal(2, b.Student.StudentsCreated);
        Assert.Equal(1, b.Guardians.GuardiansCreated);
    }

    [SkippableFact]
    public async Task Only_an_offered_application_is_admitted()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using AdmissionDbContext _ = b.Db;

        long id = (await Service(b).SaveAsync(null, Meera(), default)).Id!.Value;

        Assert.Equal(AdmissionOutcome.StageRule, (await Service(b).AdmitAsync(id, new AdmitRequest { AdmissionDate = new DateOnly(2026, 6, 1) }, default)).Outcome);
        Assert.Equal(0, b.Student.StudentsCreated);
    }

    [SkippableFact]
    public async Task A_section_of_another_class_is_refused_before_anything_is_made()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using AdmissionDbContext _ = b.Db;

        long id = await OfferedAsync(Service(b), Meera());
        b.Student.SectionMatches = false;

        Assert.Equal(AdmissionOutcome.Invalid,
            (await Service(b).AdmitAsync(id, new AdmitRequest { AdmissionDate = new DateOnly(2026, 6, 1), SectionId = 9 }, default)).Outcome);
        Assert.Equal(0, b.Guardians.GuardiansCreated);
    }

    [SkippableFact]
    public async Task An_application_is_numbered_and_its_enquiry_converted_once()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using AdmissionDbContext _ = b.Db;

        var enquiries = new EnquiryService(b.Db, b.Student, NullLogger<EnquiryService>.Instance);
        long enquiry = (await enquiries.SaveAsync(null, new SaveEnquiryRequest
        {
            EnquiryDate = new DateOnly(2026, 3, 20), ChildName = "Meera", SeekingClassId = 1, AcademicYearId = 1,
            ParentName = "Lakshmi Raman", Phone = "9840012345",
        }, default)).Id!.Value;

        SaveApplicationRequest request = Meera();
        request.EnquiryId = enquiry;

        AdmissionResult first = await Service(b).SaveAsync(null, request, default);
        Assert.Equal(AdmissionOutcome.Ok, first.Outcome);
        Assert.Equal("APL/2627/00001", await b.Db.Applications.Select(a => a.ApplicationNo).SingleAsync());
        Assert.Equal(EnquiryStatus.Converted, await b.Db.Enquiries.Select(e => e.EnquiryStatus).SingleAsync());

        Assert.Equal(AdmissionOutcome.StageRule, (await Service(b).SaveAsync(null, request, default)).Outcome);
    }

    [SkippableFact]
    public async Task Documents_verified_needs_every_document_verified()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        Branch b = await NewBranchAsync();
        await using AdmissionDbContext _ = b.Db;

        SaveApplicationRequest request = Meera();
        request.Documents.Add(new ApplicationDocumentModel { DocumentKind = DocumentKind.Photo });
        long id = (await Service(b).SaveAsync(null, request, default)).Id!.Value;

        Assert.Equal(AdmissionOutcome.Invalid,
            (await Service(b).MoveAsync(id, new MoveApplicationRequest { ApplicationStage = ApplicationStage.DocumentsVerified }, default)).Outcome);
    }
}

/// <summary>The application stages, pure (S2, TK-62).</summary>
public sealed class ApplicationStageTests
{
    [Theory]
    [InlineData(ApplicationStage.Submitted, ApplicationStage.DocumentsVerified, true)]
    [InlineData(ApplicationStage.Submitted, ApplicationStage.Offered, true)]
    [InlineData(ApplicationStage.Assessed, ApplicationStage.Offered, true)]
    [InlineData(ApplicationStage.Offered, ApplicationStage.Rejected, true)]
    [InlineData(ApplicationStage.Submitted, ApplicationStage.Withdrawn, true)]
    [InlineData(ApplicationStage.Offered, ApplicationStage.Assessed, false)]
    [InlineData(ApplicationStage.Offered, ApplicationStage.Admitted, false)]
    [InlineData(ApplicationStage.Admitted, ApplicationStage.Rejected, false)]
    [InlineData(ApplicationStage.Rejected, ApplicationStage.Offered, false)]
    [InlineData(ApplicationStage.Withdrawn, ApplicationStage.Submitted, false)]
    public void An_application_moves_forward_or_ends_and_admits_only_by_admitting(ApplicationStage from, ApplicationStage to, bool allowed) =>
        Assert.Equal(allowed, ApplicationService.CanMove(from, to));

    [Fact]
    public void Documents_are_verified_only_when_there_are_some_and_all_are()
    {
        Assert.False(ApplicationService.DocumentsVerified([]));
        Assert.False(ApplicationService.DocumentsVerified([new ApplicationDocument { IsVerified = true }, new ApplicationDocument()]));
        Assert.True(ApplicationService.DocumentsVerified([new ApplicationDocument { IsVerified = true }]));
    }
}
