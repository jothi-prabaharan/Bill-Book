using Microsoft.EntityFrameworkCore;
using Recruitment.Api.Services;
using Recruitment.Entity.Enums;
using Recruitment.Entity.Models;
using Recruitment.Entity.TableEntities;
using Recruitment.Repository;
using Shared.Kernel.Employees;
using Shared.Kernel.Numbering;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Recruitment.Api.Tests;

[Collection(nameof(PostgresCollection))]
public sealed class OfferAcceptanceIdempotencyTests
{
    private readonly PostgresFixture _postgres;

    public OfferAcceptanceIdempotencyTests(PostgresFixture postgres) => _postgres = postgres;

    private sealed class FakeNumberGenerator : INumberGenerator
    {
        private long _counter = 100;
        public Task<NumberAllocation> NextAsync(string seriesCode, DateOnly onDate, CancellationToken ct) =>
            Task.FromResult(new NumberAllocation(1, _counter++, $"{seriesCode}-{_counter}"));

        public Task<string?> PeekAsync(string seriesCode, DateOnly onDate, CancellationToken ct) =>
            Task.FromResult<string?>($"{seriesCode}-peek");
    }

    private sealed class FakeHrmClient : IHrmClient
    {
        public int OnboardCallCount { get; private set; }
        private readonly Dictionary<string, (long Id, string Code)> _byEmail = new();
        private long _idGen = 1000;

        public Task<OnboardEmployeeResult?> OnboardEmployeeAsync(OnboardEmployeeRequest request, CancellationToken ct)
        {
            OnboardCallCount++;
            if (_byEmail.TryGetValue(request.Email.ToLower(), out var existing))
            {
                return Task.FromResult<OnboardEmployeeResult?>(new OnboardEmployeeResult
                {
                    EmployeeId = existing.Id,
                    EmployeeCode = existing.Code,
                    AlreadyExisted = true,
                });
            }

            long newId = ++_idGen;
            string newCode = $"EMP-{newId}";
            _byEmail[request.Email.ToLower()] = (newId, newCode);

            return Task.FromResult<OnboardEmployeeResult?>(new OnboardEmployeeResult
            {
                EmployeeId = newId,
                EmployeeCode = newCode,
                AlreadyExisted = false,
            });
        }

        public Task<EmployeeProfile?> FindByIdAsync(Guid customerId, Guid orgId, long employeeId, CancellationToken ct)
        {
            var match = _byEmail.Values.FirstOrDefault(v => v.Id == employeeId);
            return Task.FromResult<EmployeeProfile?>(new EmployeeProfile
            {
                EmployeeId = employeeId,
                EmployeeCode = match.Code ?? $"EMP-{employeeId}",
                FullName = "Candidate Name",
            });
        }

        public Task<EmployeeProfile?> FindByUserIdAsync(Guid customerId, Guid orgId, Guid userId, CancellationToken ct) =>
            Task.FromResult<EmployeeProfile?>(null);
    }

    private sealed class FakePayrollClient : IPayrollClient
    {
        public int AssignCount { get; private set; }
        public Task<bool> AssignSalaryAsync(long employeeId, long salaryStructureId, decimal annualCtc, DateOnly effectiveFrom, CancellationToken ct)
        {
            AssignCount++;
            return Task.FromResult(true);
        }
    }

    [SkippableFact]
    public async Task Accepting_an_offer_twice_creates_only_one_employee()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        var customerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var tenant = new TenantContext
        {
            CustomerId = customerId,
            OrgId = orgId,
        };

        await using var db = _postgres.CreateContext(customerId, orgId);

        var hrmClient = new FakeHrmClient();
        var payrollClient = new FakePayrollClient();
        var numbers = new FakeNumberGenerator();

        var service = new RecruitmentService(db, tenant, numbers, hrmClient, payrollClient);

        // 1. Seed requisition, opening, candidate, application, and offer
        var req = new JobRequisition
        {
            CustomerId = tenant.CustomerId!.Value,
            OrgId = tenant.OrgId!.Value,
            RequisitionCode = "REQ-001",
            DepartmentId = 1,
            DesignationId = 2,
            GradeId = 3,
            WorkLocationId = 4,
            Openings = 2,
            EmploymentType = EmploymentType.Permanent,
            MinCtc = 500000,
            MaxCtc = 800000,
            Justification = "Backfill role",
            ApprovalStatus = ApprovalStatus.Approved,
        };
        db.JobRequisitions.Add(req);
        await db.SaveChangesAsync();

        var opening = new JobOpening
        {
            CustomerId = tenant.CustomerId!.Value,
            OrgId = tenant.OrgId!.Value,
            JobRequisitionId = req.JobRequisitionId,
            Title = "Senior Software Engineer",
            Description = "Full stack developer role",
            OpeningStatus = OpeningStatus.Open,
        };
        db.JobOpenings.Add(opening);
        await db.SaveChangesAsync();

        var candidate = new Candidate
        {
            CustomerId = tenant.CustomerId!.Value,
            OrgId = tenant.OrgId!.Value,
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice.smith@example.com",
            Phone = "9876543210",
            CandidateSource = CandidateSource.Portal,
        };
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();

        var app = new Application
        {
            CustomerId = tenant.CustomerId!.Value,
            OrgId = tenant.OrgId!.Value,
            JobOpeningId = opening.JobOpeningId,
            CandidateId = candidate.CandidateId,
            Stage = ApplicationStage.Interview,
        };
        db.Applications.Add(app);
        await db.SaveChangesAsync();

        var offer = new Offer
        {
            CustomerId = tenant.CustomerId!.Value,
            OrgId = tenant.OrgId!.Value,
            ApplicationId = app.ApplicationId,
            OfferedCtc = 750000,
            SalaryStructureId = 10,
            JoiningDate = new DateOnly(2026, 10, 1),
            OfferStatus = OfferStatus.Approved,
            ApprovalStatus = ApprovalStatus.Approved,
        };
        db.Offers.Add(offer);
        await db.SaveChangesAsync();

        // 2. Accept the offer for the first time
        AcceptOfferResult firstResult = await service.AcceptOfferAsync(offer.OfferId, default);

        Assert.NotNull(firstResult);
        Assert.False(firstResult.AlreadyExisted);
        Assert.True(firstResult.EmployeeId > 0);
        Assert.NotEmpty(firstResult.EmployeeCode);
        Assert.Equal(1, hrmClient.OnboardCallCount);
        Assert.Equal(1, payrollClient.AssignCount);

        // Verify offer and application updated
        var updatedOffer = await db.Offers.FindAsync(offer.OfferId);
        Assert.NotNull(updatedOffer);
        Assert.Equal(OfferStatus.Accepted, updatedOffer.OfferStatus);
        Assert.Equal(firstResult.EmployeeId, updatedOffer.CreatedEmployeeId);
        Assert.NotNull(updatedOffer.AcceptedAt);

        var updatedApp = await db.Applications.FindAsync(app.ApplicationId);
        Assert.NotNull(updatedApp);
        Assert.Equal(ApplicationStage.Hired, updatedApp.Stage);

        // 3. Accept the offer for the second time (Idempotent retry)
        AcceptOfferResult secondResult = await service.AcceptOfferAsync(offer.OfferId, default);

        Assert.NotNull(secondResult);
        Assert.True(secondResult.AlreadyExisted);
        Assert.Equal(firstResult.EmployeeId, secondResult.EmployeeId);
        Assert.Equal(firstResult.EmployeeCode, secondResult.EmployeeCode);

        // Crucial: No second employee creation was invoked
        Assert.Equal(1, hrmClient.OnboardCallCount);
        Assert.Equal(1, payrollClient.AssignCount);
    }
}
