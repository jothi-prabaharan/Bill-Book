using Fee.Api.Services;
using Fee.Entity.Enums;
using Fee.Entity.Models;
using Fee.Entity.TableEntities;
using Fee.Repository;
using Xunit;

namespace Fee.Api.Tests;

/// <summary>
/// A guardian's fees in the parent portal (S9, TK-69): their posted demands
/// with the balance left, and their posted receipts with what each settled.
/// Drafts, voided documents and other guardians' documents never show.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class PortalFeeTests
{
    private const long Mother = 700;
    private const long Neighbour = 701;

    private readonly PostgresFixture _postgres;

    public PortalFeeTests(PostgresFixture postgres) => _postgres = postgres;

    private static FeeDemand Demand(long structureId, long headId, long contactId, string period, FeeDocumentStatus status, string? no, decimal paid = 0m) => new()
    {
        DemandNo = no,
        StudentId = 11,
        EnrolmentId = contactId,
        FeeStructureId = structureId,
        PeriodKey = period,
        ContactId = contactId,
        DemandDate = new DateOnly(2026, 6, 1),
        DueDate = new DateOnly(2026, 6, 10),
        DocumentStatus = status,
        TotalAmount = 5000m,
        ConcessionAmount = 500m,
        NetAmount = 4500m,
        PaidAmount = paid,
        Lines = [new FeeDemandLine { FeeHeadId = headId, Amount = 5000m, ConcessionAmount = 500m }],
    };

    [SkippableFact]
    public async Task A_guardian_sees_their_posted_demands_and_receipts_only()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);
        await using FeeDbContext db = _postgres.CreateContext(Guid.NewGuid(), Guid.NewGuid());

        var head = new FeeHead { Code = "TUI", Name = "Tuition" };
        var structure = new FeeStructure { AcademicYearId = 1, SchoolClassId = 1, Name = "Day scholar" };
        db.FeeHeads.Add(head);
        db.FeeStructures.Add(structure);
        await db.SaveChangesAsync();

        FeeDemand june = Demand(structure.FeeStructureId, head.FeeHeadId, Mother, "2026-06", FeeDocumentStatus.Posted, "FDM-00001", paid: 3000m);
        db.FeeDemands.AddRange(
            june,
            Demand(structure.FeeStructureId, head.FeeHeadId, Mother, "2026-07", FeeDocumentStatus.Draft, null),
            Demand(structure.FeeStructureId, head.FeeHeadId, Mother, "2026-08", FeeDocumentStatus.Void, "FDM-00002"),
            Demand(structure.FeeStructureId, head.FeeHeadId, Neighbour, "2026-06", FeeDocumentStatus.Posted, "FDM-00003"));
        await db.SaveChangesAsync();

        db.FeeReceipts.AddRange(
            new FeeReceipt
            {
                ReceiptNo = "FRC-00001", ContactId = Mother, ReceiptDate = new DateOnly(2026, 6, 5), PaymentMode = PaymentMode.Upi,
                BankAccountId = 1, Amount = 3500m, UnallocatedAmount = 500m,
                Allocations = [new FeeReceiptAllocation { FeeDemandId = june.FeeDemandId, Amount = 3000m }],
            },
            new FeeReceipt { ReceiptNo = "FRC-00002", ContactId = Neighbour, ReceiptDate = new DateOnly(2026, 6, 5), BankAccountId = 1, Amount = 100m });
        await db.SaveChangesAsync();

        var portal = new PortalFeeService(db);

        PortalDemandView demand = Assert.Single(await portal.DemandsAsync(Mother, default));
        Assert.Equal("FDM-00001", demand.DemandNo);
        Assert.Equal(1500m, demand.Balance);
        Assert.Equal("Tuition", Assert.Single(demand.Lines).FeeHeadName);

        PortalReceiptView receipt = Assert.Single(await portal.ReceiptsAsync(Mother, default));
        Assert.Equal("FRC-00001", receipt.ReceiptNo);
        Assert.Equal(PaymentMode.Upi, receipt.PaymentMode);
        Assert.Equal(["FDM-00001"], receipt.DemandNos);
        Assert.Equal(500m, receipt.UnallocatedAmount);
    }
}
