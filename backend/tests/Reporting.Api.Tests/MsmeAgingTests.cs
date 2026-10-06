using Microsoft.AspNetCore.Mvc;
using Reporting.Api.Controllers;
using Reporting.Repository;
using Reporting.Repository.ReadModels;
using Shared.Kernel.Documents;
using Shared.Kernel.Tenancy;
using Xunit;

namespace Reporting.Api.Tests;

/// <summary>
/// MSME Ageing report & Section 43B(h) disallowance tests (TK-119).
/// Verifies:
/// 1. Bills past 45 days appear in the "Overdue > 45 days" bucket with DisallowedAmount == TotalAmount.
/// 2. Bills <= 15 days and 16-45 days appear in their respective buckets with DisallowedAmount == 0.
/// 3. Non-MSME vendor bills and draft bills are excluded.
/// 4. Half-yearly MCA MSME Form 1 export lists delayed payments past 45 days with correct DaysDelayed.
/// </summary>
[Collection(nameof(PostgresCollection))]
public sealed class MsmeAgingTests
{
    private readonly PostgresFixture _postgres;

    public MsmeAgingTests(PostgresFixture postgres) => _postgres = postgres;

    [SkippableFact]
    public async Task MsmeAging_CategorizesBuckets_AndCalculatesDisallowanceCorrectly()
    {
        Skip.If(_postgres.SkipReason is not null, _postgres.SkipReason ?? string.Empty);

        Guid customerId = Guid.NewGuid();
        Guid orgId = Guid.NewGuid();
        var tenant = new TenantContext { CustomerId = customerId, OrgId = orgId };
        await using ReportingDbContext db = _postgres.CreateContext(customerId, orgId);

        DateOnly asOf = new(2026, 9, 30);
        long idBase = (DateTime.UtcNow.Ticks % 10_000_000) * 10 + Random.Shared.Next(1, 9);
        long msmeVendorId = idBase + 1;
        long nonMsmeVendorId = idBase + 2;
        long bill1Id = idBase + 3;
        long bill2Id = idBase + 4;
        long bill3Id = idBase + 5;
        long bill4Id = idBase + 6;
        long bill5Id = idBase + 7;

        db.Contacts.AddRange(
            new ContactRead
            {
                ContactId = msmeVendorId,
                ContactCode = "V-MSME",
                DisplayName = "Micro Precision Tools",
                LegalName = "Micro Precision Tools Pvt Ltd",
                CurrencyCode = "INR",
                IsVendor = true,
                IsMsme = true,
                UdyamNumber = "UDYAM-TN-01-0000123",
                Pan = "AAACM1234D",
                IsActive = true,
                CustomerId = customerId,
                OrgId = orgId,
            },
            new ContactRead
            {
                ContactId = nonMsmeVendorId,
                ContactCode = "V-CORP",
                DisplayName = "Heavy Industry Corp",
                LegalName = "Heavy Industry Corporation Ltd",
                CurrencyCode = "INR",
                IsVendor = true,
                IsMsme = false,
                IsActive = true,
                CustomerId = customerId,
                OrgId = orgId,
            }
        );

        db.Bills.AddRange(
            // Bill 1: 10 days old (<= 15 days bucket)
            new BillRead
            {
                BillId = bill1Id,
                TransactionTypeCode = "BIL",
                DocumentNo = "BIL-001",
                VendorBillNo = "INV-MSME-1",
                VendorBillDate = asOf.AddDays(-10),
                DocumentDate = asOf.AddDays(-10),
                DueDate = asOf.AddDays(20),
                ContactId = msmeVendorId,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                TotalAmount = 15000m,
                Status = DocumentStatus.Posted,
                CustomerId = customerId,
                OrgId = orgId,
            },
            // Bill 2: 30 days old (16 - 45 days bucket)
            new BillRead
            {
                BillId = bill2Id,
                TransactionTypeCode = "BIL",
                DocumentNo = "BIL-002",
                VendorBillNo = "INV-MSME-2",
                VendorBillDate = asOf.AddDays(-30),
                DocumentDate = asOf.AddDays(-30),
                DueDate = asOf.AddDays(15),
                ContactId = msmeVendorId,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                TotalAmount = 25000m,
                Status = DocumentStatus.Posted,
                CustomerId = customerId,
                OrgId = orgId,
            },
            // Bill 3: 55 days old (Overdue > 45 days bucket)
            new BillRead
            {
                BillId = bill3Id,
                TransactionTypeCode = "BIL",
                DocumentNo = "BIL-003",
                VendorBillNo = "INV-MSME-3",
                VendorBillDate = asOf.AddDays(-55),
                DocumentDate = asOf.AddDays(-55),
                DueDate = asOf.AddDays(-10),
                ContactId = msmeVendorId,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                TotalAmount = 40000m,
                Status = DocumentStatus.Posted,
                CustomerId = customerId,
                OrgId = orgId,
            },
            // Bill 4: Non-MSME vendor, 60 days old (should be excluded)
            new BillRead
            {
                BillId = bill4Id,
                TransactionTypeCode = "BIL",
                DocumentNo = "BIL-004",
                VendorBillNo = "INV-CORP-4",
                VendorBillDate = asOf.AddDays(-60),
                DocumentDate = asOf.AddDays(-60),
                DueDate = asOf.AddDays(-15),
                ContactId = nonMsmeVendorId,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                TotalAmount = 90000m,
                Status = DocumentStatus.Posted,
                CustomerId = customerId,
                OrgId = orgId,
            },
            // Bill 5: MSME vendor, 50 days old, Draft status (should be excluded)
            new BillRead
            {
                BillId = bill5Id,
                TransactionTypeCode = "BIL",
                DocumentNo = "BIL-005",
                VendorBillNo = "INV-MSME-5",
                VendorBillDate = asOf.AddDays(-50),
                DocumentDate = asOf.AddDays(-50),
                DueDate = asOf.AddDays(-5),
                ContactId = msmeVendorId,
                CurrencyCode = "INR",
                ExchangeRate = 1m,
                TotalAmount = 10000m,
                Status = DocumentStatus.Draft,
                CustomerId = customerId,
                OrgId = orgId,
            }
        );

        await db.SaveChangesAsync();

        var controller = new MsmeController(db, tenant);

        // Act: Summary KPIs
        var summaryResult = await controller.GetSummary(asOf, default);
        var okSummary = Assert.IsType<OkObjectResult>(summaryResult);
        var summary = Assert.IsType<MsmeSummaryDto>(okSummary.Value);

        // Assert: MSME payables categorization & Sec 43B(h) disallowance
        Assert.Equal(80000m, summary.TotalMsmePayables);
        Assert.Equal(15000m, summary.Within15Days);
        Assert.Equal(25000m, summary.Days16To45);
        Assert.Equal(40000m, summary.OverduePast45Days);
        Assert.Equal(40000m, summary.DisallowedUnder43Bh);
        Assert.Equal(1, summary.MsmeVendorCount);
        Assert.Equal(1, summary.OverdueBillCount);

        // Act: MCA MSME Form 1 (half-yearly return for bills delayed past 45 days)
        var mcaResult = await controller.GetMcaForm1(asOf.AddMonths(-6), asOf, default);
        var okMca = Assert.IsType<OkObjectResult>(mcaResult);
        var mcaItems = Assert.IsType<List<McaMsmeForm1Item>>(okMca.Value);

        // Assert: Only Bill 3 is overdue past 45 days
        Assert.Single(mcaItems);
        var item = mcaItems[0];
        Assert.Equal(bill3Id, item.BillId);
        Assert.Equal("INV-MSME-3", item.BillNo);
        Assert.Equal("Micro Precision Tools", item.VendorName);
        Assert.Equal(40000m, item.AmountDue);
        Assert.Equal(10, item.DaysDelayed);
    }
}
